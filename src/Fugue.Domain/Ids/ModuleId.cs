using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Identifier of a <c>Module</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<ModuleId>))]
[TypeConverter(typeof(GuidIdTypeConverter<ModuleId>))]
public readonly record struct ModuleId(Guid Value) : IStronglyTypedId<ModuleId>, IParsable<ModuleId>
{
    public static ModuleId New() => new(Uuid7.NewGuid());

    public static ModuleId From(Guid value) => new(value);

    public static ModuleId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out ModuleId id) ? id : throw new FormatException($"'{s}' is not a valid ModuleId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out ModuleId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new ModuleId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}
