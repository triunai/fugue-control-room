using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fugue.Application.Features.Audit;
using Fugue.Application.Features.Claims.Models;
using Fugue.Domain.Claims;
using Fugue.Domain.Identity;
using Fugue.Tests.Claims;
using Fugue.Tests.Persistence;
using Fugue.Tests.Webhooks;

namespace Fugue.Tests.Audit;

/// <summary>
/// GET /api/v1/audit/trace/{correlationId} ("The Score", D-064) over real rows: a reserve set through the API, its outbox event relayed and
/// delivered to a real receiver, then read back as three voices in order under the one correlation id.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class AuditTraceTests(PostgresFixture db)
{
    private readonly ClaimsHarness _claims = new(db);

    private static string NewCorrelation() => "score-" + Guid.NewGuid().ToString("N");

    private static async Task<ClaimDto> SetReserveAsync(HttpClient officer, ClaimDto claim, long amount, string correlation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/claims/{claim.Id}/reserve") { Content = JsonContent.Create(new { amountMinor = amount }) };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{claim.Version}\"");
        request.Headers.Add("X-Correlation-Id", correlation);
        HttpResponseMessage response = await officer.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(correlation, response.Headers.GetValues("X-Correlation-Id").Single()); // the id the console reads off the response
        return (await response.Content.ReadFromJsonAsync<ClaimDto>())!;
    }

    [Fact]
    public async Task A_reserve_command_its_event_and_its_webhook_delivery_come_back_in_order_under_one_correlation_id()
    {
        DateTimeOffset now = db.Clock.Now;
        await using WebhookHarness hooks = await WebhookHarness.StartAsync(db);
        db.Clock.Now = now; // keep the present: audit rows dated in the harness's future would strand the audit indexer's checkpoint for later tests
        await hooks.AddSubscriptionAsync([ClaimReserveSet.Type]);
        using HttpClient claimant = _claims.Claimant();
        using HttpClient officer = _claims.Officer();
        ClaimDto claim = await _claims.SubmitAsync(claimant);
        claim = await ClaimsHarness.OkAsync(officer, HttpMethod.Post, $"/api/v1/claims/{claim.Id}/pickup", claim.Version);
        string correlation = NewCorrelation();

        await SetReserveAsync(officer, claim, 250_000, correlation);
        db.Clock.Now = db.Clock.Now.AddMilliseconds(40); // the relay runs later than the command
        await hooks.RunAsync();

        HttpResponseMessage response = await db.ClientAs(DevUsers.Admin).GetAsync($"/api/v1/audit/trace/{correlation}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AuditTraceDto trace = (await response.Content.ReadFromJsonAsync<AuditTraceDto>())!;

        Assert.Equal(correlation, trace.CorrelationId);
        Assert.Equal("complete", trace.Status);
        Assert.Equal([AuditTraceKinds.Command, AuditTraceKinds.Event, AuditTraceKinds.Delivery], trace.Voices.Select(v => v.Kind).Distinct());
        AuditTraceVoiceDto command = trace.Voices.First(v => v.Kind == AuditTraceKinds.Command);
        AuditTraceVoiceDto evt = Assert.Single(trace.Voices, v => v.Kind == AuditTraceKinds.Event);
        AuditTraceVoiceDto delivery = Assert.Single(trace.Voices, v => v.Kind == AuditTraceKinds.Delivery);
        Assert.Equal(("claims.reserve", "success", 0L), (command.Label, command.Status, command.AtMs));
        Assert.Contains("PUT", command.Detail, StringComparison.Ordinal);
        Assert.Equal(claim.Id.ToString(), command.EntityId);
        Assert.Equal((ClaimReserveSet.Type, "dispatched", 0L, 40L), (evt.Label, evt.Status, evt.AtMs, evt.EndMs));
        Assert.Equal((ClaimReserveSet.Type, "succeeded", 200, 1, 40L), (delivery.Label, delivery.Status, delivery.StatusCode, delivery.Attempts, delivery.AtMs));
        Assert.Equal("harness", delivery.Detail); // the subscription name
        Assert.Equal(40L, trace.SpanMs);
        long[] at = [.. trace.Voices.Select(v => v.AtMs)];
        Assert.Equal(at.Order(), at); // oldest first
    }

    [Fact]
    public async Task A_command_without_a_subscriber_is_a_complete_trace_with_no_delivery_voice()
    {
        using HttpClient claimant = _claims.Claimant();
        using HttpClient officer = _claims.Officer();
        ClaimDto claim = await _claims.SubmitAsync(claimant);
        claim = await ClaimsHarness.OkAsync(officer, HttpMethod.Post, $"/api/v1/claims/{claim.Id}/pickup", claim.Version);
        string correlation = NewCorrelation();
        await SetReserveAsync(officer, claim, 1_000, correlation);

        AuditTraceDto trace = (await db.ClientAs(DevUsers.Admin).GetFromJsonAsync<AuditTraceDto>($"/api/v1/audit/trace/{correlation}"))!;

        Assert.Equal([AuditTraceKinds.Command, AuditTraceKinds.Event], trace.Voices.Select(v => v.Kind).Distinct());
        Assert.Equal("pending", trace.Status); // the event has not been relayed yet
    }

    [Fact]
    public async Task A_failed_command_is_a_failed_trace_with_its_error_code_and_duration()
    {
        using HttpClient claimant = _claims.Claimant();
        using HttpClient officer = _claims.Officer();
        ClaimDto claim = await _claims.SubmitAsync(claimant); // Submitted: a reserve is not allowed yet
        string correlation = NewCorrelation();
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/claims/{claim.Id}/reserve") { Content = JsonContent.Create(new { amountMinor = 5 }) };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{claim.Version}\"");
        request.Headers.Add("X-Correlation-Id", correlation);
        Assert.Equal(HttpStatusCode.Conflict, (await officer.SendAsync(request)).StatusCode);

        AuditTraceDto trace = (await db.ClientAs(DevUsers.Admin).GetFromJsonAsync<AuditTraceDto>($"/api/v1/audit/trace/{correlation}"))!;

        AuditTraceVoiceDto command = Assert.Single(trace.Voices);
        Assert.Equal(("failed", AuditTraceKinds.Command, "failure"), (trace.Status, command.Kind, command.Status));
        Assert.Equal("claim.invalid_transition", command.ErrorCode);
        Assert.NotNull(command.DurationMs);
    }

    [Fact]
    public async Task An_unknown_id_is_404_and_a_malformed_id_is_400()
    {
        HttpClient admin = db.ClientAs(DevUsers.Admin);

        HttpResponseMessage unknown = await admin.GetAsync($"/api/v1/audit/trace/{NewCorrelation()}");
        HttpResponseMessage malformed = await admin.GetAsync("/api/v1/audit/trace/" + Uri.EscapeDataString("bad id!"));

        await ClaimsHarness.AssertProblemAsync(unknown, HttpStatusCode.NotFound, "audit.trace_not_found");
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    [Fact]
    public async Task Without_AUDIT_READ_the_trace_is_401_anonymous_and_403_for_staff_and_self_service()
    {
        string path = $"/api/v1/audit/trace/{NewCorrelation()}";

        Assert.Equal(HttpStatusCode.Unauthorized, (await db.Anonymous().GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await db.ClientAs(DevUsers.Staff).GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await db.ClientAs(DevUsers.Self).GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Staff_holding_AUDIT_READ_by_grant_can_read_a_trace()
    {
        using HttpClient claimant = _claims.Claimant();
        string correlation = NewCorrelation();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/claims")
        {
            Content = JsonContent.Create(new
            {
                market = Market.SG.ToString(),
                line = LineOfBusiness.Motor.ToString(),
                policyNumber = ClaimsHarness.UniquePolicy(),
                incidentDate = _claims.Yesterday().Iso(),
                incidentDescription = "Hail.",
                incidentLocation = "Tampines",
            }),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("N"));
        request.Headers.Add("X-Correlation-Id", correlation);
        Assert.Equal(HttpStatusCode.Created, (await claimant.SendAsync(request)).StatusCode);
        (string email, _) = await db.NewUserAsync([RoleCodes.Staff], overrides: [(Permissions.AuditRead, true, null)]);

        HttpResponseMessage response = await db.ClientAs(email).GetAsync($"/api/v1/audit/trace/{correlation}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains(json.RootElement.GetProperty("voices").EnumerateArray(), v => v.GetProperty("label").GetString() == ClaimSubmitted.Type);
    }
}
