using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using FluentResults;
using Fugue.Application.Features.Import.Models;

namespace Fugue.Application.Features.Import.Helpers;

/// <summary>One email as the import reads it: the sender, subject, sent date, the plain-text body and how many attachments were left out.</summary>
public sealed record EmailMessage(
    string? FromName, string? FromAddress, string? Subject, DateTimeOffset? SentAt, string? MessageId, string Body, int Attachments, string RawKey);

/// <summary>
/// Reads .eml (one RFC 5322 message) and .mbox (many, split on the <c>From </c> separator line) exports of a shared inbox, without a package: headers
/// with folding and RFC 2047 encoded words, multipart bodies (the first text/plain part wins, else text/html with the tags stripped),
/// quoted-printable and base64 transfer encodings, and UTF-8 / Latin-1 / other charsets the runtime knows. Attachments are counted, never read.
/// </summary>
public static partial class EmailReader
{
    private const int MaxDepth = 8;

    public static Result<IReadOnlyList<EmailMessage>> Read(string fileName, byte[] bytes)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not (".eml" or ".mbox" or ".mbx"))
        {
            return ImportText.Unreadable<IReadOnlyList<EmailMessage>>("An inbox import takes an .eml or .mbox file.");
        }

        if (ImportText.LooksBinary(bytes))
        {
            return ImportText.Unreadable<IReadOnlyList<EmailMessage>>("This is not a text email export.");
        }

        string text = Encoding.Latin1.GetString(bytes).Replace("\r\n", "\n", StringComparison.Ordinal); // bytes as-is; charsets are applied per part
        List<string> raw = extension == ".eml" ? [text] : SplitMbox(text);
        if (raw.Count > ImportLimits.MaxMessages)
        {
            return ImportText.Unreadable<IReadOnlyList<EmailMessage>>($"An inbox import takes at most {ImportLimits.MaxMessages} messages.");
        }

        List<EmailMessage> messages = [];
        foreach (string message in raw)
        {
            if (ParseMessage(message) is { } parsed)
            {
                messages.Add(parsed);
            }
        }

        return messages.Count == 0
            ? ImportText.Unreadable<IReadOnlyList<EmailMessage>>(extension == ".eml"
                ? "No email headers were found (an .eml starts with header lines such as From: and Subject:)."
                : "No messages were found (an .mbox separates messages with lines starting \"From \").")
            : Result.Ok<IReadOnlyList<EmailMessage>>(messages);
    }

    /// <summary>mboxrd: a line starting <c>From </c> at the start of the file or after a blank line opens a message; <c>&gt;From </c> is unescaped.</summary>
    public static List<string> SplitMbox(string text)
    {
        List<string> messages = [];
        var current = new StringBuilder();
        bool open = false;
        bool previousBlank = true;
        foreach (string line in text.Split('\n'))
        {
            if (line.StartsWith("From ", StringComparison.Ordinal) && previousBlank)
            {
                if (open && current.Length > 0)
                {
                    messages.Add(current.ToString());
                }

                current.Clear();
                open = true;
            }
            else if (open)
            {
                current.Append(FromEscape().IsMatch(line) ? line[1..] : line).Append('\n');
            }

            previousBlank = line.Length == 0;
        }

        if (open && current.Length > 0)
        {
            messages.Add(current.ToString());
        }

        return messages;
    }

    private static EmailMessage? ParseMessage(string raw)
    {
        (Dictionary<string, string> headers, string body) = SplitHeaders(raw.TrimStart('\n'));
        if (!headers.ContainsKey("from") && !headers.ContainsKey("subject"))
        {
            return null;
        }

        (string? name, string? address) = ParseAddress(headers.GetValueOrDefault("from"));
        int attachments = 0;
        string text = BodyText(headers, body, 0, ref attachments);
        string? messageId = headers.GetValueOrDefault("message-id")?.Trim();
        string key = ImportText.Sha256(messageId is { Length: > 0 }
            ? "email\nmessage-id:" + messageId
            : "email\n" + raw.Trim());
        return new EmailMessage(
            name, address, headers.TryGetValue("subject", out string? subject) ? DecodeWords(subject).Trim() : null, ParseDate(headers.GetValueOrDefault("date")),
            messageId, CleanBody(text), attachments, key);
    }

    /// <summary>Headers (lower-cased names, folded lines joined) and the rest. The first occurrence of a header wins.</summary>
    private static (Dictionary<string, string> Headers, string Body) SplitHeaders(string part)
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        int end = part.IndexOf("\n\n", StringComparison.Ordinal);
        string head = end < 0 ? part : part[..end];
        string body = end < 0 ? string.Empty : part[(end + 2)..];
        string? name = null;
        var value = new StringBuilder();
        foreach (string line in head.Split('\n'))
        {
            if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t') && name is not null)
            {
                value.Append(' ').Append(line.Trim());
                continue;
            }

            if (name is not null)
            {
                headers.TryAdd(name, value.ToString());
            }

            int colon = line.IndexOf(':');
            if (colon <= 0 || line[..colon].Contains(' '))
            {
                name = null;
                continue;
            }

            name = line[..colon].Trim().ToLowerInvariant();
            value.Clear().Append(line[(colon + 1)..].Trim());
        }

        if (name is not null)
        {
            headers.TryAdd(name, value.ToString());
        }

        return (headers, body);
    }

    private static string BodyText(Dictionary<string, string> headers, string body, int depth, ref int attachments)
    {
        (string type, Dictionary<string, string> parameters) = ContentType(headers.GetValueOrDefault("content-type"));
        string disposition = headers.GetValueOrDefault("content-disposition") ?? string.Empty;
        if (type.StartsWith("multipart/", StringComparison.Ordinal) && parameters.TryGetValue("boundary", out string? boundary) && depth < MaxDepth)
        {
            string? plain = null;
            string? html = null;
            foreach (string part in SplitMultipart(body, boundary))
            {
                (Dictionary<string, string> partHeaders, string partBody) = SplitHeaders(part.TrimStart('\n'));
                (string partType, _) = ContentType(partHeaders.GetValueOrDefault("content-type"));
                if (IsAttachment(partHeaders.GetValueOrDefault("content-disposition"), partType))
                {
                    attachments++;
                    continue;
                }

                string text = BodyText(partHeaders, partBody, depth + 1, ref attachments);
                if (partType.StartsWith("text/html", StringComparison.Ordinal))
                {
                    html ??= text;
                }
                else if (text.Length > 0)
                {
                    plain ??= text;
                }
            }

            return plain ?? html ?? string.Empty;
        }

        if (IsAttachment(disposition, type))
        {
            attachments++;
            return string.Empty;
        }

        string decoded = Decode(body, headers.GetValueOrDefault("content-transfer-encoding"), parameters.GetValueOrDefault("charset"));
        return type == "text/html" ? StripHtml(decoded) : decoded;
    }

    private static bool IsAttachment(string? disposition, string type) =>
        (disposition ?? string.Empty).TrimStart().StartsWith("attachment", StringComparison.OrdinalIgnoreCase)
        || !(type.StartsWith("text/", StringComparison.Ordinal) || type.StartsWith("multipart/", StringComparison.Ordinal));

    private static IEnumerable<string> SplitMultipart(string body, string boundary)
    {
        string delimiter = "--" + boundary;
        var part = new StringBuilder();
        bool inside = false;
        foreach (string line in body.Split('\n'))
        {
            string trimmed = line.TrimEnd();
            if (trimmed == delimiter + "--")
            {
                if (inside)
                {
                    yield return part.ToString();
                }

                yield break;
            }

            if (trimmed == delimiter)
            {
                if (inside)
                {
                    yield return part.ToString();
                }

                part.Clear();
                inside = true;
                continue;
            }

            if (inside)
            {
                part.Append(line).Append('\n');
            }
        }

        if (inside && part.Length > 0)
        {
            yield return part.ToString();
        }
    }

    private static (string Type, Dictionary<string, string> Parameters) ContentType(string? header)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(header))
        {
            return ("text/plain", parameters);
        }

        string[] pieces = header.Split(';');
        foreach (string piece in pieces.Skip(1))
        {
            int eq = piece.IndexOf('=');
            if (eq > 0)
            {
                parameters[piece[..eq].Trim()] = piece[(eq + 1)..].Trim().Trim('"');
            }
        }

        return (pieces[0].Trim().ToLowerInvariant(), parameters);
    }

    /// <summary>Applies the transfer encoding to the part's bytes (the body string holds bytes as Latin-1 chars), then the charset.</summary>
    private static string Decode(string body, string? transferEncoding, string? charset)
    {
        byte[] bytes;
        switch ((transferEncoding ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "base64":
                try
                {
                    bytes = Convert.FromBase64String(new string(body.Where(c => !char.IsWhiteSpace(c)).ToArray()));
                }
                catch (FormatException)
                {
                    bytes = Encoding.Latin1.GetBytes(body);
                }

                break;
            case "quoted-printable":
                bytes = QuotedPrintable(body, false);
                break;
            default:
                bytes = Encoding.Latin1.GetBytes(body);
                break;
        }

        return CharsetOf(charset).GetString(bytes);
    }

    private static Encoding CharsetOf(string? charset)
    {
        string name = (charset ?? "utf-8").Trim().Trim('"').ToLowerInvariant();
        if (name is "us-ascii" or "ascii" or "utf-8" or "utf8" or "")
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(name);
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    private static byte[] QuotedPrintable(string text, bool underscoreIsSpace)
    {
        var bytes = new List<byte>(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '=' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                i++; // soft line break
            }
            else if (c == '=' && i + 2 < text.Length && Uri.IsHexDigit(text[i + 1]) && Uri.IsHexDigit(text[i + 2]))
            {
                bytes.Add(Convert.ToByte(text.Substring(i + 1, 2), 16));
                i += 2;
            }
            else if (c == '_' && underscoreIsSpace)
            {
                bytes.Add((byte)' ');
            }
            else
            {
                bytes.Add((byte)(c <= 0xFF ? c : '?'));
            }
        }

        return [.. bytes];
    }

    /// <summary>RFC 2047 encoded words (<c>=?utf-8?B?...?=</c>, <c>=?iso-8859-1?Q?...?=</c>) in a header value; whitespace between two words is dropped.</summary>
    public static string DecodeWords(string value)
    {
        string joined = BetweenWords().Replace(value, "$1$2");
        string decoded = EncodedWord().Replace(joined, m =>
        {
            Encoding encoding = CharsetOf(m.Groups["cs"].Value);
            string data = m.Groups["data"].Value;
            try
            {
                byte[] bytes = m.Groups["enc"].Value.Equals("B", StringComparison.OrdinalIgnoreCase)
                    ? Convert.FromBase64String(data)
                    : QuotedPrintable(data, true);
                return encoding.GetString(bytes);
            }
            catch (FormatException)
            {
                return m.Value;
            }
        });

        // A header with raw 8-bit bytes (no encoded words) was read as Latin-1; re-read it as UTF-8 when that is what it is.
        if (decoded.Any(c => c > 0x7F && c <= 0xFF))
        {
            byte[] raw = Encoding.Latin1.GetBytes(decoded);
            try
            {
                return new UTF8Encoding(false, true).GetString(raw);
            }
            catch (DecoderFallbackException)
            {
                return decoded;
            }
        }

        return decoded;
    }

    /// <summary><c>"Name" &lt;a@b&gt;</c>, <c>Name &lt;a@b&gt;</c>, <c>a@b (Name)</c> or a bare address.</summary>
    public static (string? Name, string? Address) ParseAddress(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return (null, null);
        }

        string value = DecodeWords(header).Trim();
        if (AngleAddress().Match(value) is { Success: true } angle)
        {
            string name = angle.Groups["name"].Value.Trim().Trim('"').Trim();
            return (name.Length == 0 ? null : name, angle.Groups["addr"].Value.Trim());
        }

        if (DraftHints.Email(value) is { } address)
        {
            string rest = value.Replace(address, string.Empty, StringComparison.Ordinal).Trim().Trim('(', ')', '"').Trim();
            return (rest.Length == 0 ? null : rest, address);
        }

        return (value, null);
    }

    private static DateTimeOffset? ParseDate(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        string value = Comment().Replace(header, string.Empty).Trim();
        value = value.Contains(',') ? value[(value.IndexOf(',') + 1)..].Trim() : value; // drop the day name
        string[] formats = ["d MMM yyyy HH:mm:ss zzz", "d MMM yyyy HH:mm zzz", "d MMM yyyy HH:mm:ss", "d MMM yy HH:mm:ss zzz"];
        string normalised = OffsetWithoutColon().Replace(value, "$1$2:$3");
        if (DateTimeOffset.TryParseExact(normalised, formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out DateTimeOffset at))
        {
            return at;
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset loose) ? loose : null;
    }

    /// <summary>The reply chain (quoted lines, "On ... wrote:"), a signature after "-- " and runs of blank lines are dropped.</summary>
    private static string CleanBody(string text)
    {
        var lines = new List<string>();
        foreach (string line in text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
        {
            if (line == "-- " || line.TrimEnd() == "--" || WroteLine().IsMatch(line) || line.StartsWith("-----Original Message", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (!line.TrimStart().StartsWith('>'))
            {
                lines.Add(line.TrimEnd());
            }
        }

        return BlankRuns().Replace(string.Join('\n', lines), "\n\n").Trim();
    }

    private static string StripHtml(string html)
    {
        string text = HtmlDrop().Replace(html, string.Empty);
        text = HtmlBreak().Replace(text, "\n");
        text = HtmlTag().Replace(text, string.Empty);
        return WebUtility.HtmlDecode(text);
    }

    [GeneratedRegex(@"^>+From ", RegexOptions.CultureInvariant)]
    private static partial Regex FromEscape();

    [GeneratedRegex(@"(\?=)\s+(=\?)", RegexOptions.CultureInvariant)]
    private static partial Regex BetweenWords();

    [GeneratedRegex(@"=\?(?<cs>[^?\s*]+)(?:\*[^?\s]*)?\?(?<enc>[BbQq])\?(?<data>[^?\s]*)\?=", RegexOptions.CultureInvariant)]
    private static partial Regex EncodedWord();

    [GeneratedRegex(@"^(?<name>.*)<(?<addr>[^<>]+@[^<>]+)>\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex AngleAddress();

    [GeneratedRegex(@"\([^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex Comment();

    [GeneratedRegex(@"([+-])(\d{2})(\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex OffsetWithoutColon();

    [GeneratedRegex(@"^On .{4,200} wrote:\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex WroteLine();

    [GeneratedRegex(@"\n{3,}", RegexOptions.CultureInvariant)]
    private static partial Regex BlankRuns();

    [GeneratedRegex(@"<(script|style|head)[^>]*>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlDrop();

    [GeneratedRegex(@"<\s*(br|/p|/div|/li|/tr|/h\d)\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlBreak();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTag();
}
