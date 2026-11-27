using FluentResults;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Audit;
using Fugue.Domain.Events;
using Fugue.Domain.Ids;
using Fugue.Domain.Results;

namespace Fugue.Domain.Claims;

/// <summary>
/// An insurance claim and its state machine (W-020, D-053). Every transition is a method that returns a <see cref="Result"/>: an illegal move is
/// <c>claim.invalid_transition</c> (Conflict) and changes nothing. A legal move updates the state, appends a timeline entry (the claimant-visible
/// history) and raises a domain event; the persistence layer writes all three in one transaction.
/// <code>
/// Submitted -> UnderReview (pickup, assigns the officer) -> InfoRequested -> UnderReview (claimant answers)
/// UnderReview -> Approved | Rejected;  Approved -> Settled.   Rejected and Settled are terminal.
/// </code>
/// Money is bigint minor units in the market's currency (<see cref="Markets.CurrencyOf"/>), never floating point.
/// </summary>
public sealed class Claim : IAuditable, IVersioned, IHasDomainEvents
{
    public const string InvalidTransitionCode = "claim.invalid_transition";

    private readonly List<IDomainEvent> _events = [];
    private readonly List<ClaimTimelineEntry> _timeline = [];
    private readonly List<ClaimInfoRequest> _infoRequests = [];

    private Claim()
    {
        Number = string.Empty;
        Currency = string.Empty;
        PolicyNumber = string.Empty;
        IncidentDescription = string.Empty;
        CreatedBy = string.Empty;
        UpdatedBy = string.Empty;
    }

    public ClaimId Id { get; private set; }

    /// <summary>The number the claimant quotes, <see cref="ClaimNumber"/>; unique.</summary>
    public string Number { get; private set; }

    public Market Market { get; private set; }

    /// <summary>ISO 4217, fixed by the market.</summary>
    public string Currency { get; private set; }

    public LineOfBusiness Line { get; private set; }

    public ClaimStatus Status { get; private set; }

    /// <summary>The owner: the user who submitted it. Only they (and <c>CLAIMS_ALL</c> staff) can see it.</summary>
    public UserId ClaimantUserId { get; private set; }

    public UserId? AssignedOfficerId { get; private set; }

    public DateTimeOffset? AssignedAt { get; private set; }

    public string PolicyNumber { get; private set; }

    public DateOnly IncidentDate { get; private set; }

    public string IncidentDescription { get; private set; }

    public string? IncidentLocation { get; private set; }

    public DateTimeOffset SubmittedAt { get; private set; }

    /// <summary>The officer's internal estimate of what the claim will cost, minor units. Never shown to the claimant.</summary>
    public long? ReserveMinor { get; private set; }

    public long? ApprovedAmountMinor { get; private set; }

