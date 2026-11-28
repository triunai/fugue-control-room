import { HoodNote } from '../hood/hood-note';

export const CLAIM_ASSESSMENT_HOOD: HoodNote = {
  why: [
    'The worksheet is a calculator in the browser: five items minus the excess, in exact minor units. The API never sees the worksheet, only the net amount you choose to use as the reserve.',
    'Use as reserve is the same guarded command as on the claim page, so a stale or foreign claim is refused, not overwritten.',
  ],
  flow: [
    {
      layer: 'web',
      text:
        'The five items and the excess are summed in the browser in integer minor units; nothing is sent while you type.',
      path: 'web/src/app/features/claims/claim-assessment-logic.ts',
    },
    {
      layer: 'web',
      text:
        '"Use as reserve" is enabled when the net is above zero and the claim\'s status allows a reserve.',
      path: 'web/src/app/features/claims/claim-assessment-sheet.ts',
    },
    {
      layer: 'web',
      text:
        "It calls setReserve with the net amount: PUT /claims/{id}/reserve with the claim's ETag as If-Match.",
      path: 'web/src/app/api/claims.api.ts',
    },
    {
      layer: 'api',
      text:
        'The same endpoint as the claim page: CLAIMS_HANDLE, [IfMatch], then ReserveClaimCommand.',
      path: 'src/Fugue.Api/Controllers/ClaimsController.cs',
    },
    {
      layer: 'handler',
      text:
        'If-Match (428), scope (404) and the version check (412) come before the state machine; then the assignment rule and the aggregate (409).',
      path: 'src/Fugue.Application/Features/Claims/Helpers/ClaimFlow.cs',
    },
    {
      layer: 'db',
      text:
        'The UPDATE carries the xmin check; the reserve, its audit row and the ClaimReserveSet outbox row commit in one SaveChanges.',
      path: 'src/Fugue.Infrastructure/Persistence/Claims/ClaimRepository.cs',
    },
    {
      layer: 'web',
      text:
        'The sheet emits the reserved claim and the page refreshes its cache with the new ETag.',
      path: 'web/src/app/features/claims/claim-assessment-page.ts',
    },
  ],
  how: [
    'Reads GET /claims/{id} for the claim and its currency; the totals are formulas in the sheet and are never stored.',
    '"Use as reserve" sends PUT /claims/{id}/reserve with the net in minor units and the claim’s ETag as If-Match (xmin).',
    'It is enabled only when the net is above zero and you may reserve this claim in its status; the API checks again (409, 412).',
    'Save to Excel and CSV are built in the browser from the sheet; nothing is uploaded.',
  ],
  adrs: [
    { label: 'ADR-006 Data access: xmin is the ETag', path: 'docs/adr/ADR-006-ef-core-reads-dapper-writes.md' },
    { label: 'ADR-011 Outbox in the same transaction', path: 'docs/adr/ADR-011-outbox-and-signed-webhooks.md' },
  ],
  tests: [
    { layer: 'web', path: 'web/src/app/features/claims/claim-assessment-logic.spec.ts', name: 'sums the five items and subtracts the excess exactly, in minor units', asserts: 'The net is the five items minus the excess, computed in integer minor units with no rounding drift.' },
    { layer: 'web', path: 'web/src/app/features/claims/claim-assessment-logic.spec.ts', name: 'refuses text, negatives and more than two decimals', asserts: 'Text, negative amounts and more than two decimals are rejected before anything is sent.' },
    { layer: 'unit', path: 'tests/Fugue.Tests/Domain/ClaimTests.cs', name: 'A_reserve_must_be_positive', asserts: 'A reserve of zero or less is refused by the aggregate.' },
    { layer: 'integration', path: 'tests/Fugue.Tests/Claims/ClaimsWorkflowTests.cs', name: 'Only_the_assigned_officer_or_a_manager_may_reserve_ask_or_decide', asserts: 'Another officer gets 409 claim.not_assigned_to_you on reserve, question and decision; a manager may override.' },
    { layer: 'e2e', path: 'web/e2e/08-assessment.spec.ts', name: 'assessment worksheet: formulas, use as reserve, save to Excel', asserts: 'Entering amounts updates the formulas, Use as reserve sets the claim reserve, and Save to Excel downloads a file.' },
  ],
  decisions: [
    { label: 'D-057 Money: ISO code, two decimals', path: 'docs/decisions.md' },
    { label: 'D-059 Univer spreadsheet surface, lazy-loaded', path: 'docs/decisions.md' },
    { label: 'Money in minor units', path: 'docs/CLAIMS.md#7-money' },
    { label: 'Concurrency and atomicity', path: 'docs/CLAIMS.md#4-concurrency-duplicates-and-atomicity' },
  ],
  guards: [
    {
      status: '403',
      name: 'permission',
      path: 'src/Fugue.Api/Controllers/ClaimsController.cs',
      code: `[HttpPut("{id}/reserve")]
[HasPermission(Permissions.ClaimsHandle)]
[IfMatch]`,
      note: 'The worksheet route and the reserve command both need CLAIMS_HANDLE.',
    },
    {
      status: '400',
      name: 'amount must be positive',
      path: 'src/Fugue.Domain/Claims/Claim.cs',
      code: `if (amountMinor <= 0)
{
    return Result.Fail(AmountInvalid("The reserve must be greater than zero."));
}`,
      note: 'A net at or below zero is refused here even if the button were forced; the page keeps it disabled.',
    },
    {
      status: '409',
      name: 'not your claim',
      path: 'src/Fugue.Application/Features/Claims/Helpers/ClaimFlow.cs',
      code: `bool allowed = claim.AssignedOfficerId is not { } assigned
    || (user.UserId is { } me && assigned.Value == me)
    || user.HasPermission(Permissions.ClaimsSettle);
return allowed ? Result.Ok() : Result.Fail(ClaimErrors.NotAssignedToYou());`,
      note: 'Only the assigned officer, or a manager, may set the reserve.',
    },
    {
      status: '412',
      name: 'stale',
      path: 'src/Fugue.Application/Features/Claims/Helpers/ClaimFlow.cs',
      code: `if (claim.RowVersion != expected)
{
    return Result.Fail<ClaimDto>(ClaimErrors.VersionMismatch());
}`,
      note: 'Someone changed the claim since you opened the worksheet: reload, then use it again.',
    },
  ],
};
