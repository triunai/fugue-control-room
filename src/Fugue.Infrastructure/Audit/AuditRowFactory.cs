using System.Diagnostics;
using System.Text.Json;
using Fugue.Application.Abstractions;
using Fugue.Domain.Abstractions;
using Fugue.Domain.Audit;
using Fugue.Domain.Ids;
using Microsoft.Extensions.Options;

namespace Fugue.Infrastructure.Audit;

/// <summary>
/// Builds <see cref="AuditLog"/> rows with everything that is the same for every writer: who (user, display, tenant, permission),
/// when (the shared <see cref="IAuditStamp"/> clock), and the request (correlation, saga, trace, HTTP method, route template, client).
/// The SaveChanges interceptor and <see cref="AuditTrail"/> both use it, so rows look identical whichever path wrote them.
/// </summary>
internal sealed class AuditRowFactory(
    IAuditStamp stamp, ICurrentUser user, IAuditScope scope, IAuditRequestInfo request, ICorrelationContext correlation, IOptions<AuditOptions> options)
{
    public bool Enabled => options.Value.Enabled;

    public AuditLog Create(
        string? defaultAction,
        string fallbackAction,
        string? entityType,
        string? entityId,
        string outcome,
        string? errorCode,
        string? changesJson,
        IReadOnlyDictionary<string, object?>? extraMetadata = null,
        string? errorType = null,
        string? errorMessage = null,
        int? durationMs = null)
    {
        AuditRequestInfo http = request.Current;
        Guid? actor = user.UserId;
        string? traceId = Activity.Current?.TraceId.ToString();

        return new AuditLog
        {
            Id = AuditLogId.New(),
            OccurredAt = stamp.Capture().Now,
            ActorUserId = actor,
            ActorType = actor is null ? AuditActorTypes.System : AuditActorTypes.User,
            ActorDisplay = actor is null ? IAuditStamp.SystemActor : scope.ActorDisplay,
            TenantId = user.TenantId,
            PermissionUsed = http.PermissionUsed,
            Action = Clip(scope.Action ?? defaultAction ?? fallbackAction, 64)!,
            EntityType = Clip(entityType, 64),
            EntityId = Clip(entityId, 128),
            Outcome = outcome,
            ErrorCode = Clip(errorCode, 128),
            ErrorType = Clip(errorType, 64),
            ErrorMessage = Clip(errorMessage, 2000),
            DurationMs = durationMs,
            Changes = changesJson,
            CorrelationId = Clip(NullIfEmpty(correlation.CorrelationId), 64),
            SagaId = Clip(correlation.SagaId, 64),
            TraceId = traceId is { Length: > 0 } && traceId != "00000000000000000000000000000000" ? traceId : null,
            HttpMethod = Clip(http.HttpMethod, 8),
            RouteTemplate = Clip(http.RouteTemplate, 256),
            ClientIp = options.Value.CaptureClientIp ? Clip(http.ClientIp, 64) : null,
            UserAgent = Clip(http.UserAgent, 256),
            Metadata = MergeMetadata(extraMetadata),
        };
    }

    private string? MergeMetadata(IReadOnlyDictionary<string, object?>? extra)
    {
        if (scope.Metadata.Count == 0 && (extra is null || extra.Count == 0))
        {
            return null;
        }

        var merged = new Dictionary<string, object?>(scope.Metadata);
        foreach (KeyValuePair<string, object?> pair in extra ?? new Dictionary<string, object?>())
        {
            merged[pair.Key] = pair.Value;
        }

        return JsonSerializer.Serialize(merged);
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static string? Clip(string? value, int max) => value is null || value.Length <= max ? value : value[..max];
}
