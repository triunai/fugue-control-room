namespace Fugue.Infrastructure.Search;

/// <summary><c>Search:Meilisearch:</c> configuration (D-058). Search is OFF while <see cref="Url"/> is empty.</summary>
public sealed class MeilisearchOptions
{
    public const string Section = "Search:Meilisearch";

    public string? Url { get; set; }

    public string? ApiKey { get; set; }

    /// <summary>Rows read from <c>audit_log</c> and sent to the index per batch.</summary>
    public int IndexerBatchSize { get; set; } = 500;

    /// <summary>Pause between indexer cycles once caught up.</summary>
    public int IndexerIntervalSeconds { get; set; } = 5;

    public bool Enabled => !string.IsNullOrWhiteSpace(Url);
}
