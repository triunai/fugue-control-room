using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Identifier of a <c>DataProtectionKey</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<DataProtectionKeyId>))]
[TypeConverter(typeof(GuidIdTypeConverter<DataProtectionKeyId>))]
public readonly record struct DataProtectionKeyId(Guid Value) : IStronglyTypedId<DataProtectionKeyId>, IParsable<DataProtectionKeyId>
{
    public static DataProtectionKeyId New() => new(Uuid7.NewGuid());

    public static DataProtectionKeyId From(Guid value) => new(value);

    public static DataProtectionKeyId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out DataProtectionKeyId id) ? id : throw new FormatException($"'{s}' is not a valid DataProtectionKeyId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out DataProtectionKeyId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new DataProtectionKeyId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}
