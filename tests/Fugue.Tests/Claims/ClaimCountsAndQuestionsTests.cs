using System.Net;
using System.Net.Http.Json;
using Fugue.Application.Features.Claims;
using Fugue.Domain.Claims;
using Fugue.Domain.Identity;
using Fugue.Tests.Persistence;

namespace Fugue.Tests.Claims;

/// <summary>
/// <c>GET /claims/counts</c> (per-status counts under the caller's scope and the list's filters) and <c>GET /claims/{id}/info-requests</c> (the past
/// questions, newest first, scoped like the timeline), over HTTP against real Postgres. The fixture database is shared, so every count is isolated by a
/// unique policy number through <c>q</c>.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class ClaimCountsAndQuestionsTests(PostgresFixture db)
{
    private readonly ClaimsHarness _h = new(db);

    private static string Url(ClaimDto claim, string suffix = "") => $"/api/v1/claims/{claim.Id}{suffix}";

    private static int Count(ClaimCountsDto counts, ClaimStatus status) => counts.ByStatus.Single(c => c.Status == status).Count;

    [Fact]
    public async Task A_claimant_counts_only_their_own_claims_and_staff_count_every_claim()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient other = _h.Claimant(DevUsers.Self2);
        using HttpClient officer = _h.Officer();
        string marker = ClaimsHarness.UniquePolicy();
        ClaimDto mine = await _h.SubmitAsync(self, policy: marker + "-A");
        await _h.SubmitAsync(other, policy: marker + "-B");
        await ClaimsHarness.OkAsync(officer, HttpMethod.Post, Url(mine, "/pickup"), mine.Version);

        ClaimCountsDto own = (await self.GetFromJsonAsync<ClaimCountsDto>($"/api/v1/claims/counts?q={marker}"))!;
        ClaimCountsDto staff = (await officer.GetFromJsonAsync<ClaimCountsDto>($"/api/v1/claims/counts?q={marker}"))!;

        Assert.Equal(1, own.Total);
        Assert.Equal(1, Count(own, ClaimStatus.UnderReview));
        Assert.Equal(0, Count(own, ClaimStatus.Submitted));
        Assert.Equal(2, staff.Total);
        Assert.Equal(1, Count(staff, ClaimStatus.UnderReview));
        Assert.Equal(1, Count(staff, ClaimStatus.Submitted));
        Assert.Equal(Enum.GetValues<ClaimStatus>(), staff.ByStatus.Select(c => c.Status)); // every status is present, zero included
    }

    [Fact]
    public async Task The_counts_apply_the_assigned_market_and_line_filters_of_the_list()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient officer = _h.Officer();
        string marker = ClaimsHarness.UniquePolicy();
        ClaimDto picked = await _h.SubmitAsync(self, Market.MY, LineOfBusiness.Motor, marker + "-1");
        await _h.SubmitAsync(self, Market.SG, LineOfBusiness.Property, marker + "-2");
        await ClaimsHarness.OkAsync(officer, HttpMethod.Post, Url(picked, "/pickup"), picked.Version);

        async Task<ClaimCountsDto> Counts(string query) => (await officer.GetFromJsonAsync<ClaimCountsDto>($"/api/v1/claims/counts?q={marker}{query}"))!;

        Assert.Equal(1, (await Counts("&assigned=me")).Total);
        Assert.Equal(1, (await Counts("&assigned=none")).Total);
        Assert.Equal(2, (await Counts("&assigned=any")).Total);
        Assert.Equal(1, (await Counts("&market=MY")).Total);
        Assert.Equal(1, (await Counts("&line=Property")).Total);
        Assert.Equal(0, (await Counts("&market=MY&line=Property")).Total);
        Assert.Equal(1, Count(await Counts("&market=MY&assigned=me"), ClaimStatus.UnderReview));

        // The list and the counts agree on the same filters.
        ClaimDto[] listed = (await officer.GetFromJsonAsync<ClaimDto[]>($"/api/v1/claims?q={marker}&market=MY&assigned=me"))!;
        Assert.Equal((await Counts("&market=MY&assigned=me")).Total, listed.Length);
    }

    [Theory]
    [InlineData("assigned=nobody")]
    [InlineData("market=XX")]
    [InlineData("line=Marine")]
    public async Task A_bad_counts_filter_is_400(string query)
    {
        using HttpClient officer = _h.Officer();

        HttpResponseMessage response = await officer.GetAsync($"/api/v1/claims/counts?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Counts_and_past_questions_need_sign_in_and_claims_read()
    {
        using HttpClient anonymous = db.Anonymous();
        (string email, _) = await db.NewUserAsync([]);
        using HttpClient noRole = db.ClientAs(email);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/claims/counts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await noRole.GetAsync("/api/v1/claims/counts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await noRole.GetAsync($"/api/v1/claims/{Guid.NewGuid()}/info-requests")).StatusCode);
    }

    [Fact]
    public async Task The_past_questions_list_newest_first_with_answers_and_the_claimant_sees_the_team_not_the_officer()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient officer = _h.Officer();
        ClaimDto claim = await _h.SubmitAsync(self);
        claim = await ClaimsHarness.OkAsync(officer, HttpMethod.Post, Url(claim, "/pickup"), claim.Version);
        claim = await ClaimsHarness.OkAsync(officer, HttpMethod.Post, Url(claim, "/info-requests"), claim.Version, new { question = "First question?" });
        claim = await ClaimsHarness.OkAsync(self, HttpMethod.Post, Url(claim, $"/info-requests/{claim.OpenInfoRequest!.Id}/response"), claim.Version, new { answer = "First answer." });
        claim = await ClaimsHarness.OkAsync(officer, HttpMethod.Post, Url(claim, "/info-requests"), claim.Version, new { question = "Second question?" });

        ClaimInfoRequestEntryDto[] asClaimant = (await self.GetFromJsonAsync<ClaimInfoRequestEntryDto[]>(Url(claim, "/info-requests")))!;
        ClaimInfoRequestEntryDto[] asStaff = (await officer.GetFromJsonAsync<ClaimInfoRequestEntryDto[]>(Url(claim, "/info-requests")))!;

        Assert.Equal(["Second question?", "First question?"], asClaimant.Select(e => e.Question));
        Assert.Null(asClaimant[0].Answer); // still open
        Assert.Null(asClaimant[0].AnsweredAt);
        Assert.Equal("First answer.", asClaimant[1].Answer);
        Assert.NotNull(asClaimant[1].AnsweredAt);
        Assert.True(asClaimant[0].AskedAt >= asClaimant[1].AskedAt);
        Assert.All(asClaimant, e => Assert.Equal("Claims team", e.AskedBy));
        Assert.All(asStaff, e => Assert.NotEqual("Claims team", e.AskedBy)); // staff read the officer's real display name
        Assert.Equal(asClaimant.Select(e => e.Id), asStaff.Select(e => e.Id));
    }

    [Fact]
    public async Task The_past_questions_are_404_outside_the_callers_scope_and_empty_when_none_were_asked()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient other = _h.Claimant(DevUsers.Self2);
        ClaimDto claim = await _h.SubmitAsync(self);

        HttpResponseMessage foreign = await other.GetAsync(Url(claim, "/info-requests"));
        HttpResponseMessage unknown = await self.GetAsync($"/api/v1/claims/{Guid.NewGuid()}/info-requests");

        await ClaimsHarness.AssertProblemAsync(foreign, HttpStatusCode.NotFound, "claim.not_found");
        await ClaimsHarness.AssertProblemAsync(unknown, HttpStatusCode.NotFound, "claim.not_found");
        Assert.Empty((await self.GetFromJsonAsync<ClaimInfoRequestEntryDto[]>(Url(claim, "/info-requests")))!);
    }
}
