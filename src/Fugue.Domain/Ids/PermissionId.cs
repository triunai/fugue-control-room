using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Identifier of a <c>Permission</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<PermissionId>))]
[TypeConverter(typeof(GuidIdTypeConverter<PermissionId>))]
public readonly record struct PermissionId(Guid Value) : IStronglyTypedId<PermissionId>, IParsable<PermissionId>
{
    public static PermissionId New() => new(Uuid7.NewGuid());

    public static PermissionId From(Guid value) => new(value);

    public static PermissionId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out PermissionId id) ? id : throw new FormatException($"'{s}' is not a valid PermissionId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out PermissionId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new PermissionId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}
