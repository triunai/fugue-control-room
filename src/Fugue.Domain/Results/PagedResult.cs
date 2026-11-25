namespace Fugue.Domain.Results;

/// <summary>A keyset page. <paramref name="NextCursor"/> is null on the last page.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, string? NextCursor);
