using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Identifier of a <c>WebhookDelivery</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<WebhookDeliveryId>))]
[TypeConverter(typeof(GuidIdTypeConverter<WebhookDeliveryId>))]
public readonly record struct WebhookDeliveryId(Guid Value) : IStronglyTypedId<WebhookDeliveryId>, IParsable<WebhookDeliveryId>
{
    public static WebhookDeliveryId New() => new(Uuid7.NewGuid());

    public static WebhookDeliveryId From(Guid value) => new(value);

    public static WebhookDeliveryId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out WebhookDeliveryId id) ? id : throw new FormatException($"'{s}' is not a valid WebhookDeliveryId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out WebhookDeliveryId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new WebhookDeliveryId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}
