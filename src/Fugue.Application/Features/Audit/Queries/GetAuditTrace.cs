using FluentResults;
using FluentValidation;
using Fugue.Application.Abstractions;
using Fugue.Application.Features.Audit.Repos;
using Fugue.Domain.Audit;
using Fugue.Domain.Results;
using Fugue.Domain.Webhooks;
using Mediator;

namespace Fugue.Application.Features.Audit.Queries;

/// <summary>Contract. "The Score" (D-064): the command, events and webhook deliveries of one correlation id, with times relative to the first.</summary>
public sealed record GetAuditTraceQuery(string CorrelationId) : IQuery<Result<AuditTraceDto>>;

public sealed class GetAuditTraceValidator : AbstractValidator<GetAuditTraceQuery>
{
    public GetAuditTraceValidator() =>
        RuleFor(x => x.CorrelationId)
            .Must(CorrelationIdFormat.IsValid)
            .WithMessage($"A correlation id is 1 to {CorrelationIdFormat.MaxLength} characters of [A-Za-z0-9-_].");
}

public sealed class GetAuditTraceHandler(IAuditTraceReader reader) : IQueryHandler<GetAuditTraceQuery, Result<AuditTraceDto>>
{
    public async ValueTask<Result<AuditTraceDto>> Handle(GetAuditTraceQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<AuditTraceEntry> rows = await reader.ReadAsync(query.CorrelationId, cancellationToken);
        if (rows.Count == 0)
        {
            return Result.Fail<AuditTraceDto>(FugueErrors.NotFound("audit.trace_not_found", $"Nothing was recorded under correlation id {query.CorrelationId}."));
        }

        DateTimeOffset start = rows.Min(r => r.At);
        long Ms(DateTimeOffset at) => (long)Math.Round((at - start).TotalMilliseconds);

        AuditTraceVoiceDto[] voices = [.. rows.Select(r => new AuditTraceVoiceDto(
            r.Kind, r.Id, r.At, Ms(r.At), r.EndAt is { } end ? Ms(end) : null, r.Label, r.Detail, r.Status,
            r.DurationMs, r.Attempts, r.StatusCode, r.ErrorCode, r.EntityType, r.EntityId))];
        long span = voices.Max(v => Math.Max(v.AtMs, v.EndMs ?? v.AtMs));
        return Result.Ok(new AuditTraceDto(query.CorrelationId, start, span, StatusOf(rows), voices));
    }

    private static string StatusOf(IReadOnlyList<AuditTraceEntry> rows)
    {
        if (rows.Any(r => (r.Kind == AuditTraceKinds.Command && r.Status != AuditOutcomes.Success) || r.Status == OutboxStatuses.Dead))
        {
            return "failed";
        }

        return rows.Any(r => r.Status is OutboxStatuses.Pending or WebhookDeliveryStatuses.InFlight) ? "pending" : "complete";
    }
}
