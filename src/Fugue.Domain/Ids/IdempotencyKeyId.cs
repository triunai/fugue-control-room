using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Identifier of an <c>idempotency_keys</c> row. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<IdempotencyKeyId>))]
[TypeConverter(typeof(GuidIdTypeConverter<IdempotencyKeyId>))]
public readonly record struct IdempotencyKeyId(Guid Value) : IStronglyTypedId<IdempotencyKeyId>, IParsable<IdempotencyKeyId>
{
    public static IdempotencyKeyId New() => new(Uuid7.NewGuid());

    public static IdempotencyKeyId From(Guid value) => new(value);

    public static IdempotencyKeyId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out IdempotencyKeyId id) ? id : throw new FormatException($"'{s}' is not a valid IdempotencyKeyId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out IdempotencyKeyId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new IdempotencyKeyId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}
