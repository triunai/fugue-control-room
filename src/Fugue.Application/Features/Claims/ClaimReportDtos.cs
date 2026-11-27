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
/// <see cref="OldestOpenAgeDays"/> and <see cref="OldestOpenClaimId"/> are null when the officer holds no open claim. An officer who can handle
/// claims but holds none is listed with zeros.
/// </summary>
public sealed record OfficerWorkloadDto(
    UserId OfficerId,
    string OfficerName,
    int UnderReview,
    int InfoRequested,
    double? OldestOpenAgeDays,
    int DecidedLast7Days,
    int DecidedLast30Days,
    double? MedianDaysToDecision,
    ClaimId? OldestOpenClaimId);

/// <summary>
/// The company's exposure per market and line, in the market's own currency. Amounts are bigint minor units and are NEVER added across currencies:
/// <see cref="Totals"/> are per currency. <c>OutstandingReserveMinor</c> sums the reserves of open claims only; an approved claim leaves it and shows as
/// <c>ApprovedNotSettled</c> until it is settled; rejected and settled claims are not exposure. <c>UnreservedOpenCount</c> is the open claims with no
/// reserve yet: their exposure is unknown, so it must not be read as zero.
/// <c>SettledCount</c> and <c>SettledMinor</c> are what has been paid out: the settled claims and the sum of their approved amounts (a claim is settled
/// for exactly its approved amount), all time, in the same currency. Paid is not exposure, so a market and line with only settled claims is a row
/// whose exposure fields are all zero.
/// </summary>
public sealed record ClaimExposureDto(DateTimeOffset AsOf, IReadOnlyList<ExposureRowDto> Rows, IReadOnlyList<ExposureTotalDto> Totals);

public sealed record ExposureRowDto(
    Market Market,
    LineOfBusiness Line,
    string Currency,
    int OpenCount,
    long OutstandingReserveMinor,
    int ApprovedNotSettledCount,
    long ApprovedNotSettledMinor,
    int UnreservedOpenCount,
    int SettledCount,
    long SettledMinor);

public sealed record ExposureTotalDto(
    string Currency,
    int OpenCount,
    long OutstandingReserveMinor,
    int ApprovedNotSettledCount,
    long ApprovedNotSettledMinor,
    int UnreservedOpenCount,
    int SettledCount,
    long SettledMinor);
