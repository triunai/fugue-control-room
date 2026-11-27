using System.Globalization;
using System.Text.RegularExpressions;
using FluentResults;
using Fugue.Application.Features.Claims;
using Fugue.Domain.Results;
using Meilisearch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ClaimSearchOutcome = FluentResults.Result<Fugue.Application.Features.Claims.ClaimSearchResultDto>;

namespace Fugue.Infrastructure.Search;

/// <summary>
/// The Meilisearch adapter for claims (D-063): owns the <c>claims</c> and <c>claims_checkpoint</c> indexes, applies their settings once per process
/// and only when the engine's differ (<see cref="ClaimIndexSettings"/>),
/// serves <see cref="IClaimSearch"/> and gives the indexer its upsert and checkpoint calls. The caller's scope is enforced here, in the engine query
/// (<see cref="BuildFilter"/>), never after the fact. Any engine failure surfaces as <c>search.unavailable</c>.
/// </summary>
internal sealed partial class MeilisearchClaimSearch(IOptions<MeilisearchOptions> options, ILogger<MeilisearchClaimSearch> logger) : IClaimSearch, IDisposable
{
    internal const string ClaimsIndex = "claims";
    internal const string CheckpointIndex = "claims_checkpoint";

    private readonly MeilisearchClient _client = new(options.Value.Url, options.Value.ApiKey);
    private readonly SemaphoreSlim _ensureLock = new(1, 1);
    private bool _ready;

