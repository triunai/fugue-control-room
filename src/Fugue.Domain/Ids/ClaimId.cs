using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Identifier of a <c>Claim</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<ClaimId>))]
[TypeConverter(typeof(GuidIdTypeConverter<ClaimId>))]
public readonly record struct ClaimId(Guid Value) : IStronglyTypedId<ClaimId>, IParsable<ClaimId>
{
    public static ClaimId New() => new(Uuid7.NewGuid());

    public static ClaimId From(Guid value) => new(value);

    public static ClaimId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out ClaimId id) ? id : throw new FormatException($"'{s}' is not a valid ClaimId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out ClaimId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new ClaimId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}
