using System.Text.RegularExpressions;

namespace Fugue.Domain.Webhooks;

/// <summary>The subscription filter language: <c>*</c> (everything), <c>customer.*</c> (a prefix), or an exact versioned type.</summary>
public static partial class EventTypeFilter
{
    public const string All = "*";

    public const int MaxFilters = 20;

    [GeneratedRegex(@"^(\*|[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)*(\.\*|\.v[0-9]+)?)$")]
    private static partial Regex Valid();

    public static bool IsValid(string filter) => filter.Length <= 64 && Valid().IsMatch(filter);

    public static bool Matches(string filter, string eventType) =>
        filter == All
        || filter == eventType
        || (filter.EndsWith(".*", StringComparison.Ordinal) && eventType.StartsWith(filter[..^1], StringComparison.Ordinal));

    public static bool MatchesAny(IEnumerable<string> filters, string eventType) => filters.Any(f => Matches(f, eventType));
}
