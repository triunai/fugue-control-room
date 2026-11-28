using Fugue.Api.Auth;
using Fugue.Api.Contracts;
using Fugue.Api.Idempotency;
using Fugue.Api.Modules;
using Fugue.Api.OpenApi;
using Fugue.Application.Features.Claims.Commands;
using Fugue.Application.Features.Claims.Models;
using Fugue.Application.Features.Claims.Queries;
using Fugue.Application.Features.Claims.Repos;
using Fugue.Application.Features.Letters.Queries;
using Fugue.Domain.Identity;
using Fugue.Domain.Ids;
using Microsoft.AspNetCore.Mvc;

namespace Fugue.Api.Controllers;

/// <summary>
/// Claims: submit, queue, handle, decide and settle (W-020, D-053). One-liners only; the state machine lives in the Claim aggregate. A claimant sees only
/// their own claims (anything else is 404, never 403) and never the internal reserve. Every mutating action after submit takes <c>If-Match</c> and answers
/// with the claim as it now stands plus its new ETag.
/// </summary>
[Module(ModuleCodes.Claims)]
public sealed class ClaimsController : ApiControllerBase
{
    /// <summary>Submit a claim. The caller is the claimant; the claim starts Submitted. The incident date may not be in the future or more than a year back.</summary>
    [HttpPost]
    [HasPermission(Permissions.ClaimsSubmit)]
    [Idempotent(Required = true)]
    [ProducesResponseType(typeof(ClaimDto), StatusCodes.Status201Created)]
    public Task<ActionResult<ClaimDto>> Submit(SubmitClaimRequest body, CancellationToken ct) =>
        SendCreated(
            new SubmitClaimCommand(body.Market, body.Line, body.PolicyNumber, body.IncidentDate, body.IncidentDescription, body.IncidentLocation),
            nameof(Get), c => new { id = c.Id }, ct);

