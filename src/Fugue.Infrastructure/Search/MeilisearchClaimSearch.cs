using System.Globalization;
using FluentResults;
using Fugue.Application.Features.Claims;
using Fugue.Domain.Results;
using Meilisearch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ClaimSearchOutcome = FluentResults.Result<Fugue.Application.Features.Claims.ClaimSearchResultDto>;

namespace Fugue.Infrastructure.Search;

/// <summary>
/// The Meilisearch adapter for claims (D-063): owns the <c>claims</c> and <c>claims_checkpoint</c> indexes, applies their settings once per process,
/// serves <see cref="IClaimSearch"/> and gives the indexer its upsert and checkpoint calls. The caller's scope is enforced here, in the engine query
/// (<see cref="BuildFilter"/>), never after the fact. Any engine failure surfaces as <c>search.unavailable</c>.
/// </summary>
internal sealed partial class MeilisearchClaimSearch(IOptions<MeilisearchOptions> options, ILogger<MeilisearchClaimSearch> logger) : IClaimSearch, IDisposable
{
    internal const string ClaimsIndex = "claims";
    internal const string CheckpointIndex = "claims_checkpoint";

    private static readonly string[] Searchable = ["number", "policyNumber", "incidentLocation", "incidentDescription", "claimantName"];

    private readonly MeilisearchClient _client = new(options.Value.Url, options.Value.ApiKey);
    private readonly SemaphoreSlim _ensureLock = new(1, 1);
    private bool _ready;

    public async Task<ClaimSearchOutcome> SearchAsync(ClaimSearchFilter filter, CancellationToken ct)
    {
        try
        {
            await EnsureIndexesAsync(ct);
            var query = new SearchQuery { Limit = filter.Limit, Filter = BuildFilter(filter) };
            ISearchable<ClaimDocument> result = await _client.Index(ClaimsIndex).SearchAsync<ClaimDocument>(filter.Query, query, ct);
            int total = result is SearchResult<ClaimDocument> offset ? offset.EstimatedTotalHits : result.Hits.Count;
            return Result.Ok(new ClaimSearchResultDto([.. result.Hits.Select(d => d.ToHit())], total));
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

    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    /// <summary>The checkpoint last stored, or null on a fresh engine (the indexer then starts from the beginning).</summary>
    internal async Task<(DateTimeOffset UpdatedAt, Guid Id)?> ReadCheckpointAsync(CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        try
        {
            ClaimCheckpoint doc = await _client.Index(CheckpointIndex).GetDocumentAsync<ClaimCheckpoint>(ClaimCheckpoint.DocumentId, cancellationToken: ct);
            return (DateTimeOffset.Parse(doc.UpdatedAt, CultureInfo.InvariantCulture), Guid.Parse(doc.ClaimId));
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
            var settings = new Settings
            {
                SearchableAttributes = Searchable,
                FilterableAttributes = ["claimantUserId", "assignedOfficerId", "market", "line", "status"],
                SortableAttributes = ["submittedAtMs"],
            };
            await WaitAsync(await _client.Index(ClaimsIndex).UpdateSettingsAsync(settings, ct), ct);
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Claim search failed")]
    private partial void LogSearchFailed(Exception exception);
}

/// <summary>Registered when no search URL is configured: every search answers <c>search.unavailable</c> (503), nothing else is affected.</summary>
internal sealed class DisabledClaimSearch : IClaimSearch
{
    public Task<ClaimSearchOutcome> SearchAsync(ClaimSearchFilter filter, CancellationToken ct) =>
        Task.FromResult(Result.Fail<ClaimSearchResultDto>(FugueErrors.Unavailable("search.unavailable", "Claim search is not configured.")));
}
