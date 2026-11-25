using System.Globalization;
using System.Text;

namespace Fugue.Application.Common;

/// <summary>
/// Opaque keyset-pagination cursor over <c>(created_at, id)</c>: base64url of <c>{utcTicks}.{guid}</c>. Clients treat it
/// as a token; the format may change without notice.
/// </summary>
public readonly record struct KeysetCursor(DateTimeOffset CreatedAt, Guid Id)
{
    public string Encode() =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(
                string.Create(CultureInfo.InvariantCulture, $"{CreatedAt.UtcTicks}.{Id}")))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static bool TryDecode(string? cursor, out KeysetCursor result)
    {
        result = default;
        if (string.IsNullOrEmpty(cursor) || cursor.Length > 200)
        {
            return false;
        }

        try
        {
            string padded = cursor.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
            string text = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(padded));
            int dot = text.IndexOf('.', StringComparison.Ordinal);
            if (dot > 0
                && long.TryParse(text.AsSpan(0, dot), NumberStyles.None, CultureInfo.InvariantCulture, out long ticks)
                && ticks is >= 0 && ticks <= DateTime.MaxValue.Ticks
                && Guid.TryParse(text.AsSpan(dot + 1), out Guid id))
            {
                result = new KeysetCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
                return true;
            }
        }
        catch (FormatException)
        {
            // not base64url: fall through to false
        }
        catch (ArgumentException)
        {
            // invalid UTF-8
        }

        return false;
    }
}
