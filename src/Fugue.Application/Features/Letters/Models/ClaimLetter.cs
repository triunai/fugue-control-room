using System.Globalization;
using Fugue.Application.Features.Claims.Models;
using Fugue.Application.Features.Letters.Helpers;
using Fugue.Domain.Claims;

namespace Fugue.Application.Features.Letters.Models;

/// <summary>Which letter a decided claim gets: Approved and Settled claims a settlement letter, Rejected claims a decision letter.</summary>
public enum ClaimLetterKind
{
    Settlement,
    Decision,
}

/// <summary>One label and value row of the letter's details table.</summary>
public sealed record ClaimLetterRow(string Label, string Value);

/// <summary>
/// The words of a sample claim letter, composed from what the caller may already read (the claim and its timeline, under the caller's scope),
/// so the letter never shows a claimant more than the claim page does: no reserve, and staff read as "Claims team". Rendered by <see cref="ClaimLetterPdf"/>.
/// </summary>
public sealed record ClaimLetter(
    ClaimLetterKind Kind,
    string Title,
    string ClaimNumber,
    string Recipient,
    string LetterDate,
    string Subject,
    string Summary,
    IReadOnlyList<ClaimLetterRow> Details,
    string DecisionReason,
    string IncidentDescription,
    string Signatory,
    string CorrelationId,
    string GeneratedAt)
{
    /// <summary>The label printed on every letter: this is a demonstration, not a legal document.</summary>
    public const string SampleLabel = "Sample document, not a legal letter";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>True for the statuses a letter exists for (a decision has been made).</summary>
    public static bool IsDecided(ClaimStatus status) => status is ClaimStatus.Approved or ClaimStatus.Rejected or ClaimStatus.Settled;

    /// <summary>The download name, e.g. <c>CLM-SG-2026-000123-settlement-letter.pdf</c>.</summary>
    public string FileName => $"{ClaimNumber}-{(Kind == ClaimLetterKind.Settlement ? "settlement" : "decision")}-letter.pdf";

    /// <summary>
    /// Composes the letter for a decided claim. The decision's author is the actor on the timeline row that entered Approved or Rejected (a claimant
    /// reads "Claims team" there); the claimant's name is the staff-only field or, for the claimant themself, their own name on the submit row.
    /// </summary>
    public static ClaimLetter From(ClaimDto claim, IReadOnlyList<ClaimTimelineEntryDto> timeline, string correlationId, DateTimeOffset now)
    {
        if (!IsDecided(claim.Status))
        {
            throw new ArgumentException($"A letter needs a decided claim; this one is {claim.Status}.", nameof(claim));
        }

        TimeSpan offset = TimeSpan.FromHours(Markets.UtcOffsetHours(claim.Market));
        ClaimLetterKind kind = claim.Status == ClaimStatus.Rejected ? ClaimLetterKind.Decision : ClaimLetterKind.Settlement;
        string signatory = timeline.LastOrDefault(e => e.ToStatus is ClaimStatus.Approved or ClaimStatus.Rejected)?.ActorDisplay ?? "Claims team";
        string recipient = claim.ClaimantName ?? timeline.FirstOrDefault(e => e.FromStatus is null)?.ActorDisplay ?? "Policyholder";
        string market = MarketName(claim.Market);
        string? amount = claim.ApprovedAmountMinor is { } minor ? Money(minor, claim.Currency) : null;

        string summary = claim.Status switch
        {
            ClaimStatus.Settled =>
                $"Your claim was approved for {amount} and settled on {Date(claim.SettledAt, offset)}. This letter confirms the settlement.",
            ClaimStatus.Approved =>
                $"We have reviewed your claim and approved it for {amount}. The claim will now be settled for that amount.",
            _ => "We have reviewed your claim carefully and we are unable to accept it. The reason for our decision is set out below.",
        };

        var details = new List<ClaimLetterRow>
        {
            new("Claim number", claim.Number),
            new("Policy number", claim.PolicyNumber),
            new("Line of business", claim.Line.ToString()),
            new("Market", $"{market} ({claim.Currency})"),
            new("Incident date", Date(claim.IncidentDate)),
            new("Incident location", claim.IncidentLocation ?? "Not given"),
            new("Submitted", Date(claim.SubmittedAt, offset)),
            new("Decision", claim.Status == ClaimStatus.Rejected ? "Rejected" : "Approved"),
            new("Decided on", Date(claim.DecidedAt, offset)),
        };
        if (amount is not null)
        {
            details.Add(new ClaimLetterRow("Approved amount", amount));
        }

        if (claim.SettledAt is not null)
        {
            details.Add(new ClaimLetterRow("Settled on", Date(claim.SettledAt, offset)));
        }

        details.Add(new ClaimLetterRow("Decided by", signatory));

        return new ClaimLetter(
            kind,
            kind == ClaimLetterKind.Settlement ? "Settlement letter" : "Decision on your claim",
            claim.Number,
            recipient,
            Date(now, offset),
            $"Claim {claim.Number}, {claim.Line} claim in {market}",
            summary,
            details,
            claim.DecisionReason ?? "No reason was recorded.",
            claim.IncidentDescription,
            signatory,
            correlationId,
            now.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", Invariant));
    }

    /// <summary>
    /// Minor units to "SGD 4,500.00" with integer arithmetic (all six currencies have two decimals, as in the seeder and the console).
    /// An approved amount is always greater than zero (the aggregate enforces it).
    /// </summary>
    public static string Money(long minor, string currency) =>
        string.Create(Invariant, $"{currency} {minor / 100:N0}.{minor % 100:D2}");

    private static string MarketName(Market market) => market switch
    {
        Market.SG => "Singapore",
        Market.MY => "Malaysia",
        Market.TH => "Thailand",
        Market.HK => "Hong Kong",
        Market.ID => "Indonesia",
        Market.PH => "Philippines",
        _ => market.ToString(),
    };

    private static string Date(DateOnly date) => date.ToString("d MMMM yyyy", Invariant);

    private static string Date(DateTimeOffset? at, TimeSpan offset) =>
        at is { } value ? Date(DateOnly.FromDateTime(value.ToOffset(offset).DateTime)) : "Not recorded";
}
