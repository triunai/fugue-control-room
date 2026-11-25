using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Identifier of a <c>AuditLog</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<AuditLogId>))]
[TypeConverter(typeof(GuidIdTypeConverter<AuditLogId>))]
public readonly record struct AuditLogId(Guid Value) : IStronglyTypedId<AuditLogId>, IParsable<AuditLogId>
{
    public static AuditLogId New() => new(Uuid7.NewGuid());

    public static AuditLogId From(Guid value) => new(value);

    public static AuditLogId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out AuditLogId id) ? id : throw new FormatException($"'{s}' is not a valid AuditLogId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out AuditLogId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new AuditLogId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}