    /// <summary>The officer's reason for the decision; written for the claimant to read.</summary>
    public string? DecisionReason { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public DateTimeOffset? SettledAt { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string UpdatedBy { get; set; }

    /// <summary>Postgres <c>xmin</c> system column; the optimistic-concurrency token (the ETag).</summary>
    public uint RowVersion { get; private set; }

    long IVersioned.Version => RowVersion;

    /// <summary>Timeline rows added by this instance (the history of an already stored claim is not loaded); the repository writes them.</summary>
    [NoAudit]
    public IReadOnlyCollection<ClaimTimelineEntry> Timeline => _timeline;

    /// <summary>The open info request when loaded for an update; the repository loads only the open one.</summary>
    [NoAudit]
    public IReadOnlyCollection<ClaimInfoRequest> InfoRequests => _infoRequests;

    [NoAudit]
    public ClaimInfoRequest? OpenInfoRequest => _infoRequests.FirstOrDefault(r => r.IsOpen);

    IReadOnlyCollection<IDomainEvent> IHasDomainEvents.DomainEvents => _events;

    void IHasDomainEvents.ClearDomainEvents() => _events.Clear();

    public static Claim Submit(
        string number,
        Market market,
        LineOfBusiness line,
        UserId claimant,
        string claimantDisplay,
        string policyNumber,
        DateOnly incidentDate,
        string description,
        string? location,
        DateTimeOffset now)
    {
        var claim = new Claim
        {
            Id = ClaimId.New(),
            Number = number,
            Market = market,
            Currency = Markets.CurrencyOf(market),
            Line = line,
            Status = ClaimStatus.Submitted,
            ClaimantUserId = claimant,
            PolicyNumber = policyNumber.Trim(),
            IncidentDate = incidentDate,
            IncidentDescription = description.Trim(),
            IncidentLocation = string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
            SubmittedAt = now,
        };
        claim.Record(null, now, claimant, claimantDisplay, "Claim submitted.");
        claim._events.Add(new ClaimSubmitted(claim.Id, claim.Number, claim.Status, claim.Market, claim.Line));
        return claim;
    }

    /// <summary>Submitted to UnderReview; the officer who picks it up owns it.</summary>
    public Result Pickup(UserId officer, string officerDisplay, DateTimeOffset now)
    {
        if (Status != ClaimStatus.Submitted)
        {
            return Result.Fail(InvalidTransition(ClaimStatus.UnderReview));
        }

        AssignedOfficerId = officer;
        AssignedAt = now;
        Move(ClaimStatus.UnderReview, now, officer, officerDisplay, "A claims officer has started reviewing your claim.");
        _events.Add(new ClaimAssigned(Id, Number, Status, Market));
        return Result.Ok();
    }

    /// <summary>UnderReview to InfoRequested. The question is shown to the claimant, so it is the timeline note.</summary>
    public Result RequestInfo(string question, UserId officer, string officerDisplay, DateTimeOffset now)
    {
        if (Status != ClaimStatus.UnderReview)
        {
            return Result.Fail(InvalidTransition(ClaimStatus.InfoRequested));
        }

        var request = ClaimInfoRequest.Open(Id, question.Trim(), officer, now);
        _infoRequests.Add(request);
        Move(ClaimStatus.InfoRequested, now, officer, officerDisplay, $"More information needed: {request.Question}");
        _events.Add(new ClaimInfoRequested(Id, Number, Status, Market));
        return Result.Ok();
    }

    /// <summary>
    /// InfoRequested to UnderReview, by the claimant answering the open question. An id that is not the open request is a 404
    /// (<c>claim.info_request_not_found</c>), so a claimant cannot probe other requests.
    /// </summary>
    public Result ProvideInfo(ClaimInfoRequestId requestId, string answer, UserId claimant, string claimantDisplay, DateTimeOffset now)
    {
        if (Status != ClaimStatus.InfoRequested)
        {
            return Result.Fail(InvalidTransition(ClaimStatus.UnderReview));
        }

        ClaimInfoRequest? open = OpenInfoRequest;
        if (open is null || open.Id != requestId)
        {
            return Result.Fail(FugueErrors.NotFound("claim.info_request_not_found", "There is no open information request with that id on this claim."));
        }

        string text = answer.Trim();
        open.Answer(text, now);
        Move(ClaimStatus.UnderReview, now, claimant, claimantDisplay, $"Information provided: {text}");
        _events.Add(new ClaimInfoProvided(Id, Number, Status, Market));
        return Result.Ok();
    }

    /// <summary>Sets the internal reserve while the claim is being worked (UnderReview or InfoRequested). Not a status change, so no timeline row (the reserve is internal), but it raises <c>claim.reserve.set.v1</c> (no amount).</summary>
    public Result SetReserve(long amountMinor)
    {
        if (Status is not (ClaimStatus.UnderReview or ClaimStatus.InfoRequested))
        {
            return Result.Fail(FugueErrors.Conflict(InvalidTransitionCode, $"The reserve cannot be set while the claim is {Status}."));
        }

        if (amountMinor <= 0)
        {
            return Result.Fail(AmountInvalid("The reserve must be greater than zero."));
        }

        ReserveMinor = amountMinor;
        _events.Add(new ClaimReserveSet(Id, Number, Status, Market));
        return Result.Ok();
    }

    /// <summary>UnderReview to Approved. Needs a reserve, and <c>0 &lt; amount &lt;= reserve</c>.</summary>
    public Result Approve(long amountMinor, string reason, UserId officer, string officerDisplay, DateTimeOffset now)
    {
        if (Status != ClaimStatus.UnderReview)
        {
            return Result.Fail(InvalidTransition(ClaimStatus.Approved));
        }

        if (ReserveMinor is not { } reserve)
        {
            return Result.Fail(FugueErrors.Conflict("claim.reserve_required", "A reserve must be set before a claim can be approved."));
        }

        if (amountMinor <= 0 || amountMinor > reserve)
        {
            return Result.Fail(AmountInvalid("The approved amount must be greater than zero and not more than the reserve."));
        }

        ApprovedAmountMinor = amountMinor;
        Decide(ClaimStatus.Approved, reason, officer, officerDisplay, now, "Your claim has been approved.");
        return Result.Ok();
    }

    /// <summary>UnderReview to Rejected. No reserve is needed.</summary>
    public Result Reject(string reason, UserId officer, string officerDisplay, DateTimeOffset now)
    {
        if (Status != ClaimStatus.UnderReview)
        {
            return Result.Fail(InvalidTransition(ClaimStatus.Rejected));
        }

        Decide(ClaimStatus.Rejected, reason, officer, officerDisplay, now, "Your claim has been rejected.");
        return Result.Ok();
    }

    /// <summary>Approved to Settled.</summary>
    public Result Settle(UserId actor, string actorDisplay, DateTimeOffset now)
    {
        if (Status != ClaimStatus.Approved)
        {
            return Result.Fail(InvalidTransition(ClaimStatus.Settled));
        }

        SettledAt = now;
        Move(ClaimStatus.Settled, now, actor, actorDisplay, "Your claim has been settled.");
        _events.Add(new ClaimSettled(Id, Number, Status, Market));
        return Result.Ok();
    }

    private void Decide(ClaimStatus to, string reason, UserId officer, string officerDisplay, DateTimeOffset now, string headline)
    {
        string text = reason.Trim();
        DecisionReason = text;
        DecidedAt = now;
        Move(to, now, officer, officerDisplay, $"{headline} {text}");
        _events.Add(new ClaimDecided(Id, Number, Status, Market));
    }

    private void Move(ClaimStatus to, DateTimeOffset now, UserId actor, string actorDisplay, string note)
    {
        ClaimStatus from = Status;
        Status = to;
        Record(from, now, actor, actorDisplay, note);
    }

    /// <summary>
    /// A just-submitted claim that staff keyed in from a file import (item 11a) says so on its timeline: who imported it, from which file, and the
    /// note the import carries (a chat transcript, an email header). No status change. The claimant reads it with the actor shown as "Claims team".
    /// </summary>
    public void RecordImport(UserId staff, string staffDisplay, string note, DateTimeOffset now) =>
        Record(Status, now, staff, staffDisplay, note);

    private void Record(ClaimStatus? from, DateTimeOffset now, UserId actor, string actorDisplay, string note) =>
        _timeline.Add(ClaimTimelineEntry.Record(Id, now, from, Status, actor, actorDisplay, note));

    private FugueError InvalidTransition(ClaimStatus to) =>
        FugueErrors.Conflict(InvalidTransitionCode, $"A claim cannot move from {Status} to {to}.");

    private static FugueError AmountInvalid(string message) =>
        FugueErrors.Validation("claim.amount_invalid", message, new Dictionary<string, string[]> { ["amountMinor"] = [message] });
}
