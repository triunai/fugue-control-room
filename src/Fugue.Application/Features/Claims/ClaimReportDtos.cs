using Fugue.Domain.Claims;
using Fugue.Domain.Ids;

namespace Fugue.Application.Features.Claims;

/// <summary>
/// Who is working what, as of <see cref="AsOf"/> (live rows, as fresh as the last committed transaction). "Open" means still to be decided:
/// Submitted, UnderReview or InfoRequested. Ages are in days since submission. A decision counts for the claim's assigned officer.
/// </summary>
public sealed record ClaimWorkloadDto(
    DateTimeOffset AsOf, int UnassignedCount, IReadOnlyList<AgeingBucketDto> Ageing, IReadOnlyList<OfficerWorkloadDto> Officers);

/// <summary>Open claims whose age falls in a band: <c>0-2</c>, <c>3-7</c>, <c>8-14</c> or <c>15+</c> days.</summary>
public sealed record AgeingBucketDto(string Label, int Count);

/// <summary>
/// One officer's queue. <see cref="MedianDaysToDecision"/> is over the claims decided in the last 30 days (null when there were none);
/// <see cref="OldestOpenAgeDays"/> is null when the officer holds no open claim.
/// </summary>
public sealed record OfficerWorkloadDto(
    UserId OfficerId,
    string OfficerName,
    int UnderReview,
    int InfoRequested,
    double? OldestOpenAgeDays,
    int DecidedLast7Days,
    int DecidedLast30Days,
    double? MedianDaysToDecision);

/// <summary>
/// The company's exposure per market and line, in the market's own currency. Amounts are bigint minor units and are NEVER added across currencies:
/// <see cref="Totals"/> are per currency. <c>OutstandingReserveMinor</c> sums the reserves of open claims only; an approved claim leaves it and shows as
/// <c>ApprovedNotSettled</c> until it is settled; rejected and settled claims are not exposure.
/// </summary>
public sealed record ClaimExposureDto(DateTimeOffset AsOf, IReadOnlyList<ExposureRowDto> Rows, IReadOnlyList<ExposureTotalDto> Totals);

public sealed record ExposureRowDto(
    Market Market,
    LineOfBusiness Line,
    string Currency,
    int OpenCount,
    long OutstandingReserveMinor,
    int ApprovedNotSettledCount,
    long ApprovedNotSettledMinor);

public sealed record ExposureTotalDto(
    string Currency, int OpenCount, long OutstandingReserveMinor, int ApprovedNotSettledCount, long ApprovedNotSettledMinor);
