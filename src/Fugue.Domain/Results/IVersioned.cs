namespace Fugue.Domain.Results;

/// <summary>A resource with an optimistic-concurrency version, exposed as an ETag.</summary>
public interface IVersioned
{
    long Version { get; }
}
