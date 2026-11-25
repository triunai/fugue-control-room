using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Fugue.Domain.Ids;

/// <summary>Identifier of a <c>WebhookSubscription</c>. A UUIDv7, so ids sort by creation time.</summary>
[JsonConverter(typeof(GuidIdJsonConverter<WebhookSubscriptionId>))]
[TypeConverter(typeof(GuidIdTypeConverter<WebhookSubscriptionId>))]
public readonly record struct WebhookSubscriptionId(Guid Value) : IStronglyTypedId<WebhookSubscriptionId>, IParsable<WebhookSubscriptionId>
{
    public static WebhookSubscriptionId New() => new(Uuid7.NewGuid());

    public static WebhookSubscriptionId From(Guid value) => new(value);

    public static WebhookSubscriptionId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out WebhookSubscriptionId id) ? id : throw new FormatException($"'{s}' is not a valid WebhookSubscriptionId.");

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out WebhookSubscriptionId result)
    {
        if (Guid.TryParse(s, out Guid guid) && guid != Guid.Empty)
        {
            result = new WebhookSubscriptionId(guid);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();
}
