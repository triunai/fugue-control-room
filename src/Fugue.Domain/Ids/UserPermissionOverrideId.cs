using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Identifier of a <c>UserPermissionOverride</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<UserPermissionOverrideId>))]
[TypeConverter(typeof(GuidIdTypeConverter<UserPermissionOverrideId>))]
public readonly record struct UserPermissionOverrideId(Guid Value) : IStronglyTypedId<UserPermissionOverrideId>, IParsable<UserPermissionOverrideId>
{
    public static UserPermissionOverrideId New() => new(Uuid7.NewGuid());

    public static UserPermissionOverrideId From(Guid value) => new(value);

    public static UserPermissionOverrideId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out UserPermissionOverrideId id) ? id : throw new FormatException($"'{s}' is not a valid UserPermissionOverrideId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out UserPermissionOverrideId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new UserPermissionOverrideId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}
