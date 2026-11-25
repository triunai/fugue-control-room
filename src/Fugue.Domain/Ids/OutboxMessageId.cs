using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Identifier of a <c>OutboxMessage</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<OutboxMessageId>))]
[TypeConverter(typeof(GuidIdTypeConverter<OutboxMessageId>))]
public readonly record struct OutboxMessageId(Guid Value) : IStronglyTypedId<OutboxMessageId>, IParsable<OutboxMessageId>
{
    public static OutboxMessageId New() => new(Uuid7.NewGuid());

    public static OutboxMessageId From(Guid value) => new(value);

    public static OutboxMessageId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out OutboxMessageId id) ? id : throw new FormatException($"'{s}' is not a valid OutboxMessageId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out OutboxMessageId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new OutboxMessageId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}
