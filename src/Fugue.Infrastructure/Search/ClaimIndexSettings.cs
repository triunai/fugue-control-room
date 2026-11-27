using Fugue.Domain.Claims;
using Meilisearch;

namespace Fugue.Infrastructure.Search;

/// <summary>
/// The <c>claims</c> index settings (D-063, hardened in D-064 item 10) and the one comparison that keeps applying them idempotent: the adapter reads
/// the live settings at startup and updates only when <see cref="Matches"/> says they differ, so a restart never re-indexes for nothing.
/// <para>
/// Typo tolerance is NOT switched off with <c>typoTolerance.disableOnAttributes</c>: on the pinned engine (v1.12.8) a word that only appears in such an
/// attribute of a document added after the setting is never found, so a new claim could not be looked up by its number at all (reproduced against the
/// image; fixed by v1.13.3). Exact numbers come from the query instead (<see cref="MeilisearchClaimSearch.BuildQuery"/>: identifier tokens are sent as phrases).
/// </para>
/// </summary>
internal static class ClaimIndexSettings
{
    /// <summary>Attribute order is ranking order: identifiers first, then who, where and what.</summary>
    internal static readonly string[] Searchable =
        ["number", "policyNumber", "claimantName", "marketName", "market", "line", "incidentLocation", "incidentDescription"];

    internal static readonly string[] Filterable = ["claimantUserId", "assignedOfficerId", "market", "line", "status"];

    internal static readonly string[] Sortable = ["submittedAtMs"];

    /// <summary>The fields a search highlights (every searchable field: the highlight is the "why it matched").</summary>
    internal static readonly string[] Highlighted = Searchable;

    /// <summary>The facets a search returns counts for (under the caller's scope, like the hits).</summary>
    internal static readonly string[] Facets = ["status", "market", "line"];

    /// <summary>Market code to the name an officer types. Only the six markets the app has.</summary>
    internal static string NameOf(Market market) => market switch
    {
        Market.SG => "Singapore",
        Market.MY => "Malaysia",
        Market.TH => "Thailand",
        Market.HK => "Hong Kong",
        Market.ID => "Indonesia",
        Market.PH => "Philippines",
        _ => throw new ArgumentOutOfRangeException(nameof(market)),
    };

    /// <summary>Two-way synonyms, lower case (the engine matches case-insensitively): each market code and its name, and the everyday word for each line.</summary>
    internal static Dictionary<string, IEnumerable<string>> Synonyms()
    {
        var pairs = new List<(string, string)>([.. Enum.GetValues<Market>().Select(m => (m.ToString().ToLowerInvariant(), NameOf(m).ToLowerInvariant()))])
        {
            ("car", "motor"),
            ("home", "property"),
        };
        var map = new Dictionary<string, IEnumerable<string>>(StringComparer.Ordinal);
        foreach ((string a, string b) in pairs)
        {
            map[a] = [b];
            map[b] = [a];
        }

        return map;
    }

    internal static Settings Desired() => new()
    {
        SearchableAttributes = Searchable,
        FilterableAttributes = [.. Filterable.Select(a => (FilterableAttribute)a)],
        SortableAttributes = Sortable,
        Synonyms = Synonyms(),
    };

    /// <summary>True when <paramref name="current"/> already holds every setting this app owns (searchable order matters; the others are sets).</summary>
    internal static bool Matches(Settings current, Settings desired) =>
        (current.SearchableAttributes ?? []).SequenceEqual(desired.SearchableAttributes ?? [])
        && SameSet(current.FilterableAttributes?.Select(f => f.Attribute), desired.FilterableAttributes?.Select(f => f.Attribute))
        && SameSet(current.SortableAttributes, desired.SortableAttributes)
        && SameSynonyms(current.Synonyms, desired.Synonyms);

    private static bool SameSet(IEnumerable<string>? a, IEnumerable<string>? b) => new HashSet<string>(a ?? [], StringComparer.Ordinal).SetEquals(b ?? []);

    private static bool SameSynonyms(Dictionary<string, IEnumerable<string>>? a, Dictionary<string, IEnumerable<string>>? b)
    {
        a ??= [];
        b ??= [];
        return a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out IEnumerable<string>? other) && SameSet(kv.Value, other));
    }
}
