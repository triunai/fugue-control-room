using Fugue.Api.Auth;
using Fugue.Api.Contracts;
using Fugue.Api.Http;
using Fugue.Api.Modules;
using Fugue.Api.OpenApi;
using Fugue.Application.Features.Webhooks.Sink;
using Fugue.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Controllers;

/// <summary>
/// A receiver for the signed webhooks, for ONE-process demos (ADR-011, D-040). Development only: any other environment answers 404.
/// <c>POST</c> is anonymous on purpose (the sender has no JWT; the signature is the credential) and implements the receiver algorithm from the runbook:
/// verify, reject stale timestamps, dedupe on the event id. Tell it the secret with <c>PUT .../secret</c>, read what arrived with <c>GET</c>.
/// </summary>
[Module(ModuleCodes.Webhooks)]
[DevelopmentOnly]
public sealed class DevWebhookSinkController : ApiControllerBase
{
    /// <summary>The receiving end of a signed webhook. Anonymous: the signature is the credential.</summary>
    [HttpPost("~/api/v1/dev/webhook-sink")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(DevWebhookReceiptDto), StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status401Unauthorized, "sink.bad_signature", "sink.stale_timestamp", "sink.malformed", "sink.envelope_mismatch")]
    [ProblemResponse(StatusCodes.Status503ServiceUnavailable, "sink.no_secret")]
    public async Task<ActionResult<DevWebhookReceiptDto>> Receive(CancellationToken ct) =>
        await Send(
            new ReceiveDevWebhookRequest(
                Header("X-Fugue-Event-Id"), Header("X-Fugue-Event-Type"), Header("X-Fugue-Timestamp"), Header("X-Fugue-Signature"),
                Header("X-Correlation-Id"), Header("X-Saga-Id"), Header("traceparent"),
                await Request.ReadBodyAsync(ReceiveDevWebhookValidator.MaxBodyBytes, ct)),
            ct);

    /// <summary>The events the sink accepted, newest first.</summary>
    [HttpGet("~/api/v1/dev/webhook-sink")]
    [HasPermission(Permissions.WebhooksManage)]
    [ProducesResponseType(typeof(IReadOnlyList<ReceivedWebhookDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<ReceivedWebhookDto>>> List([FromQuery] int? limit, CancellationToken ct) =>
        Send(new ListDevWebhookSinkRequest(limit), ct);

    /// <summary>Tell the sink which secret to trust.</summary>
    [HttpPut("~/api/v1/dev/webhook-sink/secret")]
    [HasPermission(Permissions.WebhooksManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> SetSecret(SetSinkSecretRequest body, CancellationToken ct) =>
        SendNoContent(new SetDevWebhookSinkSecretRequest(body.Secret), ct);

    /// <summary>Forget the received events.</summary>
    [HttpDelete("~/api/v1/dev/webhook-sink")]
    [HasPermission(Permissions.WebhooksManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> Clear(CancellationToken ct) =>
        SendNoContent(new ClearDevWebhookSinkRequest(), ct);

    private string? Header(string name) => Request.Headers[name].FirstOrDefault();
}
