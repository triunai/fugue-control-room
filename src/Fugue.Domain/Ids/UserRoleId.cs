using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Identifier of a <c>UserRole</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<UserRoleId>))]
[TypeConverter(typeof(GuidIdTypeConverter<UserRoleId>))]
public readonly record struct UserRoleId(Guid Value) : IStronglyTypedId<UserRoleId>, IParsable<UserRoleId>
{
    public static UserRoleId New() => new(Uuid7.NewGuid());

    public static UserRoleId From(Guid value) => new(value);

    public static UserRoleId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out UserRoleId id) ? id : throw new FormatException($"'{s}' is not a valid UserRoleId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out UserRoleId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new UserRoleId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}
