using FluentResults;

namespace Fugue.Application.Features.Modules;

/// <summary>The runtime module registry (the <c>modules</c> table) joined with the configuration kill switch (ADR-009).</summary>
public interface IModuleRegistry
{
    Task<IReadOnlyList<ModuleDto>> ListAsync(CancellationToken ct);

    /// <summary>True when the module exists, is not deleted, is active in the database AND is not switched off in configuration. Unknown codes are false.</summary>
    Task<bool> IsEffectivelyEnabledAsync(string code, CancellationToken ct);

    /// <summary>Flips <c>is_active</c> under an <c>If-Match</c> version check. Fails <c>module.not_found</c>, <c>module.version_mismatch</c>.</summary>
    Task<Result<ModuleDto>> SetActiveAsync(string code, bool active, long expectedVersion, CancellationToken ct);
}
