using Fugue.Application.Features.Webhooks;
using Fugue.Application.Features.Webhooks.Sink;
using Fugue.Domain.Webhooks;

namespace Fugue.Infrastructure.Persistence.Seeding;

/// <summary>Development only (registered with the other seeders): subscribes the in-process sink to <c>claim.*</c>. Goes straight to the store, so the SSRF guard is not consulted for the local URL.</summary>
internal sealed class DevSinkSeeder(IWebhookSubscriptionStore subscriptions, ISecretProtector protector, IDevWebhookSink sink) : IDevSinkSeeder
{
    internal const string SubscriptionName = "dev sink (claim events)";

    public async Task<bool> SeedAsync(string sinkUrl, CancellationToken ct)
    {
        IReadOnlyList<WebhookSubscriptionDto> existing = await subscriptions.ListAsync(null, 200, ct);
        WebhookSubscriptionDto? found = existing.FirstOrDefault(s => s.Name == SubscriptionName);
        if (found is null)
        {
            string secret = WebhookSecretFormat.Generate();
            await subscriptions.AddAsync(
                new WebhookSubscription
                {
                    Id = Fugue.Domain.Ids.WebhookSubscriptionId.New(),
                    Name = SubscriptionName,
                    Url = sinkUrl,
                    EventTypes = ["claim.*"],
                    IsActive = true,
                    SecretCiphertext = protector.Protect(secret),
                },
                ct);
            sink.Secret = secret;
            return true;
        }

        WebhookSubscription tracked = await subscriptions.FindForUpdateAsync(found.Id, ct)
            ?? throw new InvalidOperationException("The dev sink subscription disappeared while seeding.");
        if (tracked.Url != sinkUrl || !tracked.IsActive)
        {
            tracked.Url = sinkUrl;
            tracked.IsActive = true;
            await subscriptions.SaveAsync(tracked, null, ct);
        }

        sink.Secret = protector.Unprotect(tracked.SecretCiphertext);
        return false;
    }
}
