using FluentResults;
using Fugue.Application.Common;
using Fugue.Application.Features.Webhooks.Models;
using Fugue.Application.Features.Webhooks.Repos;
using Fugue.Domain.Ids;
using Fugue.Domain.Webhooks;
using Fugue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fugue.Infrastructure.Webhooks;

/// <summary>EF read and write side of <c>webhook_subscriptions</c>. Stamping and the audit row come from the SaveChanges interceptors; the secret column is <c>[NoAudit]</c>.</summary>
internal sealed class WebhookSubscriptionStore(FugueDbContext db) : IWebhookSubscriptionStore
{
    public async Task<WebhookSubscriptionDto> AddAsync(WebhookSubscription subscription, CancellationToken ct)
    {
        db.WebhookSubscriptions.Add(subscription);
        await db.SaveChangesAsync(ct);
        return ToDto(subscription);
    }

    public async Task<WebhookSubscriptionDto?> GetAsync(WebhookSubscriptionId id, CancellationToken ct) =>
        await db.WebhookSubscriptions.AsNoTracking().Where(s => s.Id == id).Select(Projection).FirstOrDefaultAsync(ct) is { } row ? row.ToDto() : null;

    public async Task<IReadOnlyList<WebhookSubscriptionDto>> ListAsync(KeysetCursor? after, int take, CancellationToken ct)
    {
        IQueryable<WebhookSubscription> query = after is { } cursor
            ? db.WebhookSubscriptions.FromSqlRaw(SqlScripts.Get("webhooks/list-subscriptions-after"), new NpgsqlParameter("createdAt", cursor.CreatedAt), new NpgsqlParameter("id", cursor.Id))
            : db.WebhookSubscriptions;

        List<Row> rows = await query.AsNoTracking().OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).Take(take).Select(Projection).ToListAsync(ct);
        return rows.Select(r => r.ToDto()).ToList();
    }

    public Task<WebhookSubscription?> FindForUpdateAsync(WebhookSubscriptionId id, CancellationToken ct) =>
        db.WebhookSubscriptions.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<Result<WebhookSubscriptionDto>> SaveAsync(WebhookSubscription subscription, long? expectedVersion, CancellationToken ct)
    {
        if (!MatchesExpected(subscription, expectedVersion))
        {
            return Result.Fail<WebhookSubscriptionDto>(WebhookErrors.VersionMismatch());
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail<WebhookSubscriptionDto>(WebhookErrors.VersionMismatch());
        }

        return Result.Ok(ToDto(subscription));
    }

    public async Task<Result> DeleteAsync(WebhookSubscription subscription, long expectedVersion, CancellationToken ct)
    {
        if (!MatchesExpected(subscription, expectedVersion))
        {
            return Result.Fail(WebhookErrors.VersionMismatch());
        }

        db.WebhookSubscriptions.Remove(subscription);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail(WebhookErrors.VersionMismatch());
        }

        return Result.Ok();
    }

    /// <summary>Compares the loaded version with If-Match and pins it as the original value, so a write that raced in between also fails (<c>WHERE xmin = @expected</c>).</summary>
    private bool MatchesExpected(WebhookSubscription subscription, long? expectedVersion)
    {
        if (expectedVersion is not { } expected)
        {
            return true;
        }

        if (expected != subscription.RowVersion)
        {
            return false;
        }

        db.Entry(subscription).Property(s => s.RowVersion).OriginalValue = (uint)expected;
        return true;
    }

    private static readonly System.Linq.Expressions.Expression<Func<WebhookSubscription, Row>> Projection = s => new Row(
        s.Id, s.Name, s.Url, s.EventTypes, s.IsActive, s.CreatedAt, s.CreatedBy, s.UpdatedAt, s.UpdatedBy, s.RowVersion);

    private sealed record Row(
        WebhookSubscriptionId Id, string Name, string Url, string[] EventTypes, bool IsActive, DateTimeOffset CreatedAt, string CreatedBy,
        DateTimeOffset UpdatedAt, string UpdatedBy, uint RowVersion)
    {
        public WebhookSubscriptionDto ToDto() => new(Id, Name, Url, EventTypes, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, RowVersion);
    }

    private static WebhookSubscriptionDto ToDto(WebhookSubscription s) => new(
        s.Id, s.Name, s.Url, s.EventTypes, s.IsActive, s.CreatedAt, s.CreatedBy, s.UpdatedAt, s.UpdatedBy, s.RowVersion);
}
