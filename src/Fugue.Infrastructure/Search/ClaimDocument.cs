using System.Globalization;
using System.Text.Json.Serialization;
using Fugue.Application.Features.Claims;
using Fugue.Domain.Claims;
using Fugue.Domain.Ids;

namespace Fugue.Infrastructure.Search;

/// <summary>
/// One <c>claims</c> row as the search index holds it (D-063). <c>claimantUserId</c> and <c>assignedOfficerId</c> are filter-only: the scope filter
/// needs them and no response ever carries them. The reserve and the decision are deliberately not indexed. <c>_formatted</c> is unused (no highlights).
/// </summary>
internal sealed class ClaimDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("number")]
    public string Number { get; set; } = string.Empty;

    [JsonPropertyName("market")]
    public string Market { get; set; } = string.Empty;

    [JsonPropertyName("line")]
    public string Line { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("policyNumber")]
    public string PolicyNumber { get; set; } = string.Empty;

    [JsonPropertyName("incidentDescription")]
    public string IncidentDescription { get; set; } = string.Empty;

    [JsonPropertyName("incidentLocation")]
    public string? IncidentLocation { get; set; }

    [JsonPropertyName("claimantName")]
    public string? ClaimantName { get; set; }

    [JsonPropertyName("claimantUserId")]
    public string ClaimantUserId { get; set; } = string.Empty;

    [JsonPropertyName("assignedOfficerId")]
    public string? AssignedOfficerId { get; set; }

    [JsonPropertyName("submittedAt")]
    public string SubmittedAt { get; set; } = string.Empty;

    /// <summary>Epoch milliseconds, the sortable form of <see cref="SubmittedAt"/>.</summary>
    [JsonPropertyName("submittedAtMs")]
    public long SubmittedAtMs { get; set; }

    public static ClaimDocument From(ClaimIndexRow row) => new()
    {
        Id = row.Claim.Id.Value.ToString(),
        Number = row.Claim.Number,
        Market = row.Claim.Market.ToString(),
        Line = row.Claim.Line.ToString(),
        Status = row.Claim.Status.ToString(),
        Currency = row.Claim.Currency,
        PolicyNumber = row.Claim.PolicyNumber,
        IncidentDescription = row.Claim.IncidentDescription,
        IncidentLocation = row.Claim.IncidentLocation,
        ClaimantName = row.ClaimantName,
        ClaimantUserId = row.Claim.ClaimantUserId.Value.ToString(),
        AssignedOfficerId = row.Claim.AssignedOfficerId?.Value.ToString(),
        SubmittedAt = row.Claim.SubmittedAt.ToString("O", CultureInfo.InvariantCulture),
        SubmittedAtMs = row.Claim.SubmittedAt.ToUnixTimeMilliseconds(),
    };

    public ClaimSearchHitDto ToHit() => new(
        ClaimId.From(Guid.Parse(Id)), Number, Enum.Parse<Market>(Market), Currency, Enum.Parse<LineOfBusiness>(Line), Enum.Parse<ClaimStatus>(Status),
        PolicyNumber, IncidentDescription, IncidentLocation, DateTimeOffset.Parse(SubmittedAt, CultureInfo.InvariantCulture), ClaimantName);
}

/// <summary>A claim plus its claimant's display name, as the indexer reads it.</summary>
internal sealed record ClaimIndexRow(Claim Claim, string? ClaimantName);

/// <summary>The indexer's checkpoint, kept in the engine (index <c>claims_checkpoint</c>) so no migration is needed.</summary>
internal sealed class ClaimCheckpoint
{
    public const string DocumentId = "claims";

    [JsonPropertyName("id")]
    public string Id { get; set; } = DocumentId;

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;

    [JsonPropertyName("claimId")]
    public string ClaimId { get; set; } = string.Empty;
}
