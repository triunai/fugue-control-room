using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Identifier of a <c>User</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<UserId>))]
[TypeConverter(typeof(GuidIdTypeConverter<UserId>))]
public readonly record struct UserId(Guid Value) : IStronglyTypedId<UserId>, IParsable<UserId>
{
    public static UserId New() => new(Uuid7.NewGuid());

    public static UserId From(Guid value) => new(value);

    public static UserId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out UserId id) ? id : throw new FormatException($"'{s}' is not a valid UserId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out UserId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new UserId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}
