using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Identifier of a <c>AccessAudit</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<AccessAuditId>))]
[TypeConverter(typeof(GuidIdTypeConverter<AccessAuditId>))]
public readonly record struct AccessAuditId(Guid Value) : IStronglyTypedId<AccessAuditId>, IParsable<AccessAuditId>
{
    public static AccessAuditId New() => new(Uuid7.NewGuid());

    public static AccessAuditId From(Guid value) => new(value);

    public static AccessAuditId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out AccessAuditId id) ? id : throw new FormatException($"'{s}' is not a valid AccessAuditId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out AccessAuditId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new AccessAuditId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}
