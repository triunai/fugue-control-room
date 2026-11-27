using Fugue.Domain.Results;

namespace Fugue.Application.Features.Modules.Models;

/// <summary>
/// A module as the admin API shows it: the configuration switch, the runtime (database) switch, and the result.
/// <see cref="Effective"/> is true only when both are on. <see cref="Version"/> is the ETag.
/// </summary>
public sealed record ModuleDto(string Code, string Name, bool ConfigEnabled, bool DbActive, bool Effective, long Version) : IVersioned;
