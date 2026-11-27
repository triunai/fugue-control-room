using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Identifier of a <c>ClaimInfoRequest</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<ClaimInfoRequestId>))]
[TypeConverter(typeof(GuidIdTypeConverter<ClaimInfoRequestId>))]
public readonly record struct ClaimInfoRequestId(Guid Value) : IStronglyTypedId<ClaimInfoRequestId>, IParsable<ClaimInfoRequestId>
{
    public static ClaimInfoRequestId New() => new(Uuid7.NewGuid());

    public static ClaimInfoRequestId From(Guid value) => new(value);

    public static ClaimInfoRequestId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out ClaimInfoRequestId id) ? id : throw new FormatException($"'{s}' is not a valid ClaimInfoRequestId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out ClaimInfoRequestId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new ClaimInfoRequestId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}
