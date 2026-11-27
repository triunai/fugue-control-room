using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Fugue.Application.Features.Claims;
using Fugue.Domain.Claims;
using Fugue.Domain.Identity;
using Fugue.Tests.Persistence;
using Fugue.Tests.Webhooks;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Fugue.Tests.Claims;

/// <summary>
/// The claim lifecycle over HTTP against real Postgres: the happy path with its timeline and outbox rows, visibility (404 for someone else's claim),
/// optimistic concurrency (412 stale, 409 on the state, 428 missing), the money and ownership rules, and permissions.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class ClaimsWorkflowTests(PostgresFixture db)
{
    private readonly ClaimsHarness _h = new(db);

    private static string Url(ClaimDto claim, string suffix = "") => $"/api/v1/claims/{claim.Id}{suffix}";

    private static HttpMethod Put => HttpMethod.Put;

    private static HttpMethod Post => HttpMethod.Post;

    /// <summary>Submitted claim picked up by <paramref name="officer"/>, with a reserve of 100000.</summary>
    private async Task<ClaimDto> UnderReviewAsync(HttpClient claimant, HttpClient officer, long reserve = 100_000)
    {
        ClaimDto claim = await _h.SubmitAsync(claimant);
        claim = await ClaimsHarness.OkAsync(officer, Post, Url(claim, "/pickup"), claim.Version);
        return await ClaimsHarness.OkAsync(officer, Put, Url(claim, "/reserve"), claim.Version, new { amountMinor = reserve });
    }

    [Fact]
    public async Task The_whole_lifecycle_writes_a_status_a_timeline_row_and_an_outbox_event_per_transition()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient officer = _h.Officer();
        (_, HttpClient manager) = await _h.NewUserClientAsync(RoleCodes.Manager);

        ClaimDto claim = await _h.SubmitAsync(self, Market.HK, LineOfBusiness.Property);
        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        claim = await ClaimsHarness.OkAsync(officer, Post, Url(claim, "/pickup"), claim.Version);
        Assert.Equal(ClaimStatus.UnderReview, claim.Status);
        claim = await ClaimsHarness.OkAsync(officer, Post, Url(claim, "/info-requests"), claim.Version, new { question = "Photos of the damage?" });
        Assert.Equal(ClaimStatus.InfoRequested, claim.Status);
        Assert.Equal("Photos of the damage?", claim.OpenInfoRequest!.Question);

        ClaimDto asClaimant = (await self.GetFromJsonAsync<ClaimDto>(Url(claim)))!;
        Assert.Equal(claim.OpenInfoRequest.Id, asClaimant.OpenInfoRequest!.Id); // the claimant sees the question to answer

        claim = await ClaimsHarness.OkAsync(self, Post, Url(claim, $"/info-requests/{claim.OpenInfoRequest.Id}/response"), claim.Version, new { answer = "Attached." });
        Assert.Equal(ClaimStatus.UnderReview, claim.Status);
        Assert.Null(claim.OpenInfoRequest);
        claim = await ClaimsHarness.OkAsync(officer, Put, Url(claim, "/reserve"), claim.Version, new { amountMinor = 500_000 });
        claim = await ClaimsHarness.OkAsync(officer, Post, Url(claim, "/decision"), claim.Version, new { outcome = "approve", amountMinor = 450_000, reason = "Covered under section 4." });
        Assert.Equal(ClaimStatus.Approved, claim.Status);
        Assert.Equal(450_000, claim.ApprovedAmountMinor);
        claim = await ClaimsHarness.OkAsync(manager, Post, Url(claim, "/settle"), claim.Version);
        Assert.Equal(ClaimStatus.Settled, claim.Status);
        Assert.NotNull(claim.SettledAt);

        // One timeline row per status change (the reserve is internal: no row), in order.
        ClaimTimelineEntryDto[] timeline = (await self.GetFromJsonAsync<ClaimTimelineEntryDto[]>(Url(claim, "/timeline")))!;
        Assert.Equal(
            [ClaimStatus.Submitted, ClaimStatus.UnderReview, ClaimStatus.InfoRequested, ClaimStatus.UnderReview, ClaimStatus.Approved, ClaimStatus.Settled],
            timeline.Select(e => e.ToStatus));
        Assert.Equal(6, await _h.TimelineCountAsync(claim.Id.Value));
        Assert.All(timeline, e =>
        {
            Assert.DoesNotContain("500000", e.Note, StringComparison.Ordinal);
            Assert.DoesNotContain("450000", e.Note, StringComparison.Ordinal);
        });
        Assert.Contains("Photos of the damage?", timeline[2].Note, StringComparison.Ordinal);
        Assert.Contains("Covered under section 4.", timeline[4].Note, StringComparison.Ordinal);

        // One outbox event per transition (and one for the reserve), in the same order, keyed by the claim (per-claim order), with a thin payload.
        List<OutboxRow> events = await OutboxProbe.ByAggregateAsync(db, claim.Id.ToString());
        Assert.Equal(
            ["claim.submitted.v1", "claim.assigned.v1", "claim.info.requested.v1", "claim.info.provided.v1", "claim.reserve.set.v1", "claim.decided.v1", "claim.settled.v1"],
            events.Select(e => e.EventType));
        Assert.All(events, e =>
        {
            Assert.Equal("claim", e.AggregateType);
            Assert.Equal(claim.Id.ToString(), e.SagaId);
            Assert.Equal(claim.Number, e.Payload.GetProperty("number").GetString());
            Assert.Equal("HK", e.Payload.GetProperty("market").GetString());
            Assert.DoesNotContain(e.Payload.EnumerateObject(), p => p.Name is "reserveMinor" or "approvedAmountMinor" or "incidentDescription" or "reason" or "question" or "answer");
        });
        Assert.Equal(["Submitted", "UnderReview", "InfoRequested", "UnderReview", "UnderReview", "Approved", "Settled"], events.Select(e => e.Payload.GetProperty("status").GetString()));

        // The scaffold writes the audit rows itself: one success row per EF update, entity Claim.
        Assert.True(await _h.ScalarAsync<long>("SELECT count(*) FROM audit_log WHERE entity_type = 'Claim' AND entity_id = @id AND outcome = 'success'", ("id", claim.Id.ToString())) >= 7);
    }

    [Fact]
    public async Task Another_claimant_gets_404_on_the_claim_its_timeline_and_when_answering()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient other = _h.Claimant(DevUsers.Self2);
        using HttpClient officer = _h.Officer();
        ClaimDto claim = await _h.SubmitAsync(self);
        claim = await ClaimsHarness.OkAsync(officer, Post, Url(claim, "/pickup"), claim.Version);
        claim = await ClaimsHarness.OkAsync(officer, Post, Url(claim, "/info-requests"), claim.Version, new { question = "Q?" });

        HttpResponseMessage get = await other.GetAsync(Url(claim));
        HttpResponseMessage timeline = await other.GetAsync(Url(claim, "/timeline"));
        HttpResponseMessage respond = await ClaimsHarness.SendAsync(other, Post, Url(claim, $"/info-requests/{claim.OpenInfoRequest!.Id}/response"), claim.Version, new { answer = "I am not the claimant" });
        HttpResponseMessage unknown = await other.GetAsync($"/api/v1/claims/{Guid.NewGuid()}");

        await ClaimsHarness.AssertProblemAsync(get, HttpStatusCode.NotFound, "claim.not_found");
        await ClaimsHarness.AssertProblemAsync(timeline, HttpStatusCode.NotFound, "claim.not_found");
        await ClaimsHarness.AssertProblemAsync(respond, HttpStatusCode.NotFound, "claim.not_found");
        await ClaimsHarness.AssertProblemAsync(unknown, HttpStatusCode.NotFound, "claim.not_found"); // indistinguishable from an absent claim
        ClaimDto unchanged = (await officer.GetFromJsonAsync<ClaimDto>(Url(claim)))!;
        Assert.Equal(ClaimStatus.InfoRequested, unchanged.Status);
    }

    [Fact]
    public async Task Even_a_caller_who_can_see_every_claim_can_only_answer_on_their_own()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient admin = _h.Admin(); // CLAIMS_ALL and CLAIMS_RESPOND, but not the claimant
        using HttpClient officer = _h.Officer();
        ClaimDto claim = await _h.SubmitAsync(self);
        claim = await ClaimsHarness.OkAsync(officer, Post, Url(claim, "/pickup"), claim.Version);
        claim = await ClaimsHarness.OkAsync(officer, Post, Url(claim, "/info-requests"), claim.Version, new { question = "Q?" });

        HttpResponseMessage respond = await ClaimsHarness.SendAsync(admin, Post, Url(claim, $"/info-requests/{claim.OpenInfoRequest!.Id}/response"), claim.Version, new { answer = "x" });

        await ClaimsHarness.AssertProblemAsync(respond, HttpStatusCode.NotFound, "claim.not_found");
    }

    [Fact]
    public async Task A_second_officer_with_the_old_etag_gets_412_and_with_a_fresh_one_gets_409()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient first = _h.Officer();
        (_, HttpClient second) = await _h.NewUserClientAsync(RoleCodes.Staff);
        ClaimDto claim = await _h.SubmitAsync(self);
        await ClaimsHarness.OkAsync(first, Post, Url(claim, "/pickup"), claim.Version);

        HttpResponseMessage stale = await ClaimsHarness.SendAsync(second, Post, Url(claim, "/pickup"), claim.Version);
        ClaimDto fresh = (await second.GetFromJsonAsync<ClaimDto>(Url(claim)))!;
        HttpResponseMessage conflict = await ClaimsHarness.SendAsync(second, Post, Url(claim, "/pickup"), fresh.Version);

        await ClaimsHarness.AssertProblemAsync(stale, HttpStatusCode.PreconditionFailed, "claim.version_mismatch");
        await ClaimsHarness.AssertProblemAsync(conflict, HttpStatusCode.Conflict, "claim.invalid_transition");
        Assert.Equal(2, await _h.TimelineCountAsync(claim.Id.Value)); // neither failure wrote a timeline row
        Assert.Equal(2, (await OutboxProbe.ByAggregateAsync(db, claim.Id.ToString())).Count); // nor an event
    }

    [Fact]
    public async Task Two_officers_racing_to_pick_up_one_claim_with_the_same_etag_produce_exactly_one_winner()
    {
        using HttpClient self = _h.Claimant();
        (_, HttpClient a) = await _h.NewUserClientAsync(RoleCodes.Staff);
        (_, HttpClient b) = await _h.NewUserClientAsync(RoleCodes.Staff);
        ClaimDto claim = await _h.SubmitAsync(self);

        HttpResponseMessage[] results = await Task.WhenAll(
            ClaimsHarness.SendAsync(a, Post, Url(claim, "/pickup"), claim.Version),
            ClaimsHarness.SendAsync(b, Post, Url(claim, "/pickup"), claim.Version));

        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, r => r.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict);
        Assert.Equal(2, await _h.TimelineCountAsync(claim.Id.Value));
        Assert.Equal(1, await _h.ScalarAsync<long>("SELECT count(*) FROM outbox_messages WHERE aggregate_id = @id AND event_type = 'claim.assigned.v1'", ("id", claim.Id.ToString())));
    }

    [Theory]
    [InlineData("POST", "/pickup", null)]
    [InlineData("PUT", "/reserve", """{"amountMinor":100}""")]
    [InlineData("POST", "/info-requests", """{"question":"Q?"}""")]
    [InlineData("POST", "/decision", """{"outcome":"reject","reason":"No."}""")]
    [InlineData("POST", "/settle", null)]
    public async Task Every_staff_mutation_without_if_match_is_428(string method, string suffix, string? json)
    {
        using HttpClient self = _h.Claimant();
        using HttpClient admin = _h.Admin(); // holds every permission, so the 428 is the handler's, not an authorization failure
        ClaimDto claim = await _h.SubmitAsync(self);
        using var request = new HttpRequestMessage(new HttpMethod(method), Url(claim, suffix));
        if (json is not null)
        {
            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response = await admin.SendAsync(request);

        await ClaimsHarness.AssertProblemAsync(response, HttpStatusCode.PreconditionRequired, "claim.if_match_required");
    }

    [Fact]
    public async Task Answering_without_if_match_is_428()
    {
        using HttpClient self = _h.Claimant();
        ClaimDto claim = await _h.SubmitAsync(self);

        HttpResponseMessage response = await ClaimsHarness.SendAsync(self, Post, Url(claim, $"/info-requests/{Guid.NewGuid()}/response"), null, new { answer = "x" });

        await ClaimsHarness.AssertProblemAsync(response, HttpStatusCode.PreconditionRequired, "claim.if_match_required");
    }

    [Fact]
    public async Task Deciding_to_approve_without_a_reserve_is_409()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient officer = _h.Officer();
        ClaimDto claim = await _h.SubmitAsync(self);
        claim = await ClaimsHarness.OkAsync(officer, Post, Url(claim, "/pickup"), claim.Version);

        HttpResponseMessage response = await ClaimsHarness.SendAsync(officer, Post, Url(claim, "/decision"), claim.Version, new { outcome = "approve", amountMinor = 100, reason = "ok" });

        await ClaimsHarness.AssertProblemAsync(response, HttpStatusCode.Conflict, "claim.reserve_required");
        Assert.Equal(ClaimStatus.UnderReview, (await officer.GetFromJsonAsync<ClaimDto>(Url(claim)))!.Status);
    }

    [Theory]
    [InlineData(100_001L)] // over the reserve of 100000
    [InlineData(0L)]
    [InlineData(-5L)]
    public async Task Approving_an_amount_outside_zero_to_reserve_is_400_and_the_claim_is_unchanged(long amount)
    {
        using HttpClient self = _h.Claimant();
        using HttpClient officer = _h.Officer();
        ClaimDto claim = await UnderReviewAsync(self, officer);

        HttpResponseMessage response = await ClaimsHarness.SendAsync(officer, Post, Url(claim, "/decision"), claim.Version, new { outcome = "approve", amountMinor = amount, reason = "ok" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(ClaimStatus.UnderReview, (await officer.GetFromJsonAsync<ClaimDto>(Url(claim)))!.Status);
    }

    [Fact]
    public async Task Approving_exactly_the_reserve_is_allowed()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient officer = _h.Officer();
        ClaimDto claim = await UnderReviewAsync(self, officer, 70_000);

        ClaimDto approved = await ClaimsHarness.OkAsync(officer, Post, Url(claim, "/decision"), claim.Version, new { outcome = "Approve", amountMinor = 70_000, reason = "Full amount." });

        Assert.Equal(70_000, approved.ApprovedAmountMinor);
    }

    [Fact]
    public async Task A_rejection_records_the_reason_needs_no_reserve_and_ends_the_claim()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient officer = _h.Officer();
        (_, HttpClient manager) = await _h.NewUserClientAsync(RoleCodes.Manager);
        ClaimDto claim = await _h.SubmitAsync(self);
        claim = await ClaimsHarness.OkAsync(officer, Post, Url(claim, "/pickup"), claim.Version);

        ClaimDto rejected = await ClaimsHarness.OkAsync(officer, Post, Url(claim, "/decision"), claim.Version, new { outcome = "reject", reason = "Policy lapsed." });

        Assert.Equal(ClaimStatus.Rejected, rejected.Status);
        ClaimDto asClaimant = (await self.GetFromJsonAsync<ClaimDto>(Url(claim)))!;
        Assert.Equal("Policy lapsed.", asClaimant.DecisionReason);
        Assert.Null(asClaimant.ApprovedAmountMinor);
        // Terminal: nothing moves it again.
        await ClaimsHarness.AssertProblemAsync(
            await ClaimsHarness.SendAsync(manager, Post, Url(claim, "/settle"), rejected.Version), HttpStatusCode.Conflict, "claim.invalid_transition");
        await ClaimsHarness.AssertProblemAsync(
            await ClaimsHarness.SendAsync(manager, Post, Url(claim, "/decision"), rejected.Version, new { outcome = "reject", reason = "again" }), HttpStatusCode.Conflict, "claim.invalid_transition");
        await ClaimsHarness.AssertProblemAsync(
            await ClaimsHarness.SendAsync(manager, Put, Url(claim, "/reserve"), rejected.Version, new { amountMinor = 5 }), HttpStatusCode.Conflict, "claim.invalid_transition");
    }

    [Theory]
    [InlineData("""{"outcome":"approve","reason":"ok"}""")] // approve needs an amount
    [InlineData("""{"outcome":"reject","amountMinor":5,"reason":"no"}""")] // reject carries none
    [InlineData("""{"outcome":"approve","amountMinor":5,"reason":""}""")] // a reason is required
    public async Task A_malformed_decision_is_a_400(string json)
    {
        using HttpClient self = _h.Claimant();
        using HttpClient officer = _h.Officer();
        ClaimDto claim = await UnderReviewAsync(self, officer);
        using var request = new HttpRequestMessage(Post, Url(claim, "/decision")) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{claim.Version}\"");

        HttpResponseMessage response = await officer.SendAsync(request);

        await ClaimsHarness.AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    public async Task Only_the_assigned_officer_or_a_manager_may_reserve_ask_or_decide()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient owner = _h.Officer();
        (_, HttpClient other) = await _h.NewUserClientAsync(RoleCodes.Staff);
        (_, HttpClient manager) = await _h.NewUserClientAsync(RoleCodes.Manager);
        ClaimDto claim = await UnderReviewAsync(self, owner);

        HttpResponseMessage reserve = await ClaimsHarness.SendAsync(other, Put, Url(claim, "/reserve"), claim.Version, new { amountMinor = 5 });
        HttpResponseMessage ask = await ClaimsHarness.SendAsync(other, Post, Url(claim, "/info-requests"), claim.Version, new { question = "Q?" });
        HttpResponseMessage decide = await ClaimsHarness.SendAsync(other, Post, Url(claim, "/decision"), claim.Version, new { outcome = "reject", reason = "no" });

        await ClaimsHarness.AssertProblemAsync(reserve, HttpStatusCode.Conflict, "claim.not_assigned_to_you");
        await ClaimsHarness.AssertProblemAsync(ask, HttpStatusCode.Conflict, "claim.not_assigned_to_you");
        await ClaimsHarness.AssertProblemAsync(decide, HttpStatusCode.Conflict, "claim.not_assigned_to_you");
        Assert.Equal(ClaimStatus.UnderReview, (await owner.GetFromJsonAsync<ClaimDto>(Url(claim)))!.Status);

        // The manager override: CLAIMS_SETTLE lifts the assignment rule.
        ClaimDto decided = await ClaimsHarness.OkAsync(manager, Post, Url(claim, "/decision"), claim.Version, new { outcome = "reject", reason = "Duplicate claim." });
        Assert.Equal(ClaimStatus.Rejected, decided.Status);
    }

    [Fact]
    public async Task An_officer_may_not_work_an_unassigned_claim_it_is_an_invalid_transition_not_an_ownership_error()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient officer = _h.Officer();
        ClaimDto claim = await _h.SubmitAsync(self);

        HttpResponseMessage ask = await ClaimsHarness.SendAsync(officer, Post, Url(claim, "/info-requests"), claim.Version, new { question = "Q?" });
        HttpResponseMessage decide = await ClaimsHarness.SendAsync(officer, Post, Url(claim, "/decision"), claim.Version, new { outcome = "reject", reason = "no" });

        await ClaimsHarness.AssertProblemAsync(ask, HttpStatusCode.Conflict, "claim.invalid_transition");
        await ClaimsHarness.AssertProblemAsync(decide, HttpStatusCode.Conflict, "claim.invalid_transition");
    }

    [Fact]
    public async Task Staff_without_settle_cannot_settle_but_a_manager_can()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient officer = _h.Officer();
        (_, HttpClient manager) = await _h.NewUserClientAsync(RoleCodes.Manager);
        ClaimDto claim = await UnderReviewAsync(self, officer);
        claim = await ClaimsHarness.OkAsync(officer, Post, Url(claim, "/decision"), claim.Version, new { outcome = "approve", amountMinor = 1000, reason = "ok" });

        HttpResponseMessage denied = await ClaimsHarness.SendAsync(officer, Post, Url(claim, "/settle"), claim.Version);
        ClaimDto settled = await ClaimsHarness.OkAsync(manager, Post, Url(claim, "/settle"), claim.Version);

        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(ClaimStatus.Settled, settled.Status);
    }

    [Fact]
    public async Task Settling_a_claim_that_is_not_approved_is_409()
    {
        using HttpClient self = _h.Claimant();
        (_, HttpClient manager) = await _h.NewUserClientAsync(RoleCodes.Manager);
        ClaimDto claim = await _h.SubmitAsync(self);

        HttpResponseMessage response = await ClaimsHarness.SendAsync(manager, Post, Url(claim, "/settle"), claim.Version);

        await ClaimsHarness.AssertProblemAsync(response, HttpStatusCode.Conflict, "claim.invalid_transition");
    }

    [Fact]
    public async Task Permissions_split_the_audiences_staff_cannot_submit_and_claimants_cannot_handle()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient officer = _h.Officer();
        ClaimDto claim = await _h.SubmitAsync(self);
        object submit = new { market = "SG", line = "Motor", policyNumber = "P", incidentDate = _h.Yesterday().Iso(), incidentDescription = "x" };

        Assert.Equal(HttpStatusCode.Forbidden, (await officer.PostAsJsonAsync("/api/v1/claims", submit)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ClaimsHarness.SendAsync(self, Post, Url(claim, "/pickup"), claim.Version)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ClaimsHarness.SendAsync(self, Put, Url(claim, "/reserve"), claim.Version, new { amountMinor = 5 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ClaimsHarness.SendAsync(self, Post, Url(claim, "/settle"), claim.Version)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await db.Anonymous().GetAsync("/api/v1/claims")).StatusCode);
    }

    [Fact]
    public async Task One_open_question_at_a_time_a_wrong_or_repeated_answer_is_refused()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient officer = _h.Officer();
        ClaimDto claim = await UnderReviewAsync(self, officer);
        claim = await ClaimsHarness.OkAsync(officer, Post, Url(claim, "/info-requests"), claim.Version, new { question = "First?" });
        Guid requestId = claim.OpenInfoRequest!.Id.Value;

        HttpResponseMessage second = await ClaimsHarness.SendAsync(officer, Post, Url(claim, "/info-requests"), claim.Version, new { question = "Second?" });
        HttpResponseMessage wrongId = await ClaimsHarness.SendAsync(self, Post, Url(claim, $"/info-requests/{Guid.NewGuid()}/response"), claim.Version, new { answer = "x" });
        ClaimDto answered = await ClaimsHarness.OkAsync(self, Post, Url(claim, $"/info-requests/{requestId}/response"), claim.Version, new { answer = "Done." });
        HttpResponseMessage again = await ClaimsHarness.SendAsync(self, Post, Url(claim, $"/info-requests/{requestId}/response"), answered.Version, new { answer = "Twice" });

        await ClaimsHarness.AssertProblemAsync(second, HttpStatusCode.Conflict, "claim.invalid_transition");
        await ClaimsHarness.AssertProblemAsync(wrongId, HttpStatusCode.NotFound, "claim.info_request_not_found");
        await ClaimsHarness.AssertProblemAsync(again, HttpStatusCode.Conflict, "claim.invalid_transition");
        Assert.Equal(1L, await _h.ScalarAsync<long>("SELECT count(*) FROM claim_info_requests WHERE claim_id = @id", ("id", claim.Id.Value)));
        Assert.Equal("Done.", await _h.ScalarAsync<string>("SELECT response FROM claim_info_requests WHERE id = @id", ("id", requestId)));
    }

    [Fact]
    public async Task Concurrent_submissions_get_distinct_claim_numbers()
    {
        using HttpClient self = _h.Claimant();

        ClaimDto[] claims = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => _h.SubmitAsync(self, Market.PH)));

        Assert.Equal(12, claims.Select(c => c.Number).Distinct().Count());
        Assert.All(claims, c => Assert.Equal("PHP", c.Currency));
    }

    [Fact]
    public async Task A_refused_transition_leaves_one_failure_audit_row_and_no_timeline_or_event()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient officer = _h.Officer();
        ClaimDto claim = await _h.SubmitAsync(self);
        long timeline = await _h.TimelineCountAsync(claim.Id.Value);

        HttpResponseMessage response = await ClaimsHarness.SendAsync(officer, Post, Url(claim, "/settle"), claim.Version);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); // staff lack CLAIMS_SETTLE: refused before any handler runs
        (_, HttpClient manager) = await _h.NewUserClientAsync(RoleCodes.Manager);
        HttpResponseMessage invalid = await ClaimsHarness.SendAsync(manager, Post, Url(claim, "/settle"), claim.Version);

        await ClaimsHarness.AssertProblemAsync(invalid, HttpStatusCode.Conflict, "claim.invalid_transition");
        Assert.Equal(timeline, await _h.TimelineCountAsync(claim.Id.Value));
        Assert.Single(await OutboxProbe.ByAggregateAsync(db, claim.Id.ToString()));
        Assert.Equal(1, await _h.ScalarAsync<long>(
            "SELECT count(*) FROM audit_log WHERE action = 'claims.settle' AND entity_id = @id AND outcome = 'failure' AND error_code = 'claim.invalid_transition'", ("id", claim.Id.ToString())));
    }

    [Fact]
    public async Task With_the_CLAIMS_module_off_every_claims_endpoint_is_404_module_disabled()
    {
        using var factory = db.Factory.WithWebHostBuilder(b => b.UseSetting("Modules:CLAIMS:Enabled", "false"));
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await db.TokenAsync(DevUsers.Self));

        HttpResponseMessage list = await client.GetAsync("/api/v1/claims");
        HttpResponseMessage submit = await client.PostAsJsonAsync("/api/v1/claims", new { market = "SG" });
        HttpResponseMessage get = await client.GetAsync($"/api/v1/claims/{Guid.NewGuid()}");

        foreach (HttpResponseMessage response in new[] { list, submit, get })
        {
            await ClaimsHarness.AssertProblemAsync(response, HttpStatusCode.NotFound, "module.disabled");
        }
    }

    [Fact]
    public async Task The_claim_row_is_stamped_with_the_actor_on_insert_and_on_each_update()
    {
        (string claimantEmail, Fugue.Domain.Ids.UserId claimantId) = await db.NewUserAsync([RoleCodes.SelfService]);
        (string officerEmail, Fugue.Domain.Ids.UserId officerId) = await db.NewUserAsync([RoleCodes.Staff]);
        using HttpClient self = db.ClientAs(claimantEmail);
        using HttpClient officer = db.ClientAs(officerEmail);

        ClaimDto claim = await _h.SubmitAsync(self);
        await ClaimsHarness.OkAsync(officer, Post, Url(claim, "/pickup"), claim.Version);

        Assert.Equal(claimantId.Value.ToString(), await _h.ScalarAsync<string>("SELECT created_by FROM claims WHERE id = @id", ("id", claim.Id.Value)));
        Assert.Equal(officerId.Value.ToString(), await _h.ScalarAsync<string>("SELECT updated_by FROM claims WHERE id = @id", ("id", claim.Id.Value)));
        Assert.Equal(officerId.Value, await _h.ScalarAsync<Guid>("SELECT assigned_officer_id FROM claims WHERE id = @id", ("id", claim.Id.Value)));
    }

    [Fact]
    public async Task Claim_bodies_never_serialise_the_reserve_to_a_claimant_even_in_lists()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient officer = _h.Officer();
        string policy = ClaimsHarness.UniquePolicy();
        ClaimDto claim = await _h.SubmitAsync(self, policy: policy);
        claim = await ClaimsHarness.OkAsync(officer, Post, Url(claim, "/pickup"), claim.Version);
        await ClaimsHarness.OkAsync(officer, Put, Url(claim, "/reserve"), claim.Version, new { amountMinor = 123_456 });

        using JsonDocument list = JsonDocument.Parse(await self.GetStringAsync($"/api/v1/claims?q={policy}"));
        using JsonDocument staffList = JsonDocument.Parse(await officer.GetStringAsync($"/api/v1/claims?q={policy}"));

        Assert.Equal(JsonValueKind.Null, list.RootElement[0].GetProperty("reserveMinor").ValueKind);
        Assert.Equal(123_456, staffList.RootElement[0].GetProperty("reserveMinor").GetInt64());
    }
}