    public async Task<ClaimSearchOutcome> SearchAsync(ClaimSearchFilter filter, CancellationToken ct)
    {
        try
        {
            await EnsureIndexesAsync(ct);
            var query = new SearchQuery
            {
                Limit = filter.Limit,
                Filter = BuildFilter(filter),
                MatchingStrategy = "all", // every word must match (through a synonym or a typo): "SG motor" means Singapore AND motor
                AttributesToHighlight = ClaimIndexSettings.Highlighted,
                AttributesToCrop = ["incidentDescription"],
                CropLength = 24,
                HighlightPreTag = "<em>",
                HighlightPostTag = "</em>",
                Facets = ClaimIndexSettings.Facets,
            };
            ISearchable<ClaimDocument> result = await _client.Index(ClaimsIndex).SearchAsync<ClaimDocument>(BuildQuery(filter.Query), query, ct);
            int total = result is SearchResult<ClaimDocument> offset ? offset.EstimatedTotalHits : result.Hits.Count;
            return Result.Ok(new ClaimSearchResultDto([.. result.Hits.Select(d => d.ToHit())], total, Facets(result.FacetDistribution)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSearchFailed(ex);
            return Result.Fail<ClaimSearchResultDto>(FugueErrors.Unavailable("search.unavailable", "Claim search is temporarily unavailable."));
        }
    }

    /// <summary>The engine filter for a search: a claimant's claims only when a claimant id is set (the scope), nothing for staff.</summary>
    internal static string? BuildFilter(ClaimSearchFilter f) =>
        f.ClaimantUserId is { } claimant ? $"claimantUserId = {Quote(claimant.ToString())}" : null;

    /// <summary>
    /// The engine query for the typed text. A token that carries a digit and is 5+ characters long (a claim number, a policy number, a sequence) is sent
    /// as a phrase, which the engine matches exactly: no typo and no prefix, so a mistyped number finds nothing instead of its neighbours. A claim number
    /// still being typed (<c>CLM-</c> but not yet complete) stays a prefix so the list narrows as you type. Text the caller already quoted is sent as is.
    /// </summary>
    internal static string BuildQuery(string q)
    {
        if (q.Contains('"', StringComparison.Ordinal))
        {
            return q;
        }

        return string.Join(' ', q.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(t => IsIdentifier(t) ? "\"" + t + "\"" : t));
    }

    private static bool IsIdentifier(string token) =>
        token.Length >= 5 && token.Any(char.IsAsciiDigit)
        && !(token.StartsWith("CLM-", StringComparison.OrdinalIgnoreCase) && !CompleteClaimNumber().IsMatch(token));

    [GeneratedRegex(@"^CLM-[A-Z]{2}-\d{4}-\d{6,}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CompleteClaimNumber();

    private static Dictionary<string, IReadOnlyDictionary<string, int>>? Facets(IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>? distribution) =>
        distribution?.ToDictionary(f => f.Key, f => f.Value, StringComparer.Ordinal);

    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    /// <summary>
    /// The checkpoint last stored, or null on a fresh engine or when the indexed documents have an older <see cref="ClaimDocument.Shape"/> (the indexer then
    /// starts from the beginning and re-sends every claim).
    /// </summary>
    internal async Task<(DateTimeOffset UpdatedAt, Guid Id)?> ReadCheckpointAsync(CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        try
        {
            ClaimCheckpoint doc = await _client.Index(CheckpointIndex).GetDocumentAsync<ClaimCheckpoint>(ClaimCheckpoint.DocumentId, cancellationToken: ct);
            return doc.Shape == ClaimDocument.Shape ? (DateTimeOffset.Parse(doc.UpdatedAt, CultureInfo.InvariantCulture), Guid.Parse(doc.ClaimId)) : null;
        }
        catch (MeilisearchApiError ex) when (ex.Code == "document_not_found")
        {
            return null;
        }
    }

    /// <summary>Upserts the batch by id, waits until the engine has applied it, then moves the checkpoint (at-least-once).</summary>
    internal async Task UpsertAsync(IReadOnlyList<ClaimDocument> documents, (DateTimeOffset UpdatedAt, Guid Id) checkpoint, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await WaitAsync(await _client.Index(ClaimsIndex).AddDocumentsAsync(documents, "id", ct), ct);
        var state = new ClaimCheckpoint
        {
            UpdatedAt = checkpoint.UpdatedAt.ToString("O", CultureInfo.InvariantCulture),
            ClaimId = checkpoint.Id.ToString(),
            Shape = ClaimDocument.Shape,
        };
        await WaitAsync(await _client.Index(CheckpointIndex).AddDocumentsAsync([state], "id", ct), ct);
    }

    private async Task EnsureIndexesAsync(CancellationToken ct)
    {
        if (_ready)
        {
            return;
        }

        await _ensureLock.WaitAsync(ct);
        try
        {
            if (_ready)
            {
                return;
            }

            await WaitAsync(await _client.CreateIndexAsync(ClaimsIndex, "id", ct), ct, tolerateExisting: true);
            await WaitAsync(await _client.CreateIndexAsync(CheckpointIndex, "id", ct), ct, tolerateExisting: true);
            Meilisearch.Index index = _client.Index(ClaimsIndex);
            Settings desired = ClaimIndexSettings.Desired();
            if (ClaimIndexSettings.Matches(await index.GetSettingsAsync(ct), desired))
            {
                LogSettingsUnchanged();
            }
            else
            {
                // A settings update re-indexes every document, so it is sent only when something this app owns differs.
                await WaitAsync(await index.UpdateSettingsAsync(desired, ct), ct);
                LogSettingsApplied();
            }

            _ready = true;
        }
        finally
        {
            _ensureLock.Release();
        }
    }

    public void Dispose() => _ensureLock.Dispose();

    private async Task WaitAsync(TaskInfo info, CancellationToken ct, bool tolerateExisting = false)
    {
        TaskResource done = await _client.WaitForTaskAsync(info.TaskUid, timeoutMs: 30_000.0, intervalMs: 25, cancellationToken: ct);
        if (done.Status == TaskInfoStatus.Failed && !(tolerateExisting && done.Error?["code"] == "index_already_exists"))
        {
            throw new InvalidOperationException($"Meilisearch task {done.Uid} failed: {done.Error?["code"] ?? "unknown"}.");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Claim index settings differed from the engine's and were updated")]
    private partial void LogSettingsApplied();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Claim index settings already match; no update sent")]
    private partial void LogSettingsUnchanged();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Claim search failed")]
    private partial void LogSearchFailed(Exception exception);
}

/// <summary>Registered when no search URL is configured: every search answers <c>search.unavailable</c> (503), nothing else is affected.</summary>
internal sealed class DisabledClaimSearch : IClaimSearch
{
    public Task<ClaimSearchOutcome> SearchAsync(ClaimSearchFilter filter, CancellationToken ct) =>
        Task.FromResult(Result.Fail<ClaimSearchResultDto>(FugueErrors.Unavailable("search.unavailable", "Claim search is not configured.")));
}
