using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Identifier of a <c>Customer</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<CustomerId>))]
[TypeConverter(typeof(GuidIdTypeConverter<CustomerId>))]
public readonly record struct CustomerId(Guid Value) : IStronglyTypedId<CustomerId>, IParsable<CustomerId>
{
    public static CustomerId New() => new(Uuid7.NewGuid());

    public static CustomerId From(Guid value) => new(value);

    public static CustomerId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out CustomerId id) ? id : throw new FormatException($"'{s}' is not a valid CustomerId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out CustomerId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new CustomerId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}
