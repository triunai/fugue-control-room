using Fugue.Api.Auth;
using Fugue.Api.Contracts;
using Fugue.Api.Modules;
using Fugue.Api.OpenApi;
using Fugue.Application.Features.Webhooks;
using Fugue.Application.Features.Webhooks.Commands;
using Fugue.Application.Features.Webhooks.Queries;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Controllers;

/// <summary>
/// Outbound webhook subscriptions (ADR-011): CRUD plus secret rotation. The secret appears only in the create and rotate responses, never again.
/// One-liners only; every action needs <c>WEBHOOKS_MANAGE</c> (admin by default).
/// </summary>
[Module(ModuleCodes.Webhooks)]
public sealed class WebhooksController : ApiControllerBase
{
    /// <summary>Create a subscription. The signing secret is in this response and never shown again. The URL must be https and public (400 `webhook.url_rejected`).</summary>
    [HttpPost("subscriptions")]
    [HasPermission(Permissions.WebhooksManage)]
    [ProducesResponseType(typeof(WebhookSubscriptionSecretDto), StatusCodes.Status201Created)]
    public Task<ActionResult<WebhookSubscriptionSecretDto>> Create(CreateWebhookSubscriptionRequest body, CancellationToken ct) =>
        SendCreated(new CreateWebhookSubscriptionCommand(body.Name, body.Url, body.EventTypes), nameof(Get), s => new { id = s.Id }, ct);

    /// <summary>Get one subscription (no secret).</summary>
    [HttpGet("subscriptions/{id}")]
    [HasPermission(Permissions.WebhooksManage)]
    [ProducesResponseType(typeof(WebhookSubscriptionDto), StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound, "webhook.subscription_not_found")]
    public Task<ActionResult<WebhookSubscriptionDto>> Get(WebhookSubscriptionId id, CancellationToken ct) =>
        Send(new GetWebhookSubscriptionQuery(id), ct);

    /// <summary>List subscriptions, keyset paged.</summary>
    [HttpGet("subscriptions")]
    [HasPermission(Permissions.WebhooksManage)]
    [ProducesResponseType(typeof(IReadOnlyList<WebhookSubscriptionDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<WebhookSubscriptionDto>>> List([FromQuery] int? limit, [FromQuery] string? cursor, CancellationToken ct) =>
        SendPaged(new ListWebhookSubscriptionsQuery(limit, cursor), ct);

    /// <summary>Replace a subscription's name, URL, event filters and active flag.</summary>
    [HttpPut("subscriptions/{id}")]
    [HasPermission(Permissions.WebhooksManage)]
    [IfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemResponse(StatusCodes.Status404NotFound, "webhook.subscription_not_found")]
    public Task<IActionResult> Update(WebhookSubscriptionId id, UpdateWebhookSubscriptionRequest body, CancellationToken ct) =>
        SendNoContent(new UpdateWebhookSubscriptionCommand(id, body.Name, body.Url, body.EventTypes, body.IsActive, IfMatchVersion), ct);

    /// <summary>Issue a new signing secret (shown once); the old one stops working immediately.</summary>
    [HttpPost("subscriptions/{id}/rotate-secret")]
    [HasPermission(Permissions.WebhooksManage)]
    [IfMatch]
    [ProducesResponseType(typeof(WebhookSubscriptionSecretDto), StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound, "webhook.subscription_not_found")]
    public Task<ActionResult<WebhookSubscriptionSecretDto>> RotateSecret(WebhookSubscriptionId id, CancellationToken ct) =>
        Send(new RotateWebhookSecretCommand(id, IfMatchVersion), ct);

    /// <summary>Delete a subscription.</summary>
    [HttpDelete("subscriptions/{id}")]
    [HasPermission(Permissions.WebhooksManage)]
    [IfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemResponse(StatusCodes.Status404NotFound, "webhook.subscription_not_found")]
    public Task<IActionResult> Delete(WebhookSubscriptionId id, CancellationToken ct) =>
        SendNoContent(new DeleteWebhookSubscriptionCommand(id, IfMatchVersion), ct);

    /// <summary>List deliveries (status, attempts, last result; never a body or header), newest first, keyset paged.</summary>
    [HttpGet("deliveries")]
    [HasPermission(Permissions.WebhooksManage)]
    [ProducesResponseType(typeof(IReadOnlyList<WebhookDeliveryDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<WebhookDeliveryDto>>> ListDeliveries(
        [FromQuery] string? status, [FromQuery] Guid? subscriptionId, [FromQuery] string? eventType, [FromQuery] int? limit, [FromQuery] string? cursor, CancellationToken ct) =>
        SendPaged(new ListWebhookDeliveriesQuery(status, subscriptionId, eventType, limit, cursor), ct);

    /// <summary>Send a dead-lettered delivery again with a fresh attempt budget.</summary>
    [HttpPost("deliveries/{id}/redeliver")]
    [HasPermission(Permissions.WebhooksManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemResponse(StatusCodes.Status404NotFound, "webhook.delivery_not_found")]
    [ProblemResponse(StatusCodes.Status409Conflict, "webhook.delivery_not_dead")]
    public Task<IActionResult> Redeliver(WebhookDeliveryId id, CancellationToken ct) =>
        SendNoContent(new RedeliverWebhookDeliveryCommand(id), ct);
}
