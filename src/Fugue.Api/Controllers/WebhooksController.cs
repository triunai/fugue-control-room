using Fugue.Api.Auth;
using Fugue.Api.Contracts;
using Fugue.Api.Modules;
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
    [HttpPost("subscriptions")]
    [HasPermission(Permissions.WebhooksManage)]
    public Task<ActionResult<WebhookSubscriptionSecretDto>> Create(CreateWebhookSubscriptionRequest body, CancellationToken ct) =>
        SendCreated(new CreateWebhookSubscriptionCommand(body.Name, body.Url, body.EventTypes), nameof(Get), s => new { id = s.Id }, ct);

    [HttpGet("subscriptions/{id}")]
    [HasPermission(Permissions.WebhooksManage)]
    public Task<ActionResult<WebhookSubscriptionDto>> Get(WebhookSubscriptionId id, CancellationToken ct) =>
        Send(new GetWebhookSubscriptionQuery(id), ct);

    [HttpGet("subscriptions")]
    [HasPermission(Permissions.WebhooksManage)]
    public Task<ActionResult<IReadOnlyList<WebhookSubscriptionDto>>> List([FromQuery] int? limit, [FromQuery] string? cursor, CancellationToken ct) =>
        SendPaged(new ListWebhookSubscriptionsQuery(limit, cursor), ct);

    [HttpPut("subscriptions/{id}")]
    [HasPermission(Permissions.WebhooksManage)]
    public Task<IActionResult> Update(WebhookSubscriptionId id, UpdateWebhookSubscriptionRequest body, CancellationToken ct) =>
        SendNoContent(new UpdateWebhookSubscriptionCommand(id, body.Name, body.Url, body.EventTypes, body.IsActive, IfMatchVersion), ct);

    [HttpPost("subscriptions/{id}/rotate-secret")]
    [HasPermission(Permissions.WebhooksManage)]
    public Task<ActionResult<WebhookSubscriptionSecretDto>> RotateSecret(WebhookSubscriptionId id, CancellationToken ct) =>
        Send(new RotateWebhookSecretCommand(id, IfMatchVersion), ct);

    [HttpDelete("subscriptions/{id}")]
    [HasPermission(Permissions.WebhooksManage)]
    public Task<IActionResult> Delete(WebhookSubscriptionId id, CancellationToken ct) =>
        SendNoContent(new DeleteWebhookSubscriptionCommand(id, IfMatchVersion), ct);

    [HttpGet("deliveries")]
    [HasPermission(Permissions.WebhooksManage)]
    public Task<ActionResult<IReadOnlyList<WebhookDeliveryDto>>> ListDeliveries(
        [FromQuery] string? status, [FromQuery] Guid? subscriptionId, [FromQuery] string? eventType, [FromQuery] int? limit, [FromQuery] string? cursor, CancellationToken ct) =>
        SendPaged(new ListWebhookDeliveriesQuery(status, subscriptionId, eventType, limit, cursor), ct);

    [HttpPost("deliveries/{id}/redeliver")]
    [HasPermission(Permissions.WebhooksManage)]
    public Task<IActionResult> Redeliver(WebhookDeliveryId id, CancellationToken ct) =>
        SendNoContent(new RedeliverWebhookDeliveryCommand(id), ct);
}
