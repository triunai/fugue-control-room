using System.Globalization;
using FluentValidation;
using FluentValidation.Results;
using Fugue.Application.Features.Claims.Commands;
using Fugue.Application.Features.Import.Parsing;
using Fugue.Domain.Claims;

namespace Fugue.Application.Features.Import;

/// <summary>
/// The one place an import is checked against Report an incident: the raw text of a draft is normalised (market code or country name, line,
/// yyyy-mm-dd or day-first date) and the result is run through the SAME <see cref="SubmitClaimValidator"/> the submit endpoint uses, unchanged.
/// </summary>
internal static class ImportRules
{
    private static readonly (string Name, Market Code)[] MarketNames =
        [("singapore", Market.SG), ("malaysia", Market.MY), ("thailand", Market.TH), ("hong kong", Market.HK), ("hongkong", Market.HK), ("indonesia", Market.ID), ("philippines", Market.PH)];

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "d/M/yyyy", "d-M-yyyy", "d.M.yyyy", "d/M/yy", "d MMM yyyy", "d MMMM yyyy", "yyyy/M/d"];

    public static Market? ParseMarket(string? text)
    {
        string value = (text ?? string.Empty).Trim();
        if (value.Length == 2 && Enum.TryParse(value, true, out Market code) && Enum.IsDefined(code))
        {
            return code;
        }

        string lower = value.ToLowerInvariant();
        return MarketNames.Where(n => n.Name == lower).Select(n => (Market?)n.Code).FirstOrDefault();
    }

    public static LineOfBusiness? ParseLine(string? text) =>
        Enum.TryParse((text ?? string.Empty).Trim(), true, out LineOfBusiness line) && Enum.IsDefined(line) && !int.TryParse(text, out _) ? line : null;

    public static DateOnly? ParseDate(string? text) =>
        DateOnly.TryParseExact((text ?? string.Empty).Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date) ? date : null;

    /// <summary>
    /// The field errors of a draft, keyed camelCase like the validation pipeline: first the values that do not parse, then every Report an incident
    /// rule on the parsed command (a field that did not parse is not judged again on its placeholder value).
    /// </summary>
    public static Dictionary<string, string[]> Errors(
        IValidator<SubmitClaimCommand> submit, ImportDraftInput draft, out Market? market, out LineOfBusiness? line, out DateOnly? date)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        market = ParseMarket(draft.Market);
        line = ParseLine(draft.Line);
        date = ParseDate(draft.IncidentDate);
        if (market is null)
        {
            Add(errors, "market", string.IsNullOrWhiteSpace(draft.Market) ? "Choose a market." : $"Not a market: \"{Short(draft.Market)}\". Use SG, MY, TH, HK, ID or PH.");
        }

        if (line is null)
        {
            Add(errors, "line", string.IsNullOrWhiteSpace(draft.Line) ? "Choose a line of cover." : $"Not a line of cover: \"{Short(draft.Line)}\". Use Motor or Property.");
        }

        if (date is null)
        {
            Add(errors, "incidentDate", string.IsNullOrWhiteSpace(draft.IncidentDate) ? "Give the incident date." : $"Not a date: \"{Short(draft.IncidentDate)}\". Use yyyy-mm-dd or dd/mm/yyyy.");
        }

        if (string.IsNullOrWhiteSpace(draft.ClaimantEmail))
        {
            Add(errors, "claimantEmail", "Give the email of the claimant's account.");
        }

        ValidationResult result = submit.Validate(ToSubmit(draft, market ?? Market.SG, line ?? LineOfBusiness.Motor, date ?? DateOnly.MinValue));
        foreach (ValidationFailure failure in result.Errors)
        {
            string field = Camel(failure.PropertyName);
            if (!errors.TryGetValue(field, out List<string>? existing))
            {
                Add(errors, field, failure.ErrorMessage);
            }
            else if (field is not ("market" or "line" or "incidentDate"))
            {
                existing.Add(failure.ErrorMessage);
            }
        }

        return errors.ToDictionary(e => e.Key, e => e.Value.Distinct().ToArray(), StringComparer.Ordinal);
    }

    public static SubmitClaimCommand ToSubmit(ImportDraftInput draft, Market market, LineOfBusiness line, DateOnly date) =>
        new(market, line, (draft.PolicyNumber ?? string.Empty).Trim(), date, (draft.IncidentDescription ?? string.Empty).Trim(), Blank(draft.IncidentLocation));

    /// <summary>The timeline note an imported claim carries: who keyed it in from which file, then the import's own note (transcript, email header).</summary>
    public static string ProvenanceNote(ImportSource source, string fileName, string item, string? note)
    {
        string kind = source switch
        {
            ImportSource.Spreadsheet => "a spreadsheet file import",
            ImportSource.Email => "an inbox export file import (standing in for a Microsoft Graph or Gmail connector)",
            _ => "a WhatsApp chat export file import (standing in for a WhatsApp Business connector)",
        };
        string head = $"Imported by the claims team from {kind}: {fileName}, {item}.";
        return string.IsNullOrWhiteSpace(note) ? head : ImportText.Cap(head + "\n" + note.Trim(), 4100);
    }

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string Short(string? text) => ImportText.Cap((text ?? string.Empty).Trim(), 40);

    private static void Add(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out List<string>? list))
        {
            errors[field] = list = [];
        }

        list.Add(message);
    }

    private static string Camel(string name) => name.Length > 0 && char.IsUpper(name[0]) ? char.ToLowerInvariant(name[0]) + name[1..] : name;
}
