using Microsoft.Extensions.Options;

namespace Fugue.Infrastructure.Messaging;

/// <summary>Which <c>IEventPublisher</c> adapters the relay calls (ADR-011 amendment). <see cref="Webhooks"/> is the default and the only one before Kafka existed.</summary>
public enum EventPublisherMode
{
    Webhooks,
    Kafka,
    Both,
}

/// <summary><c>Events:</c> configuration. With <c>Events:Publisher</c> unset the app behaves exactly as before Kafka.</summary>
public sealed class EventPublisherOptions
{
    public const string Section = "Events";

    public EventPublisherMode Publisher { get; set; } = EventPublisherMode.Webhooks;

    public KafkaOptions Kafka { get; set; } = new();
}

public sealed class KafkaOptions
{
    /// <summary>Comma separated <c>host:port</c> list. Required when the publisher mode includes Kafka.</summary>
    public string BootstrapServers { get; set; } = string.Empty;

    /// <summary>
    /// How long one produce may take before it fails (delivery report with an error), so the relay retries with backoff instead of waiting on a dead broker.
    /// The relay holds its database transaction meanwhile, so keep it short.
    /// </summary>
    public int MessageTimeoutMs { get; set; } = 10000;

    /// <summary>Topic for an event family whose first event-type segment (<c>claim</c> in <c>claim.opened.v1</c>) has no entry in <see cref="KafkaPublisherTopics"/>.</summary>
    public string DefaultTopic { get; set; } = "fugue.events.v1";
}

internal sealed class EventPublisherOptionsValidator : IValidateOptions<EventPublisherOptions>
{
    public ValidateOptionsResult Validate(string? name, EventPublisherOptions options)
    {
        bool kafka = options.Publisher is EventPublisherMode.Kafka or EventPublisherMode.Both;
        if (kafka && string.IsNullOrWhiteSpace(options.Kafka.BootstrapServers))
        {
            return ValidateOptionsResult.Fail("Events:Kafka:BootstrapServers is required when Events:Publisher is Kafka or Both.");
        }

        return options.Kafka.MessageTimeoutMs < 1000
            ? ValidateOptionsResult.Fail("Events:Kafka:MessageTimeoutMs must be at least 1000.")
            : ValidateOptionsResult.Success;
    }
}
