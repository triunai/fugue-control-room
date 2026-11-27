using System.Globalization;
using FluentResults;
using Fugue.Application.Features.Audit.Repos;
using Fugue.Domain.Results;
using Meilisearch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AuditSearchOutcome = FluentResults.Result<Fugue.Application.Features.Audit.Repos.AuditSearchResultDto>;

namespace Fugue.Infrastructure.Search;

/// <summary>
/// The Meilisearch adapter (D-058): owns the <c>audit</c> and <c>audit_checkpoint</c> indexes, applies their settings once per process, serves
/// <see cref="IAuditSearch"/> and gives the indexer its upsert and checkpoint calls. Any engine failure surfaces as <c>search.unavailable</c>.
/// </summary>
internal sealed partial class MeilisearchAuditSearch(IOptions<MeilisearchOptions> options, ILogger<MeilisearchAuditSearch> logger) : IAuditSearch, IDisposable
{
    internal const string AuditIndex = "audit";
    internal const string CheckpointIndex = "audit_checkpoint";

    private static readonly string[] Highlighted = ["action", "entityType", "entityId", "actorDisplay", "errorCode", "errorMessage", "routeTemplate"];

    private readonly MeilisearchClient _client = new(options.Value.Url, options.Value.ApiKey);
    private readonly SemaphoreSlim _ensureLock = new(1, 1);
    private bool _ready;

    public async Task<AuditSearchOutcome> SearchAsync(AuditSearchFilter filter, CancellationToken ct)
    {
        try
        {
            await EnsureIndexesAsync(ct);
            var query = new SearchQuery
            {
                Limit = filter.Limit,
                Filter = BuildFilter(filter),
                Sort = ["occurredAtMs:desc"],
                AttributesToHighlight = filter.Query is null ? null : Highlighted,
                HighlightPreTag = "<em>",
                HighlightPostTag = "</em>",
            };

            ISearchable<AuditDocument> result = await _client.Index(AuditIndex).SearchAsync<AuditDocument>(filter.Query ?? string.Empty, query, ct);
            int total = result is SearchResult<AuditDocument> offset ? offset.EstimatedTotalHits : result.Hits.Count;
            return Result.Ok(new AuditSearchResultDto([.. result.Hits.Select(ToHit)], total));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSearchFailed(ex);
            return Result.Fail<AuditSearchResultDto>(FugueErrors.Unavailable("search.unavailable", "Audit search is temporarily unavailable."));
        }
    }

    /// <summary>The checkpoint last stored, or null on a fresh engine (the indexer then starts from the beginning).</summary>
    internal async Task<(DateTimeOffset OccurredAt, Guid Id)?> ReadCheckpointAsync(CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        try
        {
            AuditCheckpoint doc = await _client.Index(CheckpointIndex).GetDocumentAsync<AuditCheckpoint>(AuditCheckpoint.DocumentId, cancellationToken: ct);
            return (DateTimeOffset.Parse(doc.OccurredAt, CultureInfo.InvariantCulture), Guid.Parse(doc.AuditId));
        }
        catch (MeilisearchApiError ex) when (ex.Code == "document_not_found")
        {
            return null;
        }
    }

    /// <summary>Upserts the batch by id, waits until the engine has applied it, then moves the checkpoint (at-least-once).</summary>
    internal async Task UpsertAsync(IReadOnlyList<AuditDocument> documents, (DateTimeOffset OccurredAt, Guid Id) checkpoint, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await WaitAsync(await _client.Index(AuditIndex).AddDocumentsAsync(documents, "id", ct), ct);
        var state = new AuditCheckpoint
        {
            OccurredAt = checkpoint.OccurredAt.ToString("O", CultureInfo.InvariantCulture),
            AuditId = checkpoint.Id.ToString(),
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

            await WaitAsync(await _client.CreateIndexAsync(AuditIndex, "id", ct), ct, tolerateExisting: true);
            await WaitAsync(await _client.CreateIndexAsync(CheckpointIndex, "id", ct), ct, tolerateExisting: true);
            var settings = new Settings
            {
                SearchableAttributes = Highlighted,
                FilterableAttributes = ["entityType", "action", "outcome", "actorUserId", "occurredAtMs"],
                SortableAttributes = ["occurredAtMs"],
            };
            await WaitAsync(await _client.Index(AuditIndex).UpdateSettingsAsync(settings, ct), ct);
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

    private static string? BuildFilter(AuditSearchFilter f)
    {
        var parts = new List<string>();
        if (f.EntityType is not null)
        {
            parts.Add($"entityType = {Quote(f.EntityType)}");
        }

        if (f.Action is not null)
        {
            parts.Add($"action = {Quote(f.Action)}");
        }

        if (f.Outcome is not null)
        {
            parts.Add($"outcome = {Quote(f.Outcome)}");
        }

        if (f.From is { } from)
        {
            parts.Add($"occurredAtMs >= {from.ToUnixTimeMilliseconds()}");
        }

        if (f.To is { } to)
        {
            parts.Add($"occurredAtMs < {to.ToUnixTimeMilliseconds()}");
        }

        return parts.Count == 0 ? null : string.Join(" AND ", parts);
    }

    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static AuditSearchHitDto ToHit(AuditDocument d) => new(
        Guid.Parse(d.Id), DateTimeOffset.Parse(d.OccurredAt, CultureInfo.InvariantCulture), d.ActorUserId is null ? null : Guid.Parse(d.ActorUserId), d.ActorDisplay,
        d.Action, d.EntityType, d.EntityId, d.Outcome, d.ErrorCode, d.ErrorMessage, d.RouteTemplate, Highlights(d.Formatted));

    private static Dictionary<string, string>? Highlights(AuditDocument? f)
    {
        if (f is null)
        {
            return null;
        }

        var map = new Dictionary<string, string>();
        void Add(string name, string? value)
        {
            if (value is not null && value.Contains("<em>", StringComparison.Ordinal))
            {
                map[name] = value;
            }
        }

        Add("action", f.Action);
        Add("entityType", f.EntityType);
        Add("entityId", f.EntityId);
        Add("actorDisplay", f.ActorDisplay);
        Add("errorCode", f.ErrorCode);
        Add("errorMessage", f.ErrorMessage);
        Add("routeTemplate", f.RouteTemplate);
        return map;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Audit search failed")]
    private partial void LogSearchFailed(Exception exception);
}

/// <summary>Registered when no search URL is configured: every search answers <c>search.unavailable</c> (503), nothing else is affected.</summary>
internal sealed class DisabledAuditSearch : IAuditSearch
{
    public Task<AuditSearchOutcome> SearchAsync(AuditSearchFilter filter, CancellationToken ct) =>
        Task.FromResult(Result.Fail<AuditSearchResultDto>(FugueErrors.Unavailable("search.unavailable", "Audit search is not configured.")));
}
