using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Identifier of a <c>Role</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<RoleId>))]
[TypeConverter(typeof(GuidIdTypeConverter<RoleId>))]
public readonly record struct RoleId(Guid Value) : IStronglyTypedId<RoleId>, IParsable<RoleId>
{
    public static RoleId New() => new(Uuid7.NewGuid());

    public static RoleId From(Guid value) => new(value);

    public static RoleId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out RoleId id) ? id : throw new FormatException($"'{s}' is not a valid RoleId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out RoleId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new RoleId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}