    /// <summary>
    /// List claims, keyset paged. A claimant sees their own, newest first; staff with CLAIMS_ALL see every claim, oldest first (the work queue).
    /// Filter by `status` (repeat it or comma separate), `assigned` (me, none, any), `officerId` (staff), `minAgeDays` / `maxAgeDays` (whole days since submission, UTC, inclusive), `market`, `line` and `q` (claim number or policy number).
    /// </summary>
    [HttpGet]
    [HasPermission(Permissions.ClaimsRead)]
    [ProducesResponseType(typeof(IReadOnlyList<ClaimDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<ClaimDto>>> List([FromQuery] ClaimListFilter filter, CancellationToken ct) =>
        SendPaged(filter.ToQuery(), ct);

    /// <summary>
    /// Claim counts per status for the tabs: every status is present (zero included) plus a total. Same scope as the list (a claimant counts their own, staff all)
    /// and the same `assigned`, `officerId`, `minAgeDays`, `maxAgeDays`, `market`, `line` and `q` filters; `status` is not a filter here.
    /// </summary>
    [HttpGet("counts")]
    [HasPermission(Permissions.ClaimsRead)]
    [ProducesResponseType(typeof(ClaimCountsDto), StatusCodes.Status200OK)]
    public Task<ActionResult<ClaimCountsDto>> Counts([FromQuery] ClaimCountsFilter filter, CancellationToken ct) =>
        Send(filter.ToQuery(), ct);

    /// <summary>
    /// Full-text search over claims (D-063): claim number, policy number, claimant name, market code and name, line, place and description. Every word
    /// must match; words are typo tolerant and have synonyms (SG/Singapore, car/motor, home/property, ...), but a claim or policy number matches exactly.
    /// Each hit carries `highlights` (why it matched) and the result `facets` (counts by status, market, line). The scope is applied by the search filter
    /// itself: a claimant only ever finds their own claims, staff with CLAIMS_ALL find every claim. `q` is required. Eventually consistent (an indexer
    /// copies rows). 503 `search.unavailable` when the search engine is not configured or unreachable.
    /// </summary>
    [HttpGet("search")]
    [HasPermission(Permissions.ClaimsRead)]
    [ProducesResponseType(typeof(ClaimSearchResultDto), StatusCodes.Status200OK)]
    public Task<ActionResult<ClaimSearchResultDto>> Search([FromQuery] string? q, [FromQuery] int? limit, CancellationToken ct) =>
        Send(new SearchClaimsQuery(q, limit), ct);

    /// <summary>Workload board: per officer the open claims by status, the oldest open age, decisions in the last 7 and 30 days and the median days to decide; the unassigned count; ageing bands 0-2, 3-7, 8-14, 15+ days.</summary>
    [HttpGet("workload")]
    [HasPermission(Permissions.ClaimsWorkload)]
    [ProducesResponseType(typeof(ClaimWorkloadDto), StatusCodes.Status200OK)]
    public Task<ActionResult<ClaimWorkloadDto>> Workload(CancellationToken ct) =>
        Send(new GetClaimWorkloadQuery(), ct);

    /// <summary>Exposure: per market, line and currency the open count, the outstanding reserve of open claims and the approved-not-settled amount. Totals are per currency; amounts are never added across currencies.</summary>
    [HttpGet("exposure")]
    [HasPermission(Permissions.ClaimsExposure)]
    [ProducesResponseType(typeof(ClaimExposureDto), StatusCodes.Status200OK)]
    public Task<ActionResult<ClaimExposureDto>> Exposure(CancellationToken ct) =>
        Send(new GetClaimExposureQuery(), ct);

    /// <summary>Get one claim with its open question, if any. Your own claim, or any claim with CLAIMS_ALL; anything else is 404.</summary>
    [HttpGet("{id}")]
    [HasPermission(Permissions.ClaimsRead)]
    [ProducesResponseType(typeof(ClaimDto), StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound, "claim.not_found")]
    public Task<ActionResult<ClaimDto>> Get(ClaimId id, CancellationToken ct) =>
        Send(new GetClaimByIdQuery(id), ct);

    /// <summary>The claim's history, oldest first, with claimant-safe notes. Same visibility as the claim.</summary>
    [HttpGet("{id}/timeline")]
    [HasPermission(Permissions.ClaimsRead)]
    [ProducesResponseType(typeof(IReadOnlyList<ClaimTimelineEntryDto>), StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound, "claim.not_found")]
    public Task<ActionResult<IReadOnlyList<ClaimTimelineEntryDto>>> Timeline(ClaimId id, CancellationToken ct) =>
        Send(new GetClaimTimelineQuery(id), ct);

    /// <summary>Every question asked on the claim, newest first, with who asked, when, the answer and when it was answered. A claimant sees staff as "Claims team". Same visibility as the claim.</summary>
    [HttpGet("{id}/info-requests")]
    [HasPermission(Permissions.ClaimsRead)]
    [ProducesResponseType(typeof(IReadOnlyList<ClaimInfoRequestEntryDto>), StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound, "claim.not_found")]
    public Task<ActionResult<IReadOnlyList<ClaimInfoRequestEntryDto>>> InfoRequests(ClaimId id, CancellationToken ct) =>
        Send(new GetClaimInfoRequestsQuery(id), ct);

    /// <summary>
    /// The sample letter of a decided claim as a PDF download: a settlement letter for Approved and Settled claims, a decision letter for Rejected ones.
    /// Same visibility as the claim (a claimant's copy never shows the reserve or staff names). Made per request, never stored; labelled a sample, not a legal letter.
    /// </summary>
    [HttpGet("{id}/letter")]
    [HasPermission(Permissions.ClaimsRead)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, ClaimLetterFile.ContentType)]
    [ProblemResponse(StatusCodes.Status404NotFound, "claim.not_found")]
    [ProblemResponse(StatusCodes.Status409Conflict, "claim.letter_not_available")]
    public async Task<IActionResult> Letter(ClaimId id, CancellationToken ct) =>
        await Sender.Send(new GetClaimLetterQuery(id), ct) switch
        {
            { IsFailed: true } failed => Problem(failed.Errors[0]),
            var letter => File(letter.Value.Content, ClaimLetterFile.ContentType, letter.Value.FileName),
        };

    /// <summary>Pick up a Submitted claim: it becomes UnderReview and you become its assigned officer.</summary>
    [HttpPost("{id}/pickup")]
    [HasPermission(Permissions.ClaimsHandle)]
    [IfMatch]
    [ProducesResponseType(typeof(ClaimDto), StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound, "claim.not_found")]
    [ProblemResponse(StatusCodes.Status409Conflict, "claim.invalid_transition")]
    public Task<ActionResult<ClaimDto>> Pickup(ClaimId id, CancellationToken ct) =>
        Send(new PickupClaimCommand(id, IfMatchVersion), ct);

    /// <summary>Set the internal reserve (minor units) while the claim is UnderReview or InfoRequested. Never shown to the claimant. Assigned officer or manager only.</summary>
    [HttpPut("{id}/reserve")]
    [HasPermission(Permissions.ClaimsHandle)]
    [IfMatch]
    [ProducesResponseType(typeof(ClaimDto), StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound, "claim.not_found")]
    [ProblemResponse(StatusCodes.Status409Conflict, "claim.invalid_transition", "claim.not_assigned_to_you")]
    public Task<ActionResult<ClaimDto>> SetReserve(ClaimId id, SetClaimReserveRequest body, CancellationToken ct) =>
        Send(new ReserveClaimCommand(id, body.AmountMinor, IfMatchVersion), ct);

    /// <summary>Ask the claimant a question: UnderReview becomes InfoRequested. One open question at a time. Assigned officer or manager only.</summary>
    [HttpPost("{id}/info-requests")]
    [HasPermission(Permissions.ClaimsHandle)]
    [IfMatch]
    [ProducesResponseType(typeof(ClaimDto), StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound, "claim.not_found")]
    [ProblemResponse(StatusCodes.Status409Conflict, "claim.invalid_transition", "claim.not_assigned_to_you")]
    public Task<ActionResult<ClaimDto>> RequestInfo(ClaimId id, RequestClaimInfoRequest body, CancellationToken ct) =>
        Send(new RequestClaimInfoCommand(id, body.Question, IfMatchVersion), ct);

    /// <summary>Answer the open question on your own claim: InfoRequested becomes UnderReview.</summary>
    [HttpPost("{id}/info-requests/{requestId}/response")]
    [HasPermission(Permissions.ClaimsRespond)]
    [IfMatch]
    [ProducesResponseType(typeof(ClaimDto), StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound, "claim.not_found", "claim.info_request_not_found")]
    [ProblemResponse(StatusCodes.Status409Conflict, "claim.invalid_transition")]
    public Task<ActionResult<ClaimDto>> Respond(ClaimId id, ClaimInfoRequestId requestId, AnswerClaimInfoRequest body, CancellationToken ct) =>
        Send(new RespondToClaimInfoCommand(id, requestId, body.Answer, IfMatchVersion), ct);

    /// <summary>
    /// Approve or reject a claim under review. Approving needs a reserve (409 `claim.reserve_required`) and an amount with 0 &lt; amount &lt;= reserve
    /// (400 `claim.amount_invalid`); the reason is shown to the claimant. Assigned officer or manager only.
    /// </summary>
    [HttpPost("{id}/decision")]
    [HasPermission(Permissions.ClaimsHandle)]
    [IfMatch]
    [ProducesResponseType(typeof(ClaimDto), StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound, "claim.not_found")]
    [ProblemResponse(StatusCodes.Status409Conflict, "claim.invalid_transition", "claim.reserve_required", "claim.not_assigned_to_you")]
    public Task<ActionResult<ClaimDto>> Decide(ClaimId id, DecideClaimRequest body, CancellationToken ct) =>
        Send(new DecideClaimCommand(id, body.Outcome, body.AmountMinor, body.Reason, IfMatchVersion), ct);

    /// <summary>Settle an Approved claim. Needs CLAIMS_SETTLE (managers).</summary>
    [HttpPost("{id}/settle")]
    [HasPermission(Permissions.ClaimsSettle)]
    [IfMatch]
    [ProducesResponseType(typeof(ClaimDto), StatusCodes.Status200OK)]
    [ProblemResponse(StatusCodes.Status404NotFound, "claim.not_found")]
    [ProblemResponse(StatusCodes.Status409Conflict, "claim.invalid_transition")]
    public Task<ActionResult<ClaimDto>> Settle(ClaimId id, CancellationToken ct) =>
        Send(new SettleClaimCommand(id, IfMatchVersion), ct);
}
