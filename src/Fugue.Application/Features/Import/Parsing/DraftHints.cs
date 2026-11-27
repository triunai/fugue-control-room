using System.Globalization;
using System.Text.RegularExpressions;

namespace Fugue.Application.Features.Import.Parsing;

/// <summary>
/// Reads claim fields out of free text (an email body, chat messages). Every finding comes with a hint that says where it came from, because these are
/// guesses for a person to confirm, never facts: nothing here is trusted until the reviewer has looked at the draft.
/// </summary>
public static partial class DraftHints
{
    private static readonly string[] MotorWords =
        ["car", "vehicle", "bumper", "collision", "rear-ended", "rear ended", "crash", "motorcycle", "windscreen", "windshield", "taxi", "van", "parked", "lorry", "scratch", "dent"];

    private static readonly string[] PropertyWords =
        ["flat", "house", "home", "apartment", "condo", "leak", "flood", "fire", "burglary", "break-in", "ceiling", "pipe", "roof", "kitchen", "water damage", "shophouse"];

    private static readonly (string Name, string Code)[] MarketNames =
        [("singapore", "SG"), ("malaysia", "MY"), ("thailand", "TH"), ("hong kong", "HK"), ("indonesia", "ID"), ("philippines", "PH")];

    /// <summary>Calling codes of the six markets, longest first so +852 wins over a shorter prefix.</summary>
    private static readonly (string Prefix, string Code)[] CallingCodes =
        [("+852", "HK"), ("+65", "SG"), ("+60", "MY"), ("+66", "TH"), ("+62", "ID"), ("+63", "PH")];

    public static (string Value, string Hint)? Policy(string text)
    {
        foreach (Match m in LabeledPolicy().Matches(text))
        {
            string value = m.Groups["v"].Value.TrimEnd('.', ',', '-', '/');
            if (value.Any(char.IsDigit))
            {
                return (value.ToUpperInvariant(), $"Policy number {value.ToUpperInvariant()} read from \"{Shorten(m.Value)}\".");
            }
        }

        return PolicyShape().Match(text) is { Success: true } shape
            ? (shape.Value.ToUpperInvariant(), $"Policy number {shape.Value.ToUpperInvariant()} matched the POL-... shape.")
            : null;
    }

    /// <summary>
    /// The incident date: a labelled date first ("Date of incident: 27/11/2026"), then any explicit date, then "yesterday" or "today" counted from
    /// <paramref name="sentOn"/>. Day-first (dd/mm) like every one of the six markets; a date after <paramref name="sentOn"/> is skipped.
    /// </summary>
    public static (string Iso, string Hint)? IncidentDate(string text, DateOnly sentOn)
    {
        if (LabeledDate().Match(text) is { Success: true } labeled && ParseDate(labeled.Groups["v"].Value, sentOn) is { } d0 && d0 <= sentOn)
        {
            return (Iso(d0), $"Incident date {Iso(d0)} read from \"{Shorten(labeled.Value)}\".");
        }

        foreach (Match m in AnyDate().Matches(text))
        {
            if (ParseDate(m.Value, sentOn) is { } d && d <= sentOn)
            {
                return (Iso(d), $"Incident date {Iso(d)} is the first date in the text (\"{m.Value}\"); check it is the day of the incident.");
            }
        }

        if (Relative().Match(text) is { Success: true } relative)
        {
            string word = relative.Value.ToLowerInvariant();
            DateOnly d = word is "today" or "this morning" or "this evening" or "tonight" or "just now" ? sentOn : sentOn.AddDays(-1);
            return (Iso(d), $"Incident date {Iso(d)} counted from \"{relative.Value}\" in a message sent {Iso(sentOn)}.");
        }

        return null;
    }

    public static string? Location(string text) =>
        LabeledLocation().Match(text) is { Success: true } m ? m.Groups["v"].Value.Trim().TrimEnd('.') : null;

