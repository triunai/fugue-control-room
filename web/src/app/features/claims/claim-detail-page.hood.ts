import { HoodNote } from '../hood/hood-note';

export const CLAIM_DETAIL_HOOD: HoodNote = {
  why: [
    'Opening the claim reads it and keeps its ETag; every action is sent with that ETag as If-Match, and the reply is the claim as it now stands.',
    'It refreshes on focus and every 15 s (paused mid-form). A stale action shows a Reload banner, never a silent overwrite; a claimant never sees the reserve.',
  ],
  how: [
    'Reads GET /claims/{id}; actions POST pickup, PUT reserve, POST info-requests, decision and settle, each answering with the claim and its new ETag.',
    'The ETag is the row version (Postgres xmin). If-Match is required (428), checked before the state machine (412), and again by the UPDATE.',
    'The aggregate guards each move (409); the new status, timeline row and outbox event commit in one SaveChanges.',
    'Outside your scope a claim is 404, never 403; permission codes from /me decide which buttons show, the API checks again.',
  ],
  adrs: [
    { label: 'ADR-006 Data access: xmin is the ETag', path: 'docs/adr/ADR-006-ef-core-reads-dapper-writes.md' },
    { label: 'ADR-004 Errors: FluentResults with ErrorType', path: 'docs/adr/ADR-004-fluentresults-with-errortype.md' },
    { label: 'ADR-011 Outbox in the same transaction', path: 'docs/adr/ADR-011-outbox-and-signed-webhooks.md' },
  ],
  tests: [
    { layer: 'unit', path: 'tests/Fugue.Tests/Domain/ClaimTests.cs', name: 'The_transition_table_holds_for_every_status_and_move', asserts: 'Every status x move: legal ones change status with one timeline row and event; illegal ones are 409 and change nothing.' },
    { layer: 'integration', path: 'tests/Fugue.Tests/Claims/ClaimsWorkflowTests.cs', name: 'A_second_officer_with_the_old_etag_gets_412_and_with_a_fresh_one_gets_409', asserts: 'A stale If-Match is 412, a fresh one then meets the state machine (409); neither failure wrote a timeline row or an event.' },
    { layer: 'api', path: 'bruno/fugue/claims/06-pickup-without-if-match.bru', name: 'Pickup without If-Match is 428', asserts: 'Pickup with no If-Match answers 428 with code claim.if_match_required.' },
    { layer: 'web', path: 'web/src/app/core/http/interceptors.spec.ts', name: 'sends the claim tag on every claim command and stores the new tag against the claim', asserts: 'Every claim command sends the stored ETag as If-Match and keeps the new ETag from the reply.' },
    { layer: 'e2e', path: 'web/e2e/06-claims-flow.spec.ts', name: 'a claim from report to settlement, and the changed-since-you-opened-it banner', asserts: 'A claim goes from report to settlement in the console; a stale edit shows the Changed-since-you-opened-it banner with Reload.' },
  ],
  decisions: [
    { label: 'D-056 ETag checked before the state machine', path: 'docs/decisions.md' },
    { label: 'Concurrency and atomicity', path: 'docs/CLAIMS.md#4-concurrency-duplicates-and-atomicity' },
    { label: 'The claim and its state machine', path: 'docs/CLAIMS.md#2-the-claim-and-its-state-machine' },
    { label: 'Who sees what', path: 'docs/CLAIMS.md#3-who-sees-what' },
  ],
  guards: [
    {
      status: '428',
      name: 'If-Match required',
      path: 'src/Fugue.Application/Features/Claims/ClaimFlow.cs',
      code: `if (expectedVersion is not { } expected)
{
    return Result.Fail<ClaimDto>(ClaimErrors.IfMatchRequired());
}`,
      note: 'An action without If-Match is refused before anything is loaded.',
    },
    {
      status: '412',
      name: 'stale',
      path: 'src/Fugue.Application/Features/Claims/ClaimFlow.cs',
      code: `if (claim.RowVersion != expected)
{
    return Result.Fail<ClaimDto>(ClaimErrors.VersionMismatch());
}`,
      note: 'Someone changed the claim since you read it: reload, then retry.',
    },
    {
      status: '409',
      name: 'invalid transition',
      path: 'src/Fugue.Domain/Claims/Claim.cs',
      code: `if (Status != ClaimStatus.Submitted)
{
    return Result.Fail(InvalidTransition(ClaimStatus.UnderReview));
}`,
      note: 'Shown for Pickup; every move in the aggregate checks the current status the same way.',
    },
    {
      status: '409',
      name: 'not your claim',
      path: 'src/Fugue.Application/Features/Claims/ClaimFlow.cs',
      code: `bool allowed = claim.AssignedOfficerId is not { } assigned
    || (user.UserId is { } me && assigned.Value == me)
    || user.HasPermission(Permissions.ClaimsSettle);
return allowed ? Result.Ok() : Result.Fail(ClaimErrors.NotAssignedToYou());`,
      note: 'Only the assigned officer, or a manager, may act on an assigned claim.',
    },
    {
      status: '404',
      name: 'outside your scope',
      path: 'src/Fugue.Infrastructure/Persistence/Claims/ClaimRepository.cs',
      code: `scope.All ? query
: scope.ClaimantId is { } owner ? query.Where(c => c.ClaimantUserId == owner)
: query.Where(_ => false);`,
      note: 'A claim you may not see answers exactly like one that does not exist.',
    },
    {
      status: '403',
      name: 'permission',
      path: 'src/Fugue.Api/Auth/HasPermissionAttribute.cs',
      code: `if (context.User.HasClaim(FugueClaimTypes.Permission, requirement.Permission))
{
    context.Succeed(requirement);
}`,
      note: 'Without the permission code the policy never succeeds and the API answers 403.',
    },
  ],
};
