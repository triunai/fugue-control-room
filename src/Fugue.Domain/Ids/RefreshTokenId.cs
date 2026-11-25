using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Identifier of a <c>RefreshToken</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<RefreshTokenId>))]
[TypeConverter(typeof(GuidIdTypeConverter<RefreshTokenId>))]
public readonly record struct RefreshTokenId(Guid Value) : IStronglyTypedId<RefreshTokenId>, IParsable<RefreshTokenId>
{
    public static RefreshTokenId New() => new(Uuid7.NewGuid());

    public static RefreshTokenId From(Guid value) => new(value);

    public static RefreshTokenId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out RefreshTokenId id) ? id : throw new FormatException($"'{s}' is not a valid RefreshTokenId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out RefreshTokenId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new RefreshTokenId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}
