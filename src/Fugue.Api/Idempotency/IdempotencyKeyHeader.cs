using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;

namespace Fugue.Api.Idempotency;

/// <summary>
/// Parses the <c>Idempotency-Key</c> request header (ADR-013, draft-ietf-httpapi-idempotency-key-header-09 section 2.1): ONE Structured Field String
/// (<c>"abc"</c>, with <c>\"</c> and <c>\\</c> escapes), decoded length 1 to 255, printable ASCII only. A bare token <c>[A-Za-z0-9._:-]{1,255}</c> is also
/// accepted for curl convenience (a documented deviation). Two header lines, a comma list, parameters, bad quoting or anything else is invalid.
/// A missing header means "no key"; a present but blank one is invalid: the request runs exactly as it would without idempotency.
/// </summary>
public static partial class IdempotencyKeyHeader
{
    public const string Name = "Idempotency-Key";

    public const int MaxLength = 255;

    [GeneratedRegex("^[A-Za-z0-9._:-]{1,255}$", RegexOptions.CultureInvariant)]
    private static partial Regex BareToken();

    /// <summary>Absent: no key. Valid: the decoded key. Invalid: the caller answers 400 <c>idempotency.key_invalid</c>.</summary>
    public static HeaderParse Parse(StringValues header)
    {
        if (header.Count == 0)
        {
            return HeaderParse.Absent;
        }

        if (header.Count > 1)
        {
            return HeaderParse.Invalid;
        }

        string raw = header[0]?.Trim() ?? string.Empty;
        if (raw.Length == 0)
        {
            return HeaderParse.Invalid; // present but blank: a supplied key must be valid, only an ABSENT header bypasses
        }

        if (raw[0] != '"')
        {
            return BareToken().IsMatch(raw) ? HeaderParse.Valid(raw) : HeaderParse.Invalid;
        }

        var decoded = new StringBuilder();
        for (int i = 1; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c == '\\')
            {
                if (i + 1 >= raw.Length || raw[i + 1] is not ('"' or '\\'))
                {
                    return HeaderParse.Invalid;
                }

                decoded.Append(raw[++i]);
            }
            else if (c == '"')
            {
                // The closing quote must be the last character: anything after it (a list, a parameter) is not a single string.
                bool valid = i == raw.Length - 1 && decoded.Length is >= 1 and <= MaxLength;
                return valid ? HeaderParse.Valid(decoded.ToString()) : HeaderParse.Invalid;
            }
            else if (c is < ' ' or > '~')
            {
                return HeaderParse.Invalid;
            }
            else
            {
                decoded.Append(c);
            }
        }

        return HeaderParse.Invalid; // no closing quote
    }
}

/// <summary>The outcome of <see cref="IdempotencyKeyHeader.Parse"/>.</summary>
public readonly record struct HeaderParse(bool IsPresent, bool IsValid, string? Key)
{
    public static HeaderParse Absent => new(false, true, null);

    public static HeaderParse Invalid => new(true, false, null);

    public static HeaderParse Valid(string key) => new(true, true, key);
}
