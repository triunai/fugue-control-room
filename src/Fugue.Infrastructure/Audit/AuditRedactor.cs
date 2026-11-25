using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fugue.Application.Abstractions;
using Fugue.Domain.Audit;
using Microsoft.Extensions.Options;

namespace Fugue.Infrastructure.Audit;

/// <summary>
/// Turns raw field changes into the stored <c>changes</c> document: omits <c>[NoAudit]</c> fields, masks secrets, partially masks
/// personal data, caps long strings and bounds the document size (ADR-010). Everything stored has passed through here.
/// </summary>
internal sealed class AuditRedactor(IOptions<AuditOptions> options)
{
    public const string Mask = "***";
    private const int MaxValueChars = 1024;
    private const int MarkerReserveBytes = 48;

    /// <summary>Stamp columns and the concurrency token never describe a business change.</summary>
    public static readonly IReadOnlySet<string> NoiseFields =
        new HashSet<string>(["CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy", "RowVersion"], StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    private readonly HashSet<string> _masked = new(AuditOptions.BuiltInMasked.Concat(options.Value.MaskedProperties), StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _partial = new(
        (options.Value.PartialMaskPersonalData ? AuditOptions.BuiltInPartiallyMasked : []).Concat(options.Value.PartialMaskedProperties),
        StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _excluded = new(AuditOptions.BuiltInExcludedEntities.Concat(options.Value.ExcludedEntities), StringComparer.Ordinal);

    public bool IsExcluded(Type entity) =>
        _excluded.Contains(entity.Name) || Attribute.IsDefined(entity, typeof(NoAuditAttribute));

    /// <summary>The stored JSON, or null when there is nothing to store.</summary>
    public string? Build(Type? entity, IEnumerable<KeyValuePair<string, AuditFieldChange>> fields)
    {
        var parts = new List<(string Key, byte[] Value)>();
        bool truncated = false;
        foreach ((string name, AuditFieldChange change) in fields)
        {
            PropertyInfo? property = entity?.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is not null && Attribute.IsDefined(property, typeof(NoAuditAttribute)))
            {
                continue;
            }

            var value = new Dictionary<string, object?>(2);
            if (change.HasOld)
            {
                value["old"] = Redact(name, property, change.Old, ref truncated);
            }

            if (change.HasNew)
            {
                value["new"] = Redact(name, property, change.New, ref truncated);
            }

            parts.Add((CamelCase(name), JsonSerializer.SerializeToUtf8Bytes(value, Json)));
        }

        if (parts.Count == 0)
        {
            return null;
        }

        return Assemble(parts, truncated, Math.Max(options.Value.MaxChangesBytes, MarkerReserveBytes * 2));
    }

    /// <summary>
    /// The same masking rules for a value that is not an audit change (an outbox payload field): false when the property is <c>[NoAudit]</c> and
    /// must be left out, otherwise <paramref name="redacted"/> is masked, partially masked or capped like an audit value.
    /// </summary>
    public bool TryRedact(Type entity, string name, object? value, out object? redacted)
    {
        PropertyInfo? property = entity.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        redacted = null;
        if (property is not null && Attribute.IsDefined(property, typeof(NoAuditAttribute)))
        {
            return false;
        }

        bool truncated = false;
        redacted = Redact(name, property, value, ref truncated);
        return true;
    }

    private object? Redact(string name, PropertyInfo? property, object? value, ref bool truncated)
    {
        if (value is null)
        {
            return null;
        }

        if (_masked.Contains(name) || (property is not null && Attribute.IsDefined(property, typeof(AuditMaskAttribute))))
        {
            return Mask;
        }

        if (value is string text)
        {
            if (_partial.Contains(name))
            {
                return PartialMask(text);
            }

            if (text.Length > MaxValueChars)
            {
                truncated = true;
                return text[..MaxValueChars];
            }
        }

        return value;
    }

    /// <summary>An email keeps its first character and domain (<c>j***@example.test</c>); anything else keeps its last two characters (<c>***42</c>).</summary>
    public static string PartialMask(string value)
    {
        int at = value.IndexOf('@', StringComparison.Ordinal);
        if (at > 0)
        {
            return $"{value[0]}{Mask}{value[at..]}";
        }

        return value.Length >= 6 ? Mask + value[^2..] : Mask;
    }

    private static string Assemble(List<(string Key, byte[] Value)> parts, bool truncated, int budget)
    {
        // Each field costs its value plus "key": and separators; when the whole document does not fit, keep fields in order while
        // there is still room for the truncation markers and drop the rest.
        static int Cost((string Key, byte[] Value) part) => Encoding.UTF8.GetByteCount(part.Key) + part.Value.Length + 5;

        bool fits = 2 + parts.Sum(Cost) <= budget;
        var kept = new List<(string Key, byte[] Value)>();
        int size = 2;
        foreach ((string Key, byte[] Value) part in parts)
        {
            if (!fits && size + Cost(part) + MarkerReserveBytes > budget)
            {
                truncated = true;
                continue;
            }

            kept.Add(part);
            size += Cost(part);
        }

        var builder = new StringBuilder("{");
        foreach ((string key, byte[] value) in kept)
        {
            if (builder.Length > 1)
            {
                builder.Append(',');
            }

            builder.Append(JsonSerializer.Serialize(key)).Append(':').Append(Encoding.UTF8.GetString(value));
        }

        if (truncated)
        {
            builder.Append(builder.Length > 1 ? "," : string.Empty).Append("\"_truncated\":true,\"_omittedFields\":").Append(parts.Count - kept.Count);
        }

        return builder.Append('}').ToString();
    }

    private static string CamelCase(string name) => name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
