using Fugue.Api.Http;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Modules;
using Fugue.Domain.Ids;

namespace Fugue.Api.Modules;

/// <summary>
/// Refuses requests to a disabled module with 404 <c>module.disabled</c> (never 403: that would confirm the feature exists). Runs
/// after authentication and before authorization, because a disabled module also removes its permissions and the caller would
/// otherwise see a 403. Anonymous callers are left to the 401 so the gate reveals nothing to them.
/// </summary>
public sealed partial class ModuleGateMiddleware(RequestDelegate next, ILogger<ModuleGateMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ModuleAttribute? module = context.GetEndpoint()?.Metadata.GetMetadata<ModuleAttribute>();
        if (module is not null
            && context.User.Identity?.IsAuthenticated == true
            && !await context.RequestServices.GetRequiredService<IModuleRegistry>().IsEffectivelyEnabledAsync(module.Code, context.RequestAborted))
        {
            LogRefused(module.Code, context.Request.Path);
            _ = Guid.TryParse(context.User.FindFirst("sub")?.Value, out Guid sub);
            await context.RequestServices.GetRequiredService<IAccessAuditWriter>().WriteAsync(
                new AccessAuditEntry("module_disabled", "denied", module.Code, sub == Guid.Empty ? null : UserId.From(sub), null, AuthProblems.ClientOf(context)),
                context.RequestAborted);
            await AuthProblems.WriteAsync(context, StatusCodes.Status404NotFound, "module.disabled", "The requested resource was not found.");
            return;
        }

        await next(context);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Module {Module} is disabled; refused {Path}")]
    private partial void LogRefused(string module, PathString path);
}
