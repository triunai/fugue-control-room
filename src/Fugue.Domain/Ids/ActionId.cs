using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Identifier of a <c>Action</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<ActionId>))]
[TypeConverter(typeof(GuidIdTypeConverter<ActionId>))]
public readonly record struct ActionId(Guid Value) : IStronglyTypedId<ActionId>, IParsable<ActionId>
{
    public static ActionId New() => new(Uuid7.NewGuid());

    public static ActionId From(Guid value) => new(value);

    public static ActionId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out ActionId id) ? id : throw new FormatException($"'{s}' is not a valid ActionId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out ActionId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new ActionId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}