    public static (string Line, string Hint)? Line(string text)
    {
        if (LabeledLine().Match(text) is { Success: true } labeled)
        {
            string line = char.ToUpperInvariant(labeled.Groups["v"].Value[0]) + labeled.Groups["v"].Value[1..].ToLowerInvariant();
            return (line, $"Line {line} read from \"{Shorten(labeled.Value)}\".");
        }

        string lower = text.ToLowerInvariant();
        string[] motor = MotorWords.Where(w => ContainsWord(lower, w)).ToArray();
        string[] property = PropertyWords.Where(w => ContainsWord(lower, w)).ToArray();
        if (motor.Length == property.Length)
        {
            return null;
        }

        (string chosen, string[] words) = motor.Length > property.Length ? ("Motor", motor) : ("Property", property);
        return (chosen, $"Line {chosen} is a guess from the words {string.Join(", ", words.Take(3).Select(w => $"\"{w}\""))}.");
    }

    /// <summary>A market written in the text: "Market: SG" or a labelled country name.</summary>
    public static (string Market, string Hint)? MarketInText(string text)
    {
        if (LabeledMarket().Match(text) is not { Success: true } m)
        {
            return null;
        }

        string value = m.Groups["v"].Value.Trim().ToLowerInvariant();
        string? code = value.Length == 2 && MarketNames.Any(n => n.Code.Equals(value, StringComparison.OrdinalIgnoreCase))
            ? value.ToUpperInvariant()
            : MarketNames.FirstOrDefault(n => value.StartsWith(n.Name, StringComparison.Ordinal)).Code;
        return code is null ? null : (code, $"Market {code} read from \"{Shorten(m.Value)}\".");
    }

    /// <summary>The market of a phone number by its calling code (+65 Singapore ...); null for a name or another country.</summary>
    public static (string Market, string Hint)? MarketForPhone(string sender)
    {
        string digits = new(sender.Where(c => char.IsDigit(c) || c == '+').ToArray());
        foreach ((string prefix, string code) in CallingCodes)
        {
            if (digits.StartsWith(prefix, StringComparison.Ordinal))
            {
                return (code, $"Market {code} from the sender's calling code {prefix}.");
            }
        }

        return null;
    }

    /// <summary>
    /// Routes a sender domain to a market with the configured table (<c>Import:MarketByDomain</c>): the longest configured suffix wins, so an entry for
    /// <c>broker.com.sg</c> beats one for <c>com.sg</c>, and <c>claims.broker.com.sg</c> matches either.
    /// </summary>
    public static (string Market, string Hint)? MarketForDomain(string domain, IReadOnlyDictionary<string, string> marketByDomain)
    {
        string d = domain.Trim().TrimEnd('.').ToLowerInvariant();
        string[] labels = d.Split('.');
        for (int i = 0; i < labels.Length; i++)
        {
            string suffix = string.Join('.', labels[i..]);
            foreach ((string key, string market) in marketByDomain)
            {
                if (key.Trim().TrimStart('.').Equals(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return (market, $"Market {market} routed from the sender domain {d} (Import:MarketByDomain entry \"{key}\").");
                }
            }
        }

        return null;
    }

    public static string? Email(string text) => EmailAddress().Match(text) is { Success: true } m ? m.Value : null;

    /// <summary>An email on a line about the claimant ("Insured: Sam Lee (sam@example.com)"), for a broker writing on a client's behalf.</summary>
    public static (string Email, string Hint)? ClaimantEmailInText(string text) =>
        LabeledClaimant().Match(text) is { Success: true } line && Email(line.Value) is { } email
            ? (email, $"Claimant email {email} read from \"{Shorten(line.Value)}\", not the sender.")
            : null;

    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Parses one date written day-first (27/11/2026, 27-11-26), ISO (2026-11-27) or with a month name (27 Nov 2026, Nov 27); a missing year is the year that keeps the date on or before <paramref name="sentOn"/>.</summary>
    public static DateOnly? ParseDate(string text, DateOnly sentOn)
    {
        text = text.Trim();
        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly iso))
        {
            return iso;
        }

        if (NumericDate().Match(text) is { Success: true } n)
        {
            int day = int.Parse(n.Groups["d"].Value, CultureInfo.InvariantCulture);
            int month = int.Parse(n.Groups["m"].Value, CultureInfo.InvariantCulture);
            int year = int.Parse(n.Groups["y"].Value, CultureInfo.InvariantCulture);
            year = year < 100 ? 2000 + year : year;
            return Safe(year, month, day);
        }

        if (NamedDate().Match(text) is { Success: true } named)
        {
            int day = int.Parse(named.Groups["d1"].Success ? named.Groups["d1"].Value : named.Groups["d2"].Value, CultureInfo.InvariantCulture);
            int month = MonthOf(named.Groups["mon"].Value);
            if (named.Groups["y"].Success)
            {
                return Safe(int.Parse(named.Groups["y"].Value, CultureInfo.InvariantCulture), month, day);
            }

            DateOnly? sameYear = Safe(sentOn.Year, month, day);
            return sameYear is { } s && s > sentOn ? Safe(sentOn.Year - 1, month, day) : sameYear;
        }

        return null;
    }

    private static DateOnly? Safe(int year, int month, int day) =>
        year is >= 1900 and <= 2999 && month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month) ? new DateOnly(year, month, day) : null;

    private static int MonthOf(string name) =>
        Array.FindIndex(CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedMonthNames, m => m.Length > 0 && name.StartsWith(m, StringComparison.OrdinalIgnoreCase)) + 1;

    private static bool ContainsWord(string lower, string word) =>
        Regex.IsMatch(lower, $@"(?<![a-z]){Regex.Escape(word)}(?![a-z])", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

    private static string Shorten(string text)
    {
        string flat = WhiteSpace().Replace(text.Trim(), " ");
        return flat.Length <= 60 ? flat : flat[..57] + "...";
    }

    [GeneratedRegex(@"\bpolicy(?:\s*(?:no\.?|number|num|#))?\s*(?:is|:|-|#)?\s*(?<v>[A-Z0-9][A-Z0-9/-]{3,63})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 200)]
    private static partial Regex LabeledPolicy();

    [GeneratedRegex(@"\bPOL-[A-Z0-9][A-Z0-9-]{2,60}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 200)]
    private static partial Regex PolicyShape();

    [GeneratedRegex(@"(?:date of (?:the )?(?:incident|loss|accident)|incident date|happened on|occurred on)\s*[:\-]?\s*(?<v>\d{4}-\d{2}-\d{2}|\d{1,2}[/.-]\d{1,2}[/.-]\d{2,4}|\d{1,2}(?:st|nd|rd|th)?\s+[A-Za-z]{3,9}\.?(?:\s+\d{4})?|[A-Za-z]{3,9}\.?\s+\d{1,2}(?:st|nd|rd|th)?(?:,?\s+\d{4})?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 200)]
    private static partial Regex LabeledDate();

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b|\b\d{1,2}[/.-]\d{1,2}[/.-]\d{2,4}\b|\b\d{1,2}(?:st|nd|rd|th)?\s+(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\.?(?:\s+\d{4})?\b|\b(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\.?\s+\d{1,2}(?:st|nd|rd|th)?(?:,?\s+\d{4})?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 200)]
    private static partial Regex AnyDate();

    [GeneratedRegex(@"^(?<d>\d{1,2})[/.-](?<m>\d{1,2})[/.-](?<y>\d{2}|\d{4})$", RegexOptions.CultureInvariant, 200)]
    private static partial Regex NumericDate();

    [GeneratedRegex(@"^(?:(?<d1>\d{1,2})(?:st|nd|rd|th)?\s+(?<mon>[A-Za-z]{3,9})\.?(?:,?\s+(?<y>\d{4}))?|(?<mon>[A-Za-z]{3,9})\.?\s+(?<d2>\d{1,2})(?:st|nd|rd|th)?(?:,?\s+(?<y>\d{4}))?)$", RegexOptions.CultureInvariant, 200)]
    private static partial Regex NamedDate();

    [GeneratedRegex(@"\b(?:yesterday|last night|today|this morning|this evening|tonight|just now)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 200)]
    private static partial Regex Relative();

    [GeneratedRegex(@"^[ \t]*(?:location|place|where|address|incident location)[ \t]*[:\-][ \t]*(?<v>\S.{1,198})$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant, 200)]
    private static partial Regex LabeledLocation();

    [GeneratedRegex(@"\b(?:line of (?:business|cover)|line|cover type|claim type)[ \t]*[:\-][ \t]*(?<v>motor|property)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 200)]
    private static partial Regex LabeledLine();

    [GeneratedRegex(@"\b(?:market|country)[ \t]*[:\-][ \t]*(?<v>[A-Za-z][A-Za-z ]{1,20})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 200)]
    private static partial Regex LabeledMarket();

    [GeneratedRegex(@"^[ \t]*(?:claimant|insured|policyholder|policy holder|customer)\b[^\n]*@[^\n]*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant, 200)]
    private static partial Regex LabeledClaimant();

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,24}", RegexOptions.CultureInvariant, 200)]
    private static partial Regex EmailAddress();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 200)]
    private static partial Regex WhiteSpace();
}
