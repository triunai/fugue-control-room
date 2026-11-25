using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Identifier of a <c>Group</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<GroupId>))]
[TypeConverter(typeof(GuidIdTypeConverter<GroupId>))]
public readonly record struct GroupId(Guid Value) : IStronglyTypedId<GroupId>, IParsable<GroupId>
{
    public static GroupId New() => new(Uuid7.NewGuid());

    public static GroupId From(Guid value) => new(value);

    public static GroupId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out GroupId id) ? id : throw new FormatException($"'{s}' is not a valid GroupId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out GroupId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new GroupId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}
