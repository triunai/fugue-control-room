using System.Net;
using System.Net.Http.Json;
using System.Text;
using Fugue.Application.Features.Claims.Models;
using Fugue.Domain.Claims;
using Fugue.Domain.Identity;
using Fugue.Tests.Claims;
using Fugue.Tests.Persistence;

namespace Fugue.Tests.Letters;

/// <summary>
/// <c>GET /claims/{id}/letter</c> against real Postgres (item 11b): the scope of the claim (staff and the own claimant 200, another claimant 404,
/// anonymous 401), decided-only (409 before a decision), and the PDF's content read back from the response.
/// </summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class ClaimLetterApiTests(PostgresFixture db)
{
    private readonly ClaimsHarness _h = new(db);

    private static string Url(ClaimDto claim, string suffix = "") => $"/api/v1/claims/{claim.Id}{suffix}";

    /// <summary>A claim submitted by <paramref name="claimant"/>, picked up by staff with a reserve of SGD 1,000.00, then decided.</summary>
    private async Task<ClaimDto> DecidedAsync(HttpClient claimant, HttpClient officer, string outcome)
    {
        ClaimDto claim = await _h.SubmitAsync(claimant);
        claim = await ClaimsHarness.OkAsync(officer, HttpMethod.Post, Url(claim, "/pickup"), claim.Version);
        claim = await ClaimsHarness.OkAsync(officer, HttpMethod.Put, Url(claim, "/reserve"), claim.Version, new { amountMinor = 100_000 });
        object body = outcome == "approve"
            ? new { outcome, amountMinor = 45_000, reason = "Covered under section 4." }
            : new { outcome, reason = "The policy had lapsed on the incident date." };
        return await ClaimsHarness.OkAsync(officer, HttpMethod.Post, Url(claim, "/decision"), claim.Version, body);
    }

    private static async Task<string> PdfTextAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        byte[] pdf = await response.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5), StringComparison.Ordinal);
        return PdfText.Extract(pdf);
    }

    [Fact]
    public async Task Staff_download_the_settlement_letter_of_an_approved_claim_with_its_number_and_amount()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient officer = _h.Officer();
        ClaimDto claim = await DecidedAsync(self, officer, "approve");

        using HttpResponseMessage response = await officer.GetAsync(Url(claim, "/letter"));
        string text = await PdfTextAsync(response);

        Assert.Equal($"{claim.Number}-settlement-letter.pdf", response.Content.Headers.ContentDisposition?.FileName);
        Assert.Contains(claim.Number, text, StringComparison.Ordinal);
        Assert.Contains("SGD 450.00", text, StringComparison.Ordinal);
        Assert.Contains(claim.PolicyNumber, text, StringComparison.Ordinal);
        Assert.Contains("Covered under section 4.", text, StringComparison.Ordinal);
        Assert.Contains("Sample document, not a legal letter", text, StringComparison.Ordinal);
        Assert.Contains(response.Headers.GetValues("X-Correlation-Id").Single(), text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_claimant_downloads_their_own_letter_without_the_reserve_or_staff_names()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient officer = _h.Officer();
        ClaimDto claim = await DecidedAsync(self, officer, "approve");
        ClaimTimelineEntryDto[] staffTimeline = (await officer.GetFromJsonAsync<ClaimTimelineEntryDto[]>(Url(claim, "/timeline")))!;
        string decider = staffTimeline.Single(e => e.ToStatus == ClaimStatus.Approved).ActorDisplay;

        string text = await PdfTextAsync(await self.GetAsync(Url(claim, "/letter")));

        Assert.Contains(claim.Number, text, StringComparison.Ordinal);
        Assert.Contains("SGD 450.00", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SGD 1,000.00", text, StringComparison.Ordinal); // the reserve
        Assert.DoesNotContain(decider, text, StringComparison.Ordinal);
        Assert.Contains("Claims team", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_rejected_claim_gets_a_decision_letter_and_a_settled_one_a_settlement_letter()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient officer = _h.Officer();
        (_, HttpClient manager) = await _h.NewUserClientAsync(RoleCodes.Manager);

        ClaimDto rejected = await DecidedAsync(self, officer, "reject");
        using HttpResponseMessage decision = await self.GetAsync(Url(rejected, "/letter"));
        string text = await PdfTextAsync(decision);
        Assert.Equal($"{rejected.Number}-decision-letter.pdf", decision.Content.Headers.ContentDisposition?.FileName);
        Assert.Contains("The policy had lapsed on the incident date.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Approved amount", text, StringComparison.Ordinal);

        ClaimDto approved = await DecidedAsync(self, officer, "approve");
        ClaimDto settled = await ClaimsHarness.OkAsync(manager, HttpMethod.Post, Url(approved, "/settle"), approved.Version);
        using HttpResponseMessage settlement = await self.GetAsync(Url(settled, "/letter"));
        Assert.Contains("Settled on", await PdfTextAsync(settlement), StringComparison.Ordinal);
        Assert.Equal($"{settled.Number}-settlement-letter.pdf", settlement.Content.Headers.ContentDisposition?.FileName);
    }

    [Fact]
    public async Task Another_claimant_gets_404_and_an_anonymous_caller_401()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient officer = _h.Officer();
        using HttpClient other = _h.Claimant(DevUsers.Self2);
        using HttpClient anonymous = _h.Db.Anonymous();
        ClaimDto claim = await DecidedAsync(self, officer, "approve");

        await ClaimsHarness.AssertProblemAsync(await other.GetAsync(Url(claim, "/letter")), HttpStatusCode.NotFound, "claim.not_found");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Url(claim, "/letter"))).StatusCode);
    }

    [Fact]
    public async Task An_undecided_claim_has_no_letter_yet()
    {
        using HttpClient self = _h.Claimant();
        using HttpClient officer = _h.Officer();
        ClaimDto claim = await _h.SubmitAsync(self);

        await ClaimsHarness.AssertProblemAsync(await self.GetAsync(Url(claim, "/letter")), HttpStatusCode.Conflict, "claim.letter_not_available");
        claim = await ClaimsHarness.OkAsync(officer, HttpMethod.Post, Url(claim, "/pickup"), claim.Version);
        await ClaimsHarness.AssertProblemAsync(await officer.GetAsync(Url(claim, "/letter")), HttpStatusCode.Conflict, "claim.letter_not_available");
    }
}
