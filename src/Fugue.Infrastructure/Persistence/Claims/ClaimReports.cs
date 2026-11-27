using Fugue.Application.Features.Claims;
using Fugue.Domain.Claims;
using Fugue.Domain.Ids;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fugue.Infrastructure.Persistence.Claims;

/// <summary>
/// The workload board and the exposure table, read through EF only (ADR-006: no reads through Dapper): LINQ for the counts and sums, one EF raw
/// SQL statement for the per-officer board because the median needs <c>percentile_cont</c>. Live rows, so a result is as fresh as the last commit.
/// </summary>
internal sealed class ClaimReports(FugueDbContext db) : IClaimReports
{
    public async Task<ClaimWorkloadDto> GetWorkloadAsync(DateTimeOffset asOf, CancellationToken ct)
    {
        List<OfficerRow> officers = await db.Database.SqlQueryRaw<OfficerRow>(SqlScripts.Get("claims/workload-by-officer"), new NpgsqlParameter("asOf", asOf)).ToListAsync(ct);

        // Open claims by age band. Thresholds are computed here so the comparison stays on the indexed column.
        IQueryable<DateTimeOffset> open = db.Claims.AsNoTracking()
            .Where(c => c.Status == ClaimStatus.Submitted || c.Status == ClaimStatus.UnderReview || c.Status == ClaimStatus.InfoRequested)
            .Select(c => c.SubmittedAt);
        DateTimeOffset d3 = asOf.AddDays(-3), d8 = asOf.AddDays(-8), d15 = asOf.AddDays(-15);
        int unassigned = await db.Claims.AsNoTracking().CountAsync(c => c.Status == ClaimStatus.Submitted && c.AssignedOfficerId == null, ct);
        int[] ageing =
        [
            await open.CountAsync(s => s > d3, ct), // 0-2 days
            await open.CountAsync(s => s <= d3 && s > d8, ct), // 3-7
            await open.CountAsync(s => s <= d8 && s > d15, ct), // 8-14
            await open.CountAsync(s => s <= d15, ct), // 15+
        ];

        return new ClaimWorkloadDto(
            asOf,
            unassigned,
            [new("0-2", ageing[0]), new("3-7", ageing[1]), new("8-14", ageing[2]), new("15+", ageing[3])],
            [.. officers.Select(o => new OfficerWorkloadDto(
                UserId.From(o.OfficerId), o.OfficerName, o.UnderReview, o.InfoRequested,
                o.OldestOpenSubmittedAt is { } oldest ? Math.Max(0, (asOf - oldest).TotalDays) : null,
                o.DecidedWeek, o.DecidedMonth, o.MedianDays,
                o.OldestOpenClaimId is { } oldestId ? ClaimId.From(oldestId) : null))]);
    }

    public async Task<ClaimExposureDto> GetExposureAsync(DateTimeOffset asOf, CancellationToken ct)
    {
        // Only claims still carrying money: open ones (their reserve) and approved ones awaiting settlement (their approved amount).
        var grouped = await db.Claims.AsNoTracking()
            .Where(c => c.Status == ClaimStatus.Submitted || c.Status == ClaimStatus.UnderReview || c.Status == ClaimStatus.InfoRequested || c.Status == ClaimStatus.Approved)
            .GroupBy(c => new { c.Market, c.Line, c.Currency })
            .Select(g => new
            {
                g.Key.Market,
                g.Key.Line,
                g.Key.Currency,
                OpenCount = g.Count(c => c.Status != ClaimStatus.Approved),
                Reserve = g.Sum(c => c.Status != ClaimStatus.Approved ? c.ReserveMinor ?? 0 : 0),
                Unreserved = g.Count(c => c.Status != ClaimStatus.Approved && c.ReserveMinor == null),
                ApprovedCount = g.Count(c => c.Status == ClaimStatus.Approved),
                ApprovedAmount = g.Sum(c => c.Status == ClaimStatus.Approved ? c.ApprovedAmountMinor ?? 0 : 0),
            })
            .ToListAsync(ct);

        List<ExposureRowDto> rows = [.. grouped
            .Select(g => new ExposureRowDto(g.Market, g.Line, g.Currency, g.OpenCount, g.Reserve, g.ApprovedCount, g.ApprovedAmount, g.Unreserved))
            .OrderBy(r => r.Market).ThenBy(r => r.Line)];

        // Totals are per currency: a row is only ever added to rows of the SAME currency.
        List<ExposureTotalDto> totals = [.. rows.GroupBy(r => r.Currency).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new ExposureTotalDto(
                g.Key, g.Sum(r => r.OpenCount), g.Sum(r => r.OutstandingReserveMinor), g.Sum(r => r.ApprovedNotSettledCount), g.Sum(r => r.ApprovedNotSettledMinor), g.Sum(r => r.UnreservedOpenCount)))];

        return new ClaimExposureDto(asOf, rows, totals);
    }

    /// <summary>The raw-SQL row of the officer board; properties follow the snake_case column aliases through the model's naming convention.</summary>
    private sealed class OfficerRow
    {
        public Guid OfficerId { get; set; }

        public string OfficerName { get; set; } = string.Empty;

        public int UnderReview { get; set; }

        public int InfoRequested { get; set; }

        public DateTimeOffset? OldestOpenSubmittedAt { get; set; }

        public Guid? OldestOpenClaimId { get; set; }

        public int DecidedWeek { get; set; }

        public int DecidedMonth { get; set; }

        public double? MedianDays { get; set; }
    }
}
