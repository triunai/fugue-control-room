using System.Globalization;
using System.Text.Json.Serialization;
using Fugue.Application.Features.Claims;
using Fugue.Domain.Claims;
using Fugue.Domain.Ids;

namespace Fugue.Infrastructure.Search;

/// <summary>
/// One <c>claims</c> row as the search index holds it (D-063). <c>claimantUserId</c> and <c>assignedOfficerId</c> are filter-only: the scope filter
/// needs them and no response ever carries them. The reserve and the decision are deliberately not indexed. <c>_formatted</c> is filled by the engine on a
/// highlighted search (<see cref="ClaimHighlightFields"/>).
/// </summary>
internal sealed class ClaimDocument
{
    /// <summary>The document shape version. Bump it when a field is added or changed: a checkpoint of another version restarts the indexer from the
    /// beginning once, so every claim is re-sent in the new shape (no migration, no manual reindex).</summary>
    public const int Shape = 2;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("number")]
    public string Number { get; set; } = string.Empty;

    [JsonPropertyName("market")]
    public string Market { get; set; } = string.Empty;

    /// <summary>The market's name ("Singapore"), so an officer can type the place as well as the code.</summary>
    [JsonPropertyName("marketName")]
    public string MarketName { get; set; } = string.Empty;

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

    [JsonPropertyName("_formatted")]
    public ClaimHighlightFields? Formatted { get; set; }

    public static ClaimDocument From(ClaimIndexRow row) => new()
    {
        Id = row.Claim.Id.Value.ToString(),
        Number = row.Claim.Number,
        Market = row.Claim.Market.ToString(),
        MarketName = ClaimIndexSettings.NameOf(row.Claim.Market),
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
        PolicyNumber, IncidentDescription, IncidentLocation, DateTimeOffset.Parse(SubmittedAt, CultureInfo.InvariantCulture), ClaimantName,
        Formatted?.Matched());
}

/// <summary>
/// The highlighted copy of a hit (<c>_formatted</c>): the searchable text fields only, matches wrapped in <c>&lt;em&gt;</c>, the description cropped
/// around the match. A separate type because the engine turns every number in <c>_formatted</c> into a string.
/// </summary>
internal sealed class ClaimHighlightFields
{
    [JsonPropertyName("number")]
    public string? Number { get; set; }

    [JsonPropertyName("policyNumber")]
    public string? PolicyNumber { get; set; }

    [JsonPropertyName("claimantName")]
    public string? ClaimantName { get; set; }

    [JsonPropertyName("marketName")]
    public string? MarketName { get; set; }

    [JsonPropertyName("market")]
    public string? Market { get; set; }

    [JsonPropertyName("line")]
    public string? Line { get; set; }

    [JsonPropertyName("incidentLocation")]
    public string? IncidentLocation { get; set; }

    [JsonPropertyName("incidentDescription")]
    public string? IncidentDescription { get; set; }

    /// <summary>Only the fields the query matched, keyed by the hit's field names: this is why the hit is in the list.</summary>
    public Dictionary<string, string> Matched()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        void Add(string name, string? value)
        {
            if (value is not null && value.Contains("<em>", StringComparison.Ordinal))
            {
                map[name] = value;
            }
        }

        Add("number", Number);
        Add("policyNumber", PolicyNumber);
        Add("claimantName", ClaimantName);
        Add("marketName", MarketName);
        Add("market", Market);
        Add("line", Line);
        Add("incidentLocation", IncidentLocation);
        Add("incidentDescription", IncidentDescription);
        return map;
    }
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

    /// <summary>The <see cref="ClaimDocument.Shape"/> the indexed documents have. Absent (0) on a checkpoint written before shapes existed.</summary>
    [JsonPropertyName("shape")]
    public int Shape { get; set; }
}
