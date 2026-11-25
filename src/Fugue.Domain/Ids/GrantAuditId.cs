using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Identifier of a <c>GrantAudit</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<GrantAuditId>))]
[TypeConverter(typeof(GuidIdTypeConverter<GrantAuditId>))]
public readonly record struct GrantAuditId(Guid Value) : IStronglyTypedId<GrantAuditId>, IParsable<GrantAuditId>
{
    public static GrantAuditId New() => new(Uuid7.NewGuid());

    public static GrantAuditId From(Guid value) => new(value);

    public static GrantAuditId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out GrantAuditId id) ? id : throw new FormatException($"'{s}' is not a valid GrantAuditId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out GrantAuditId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new GrantAuditId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}
