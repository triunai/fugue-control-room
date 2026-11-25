using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Identifier of a <c>Tenant</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<TenantId>))]
[TypeConverter(typeof(GuidIdTypeConverter<TenantId>))]
public readonly record struct TenantId(Guid Value) : IStronglyTypedId<TenantId>, IParsable<TenantId>
{
    public static TenantId New() => new(Uuid7.NewGuid());

    public static TenantId From(Guid value) => new(value);

    public static TenantId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out TenantId id) ? id : throw new FormatException($"'{s}' is not a valid TenantId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out TenantId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new TenantId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}
