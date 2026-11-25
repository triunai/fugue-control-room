using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Identifier of a <c>Resource</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<ResourceId>))]
[TypeConverter(typeof(GuidIdTypeConverter<ResourceId>))]
public readonly record struct ResourceId(Guid Value) : IStronglyTypedId<ResourceId>, IParsable<ResourceId>
{
    public static ResourceId New() => new(Uuid7.NewGuid());

    public static ResourceId From(Guid value) => new(value);

    public static ResourceId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out ResourceId id) ? id : throw new FormatException($"'{s}' is not a valid ResourceId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out ResourceId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new ResourceId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}
