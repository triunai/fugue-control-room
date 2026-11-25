using Fugue.Domain.Results;

namespace Fugue.Application.Features.Modules;

public static class ModuleErrors
{
    public static FugueError NotFound(string code) => FugueErrors.NotFound("module.not_found", $"Module {code} was not found.");

    public static FugueError SystemProtected() =>
        FugueErrors.Conflict("module.system_protected", "The SYSTEM module cannot be disabled: it holds module management itself.");

    public static FugueError VersionMismatch() =>
        FugueErrors.PreconditionFailed("module.version_mismatch", "The module changed since it was read. Re-read it and retry with the new ETag.");

    public static FugueError IfMatchRequired() =>
        FugueErrors.PreconditionRequired("module.if_match_required", "This operation requires an If-Match header with the current ETag.");
}
