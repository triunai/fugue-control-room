using Fugue.Domain.Abstractions;
using Fugue.Domain.Audit;
using Fugue.Domain.Ids;
using Fugue.Domain.Results;

namespace Fugue.Domain.Webhooks;

/// <summary>
/// A receiver of signed outbound webhooks. The signing secret is stored only as Data Protection ciphertext and is shown to the caller
/// once, on create and on rotation (ADR-011). <see cref="SecretCiphertext"/> is <c>[NoAudit]</c>: it never reaches <c>audit_log</c>.
/// </summary>
public sealed class WebhookSubscription : IAuditable, IVersioned
{
    public WebhookSubscriptionId Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    /// <summary>Event type filters: exact (<c>customer.created.v1</c>), prefix wildcard (<c>customer.*</c>) or <c>*</c>. See <see cref="EventTypeFilter"/>.</summary>
    public string[] EventTypes { get; set; } = [];

    public bool IsActive { get; set; } = true;

    [NoAudit]
    public string SecretCiphertext { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;

    /// <summary>Postgres <c>xmin</c>; the optimistic-concurrency token.</summary>
    public uint RowVersion { get; private set; }

    long IVersioned.Version => RowVersion;
}
