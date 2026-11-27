import { HoodNote } from '../hood/hood-note';

export const RESERVE_SHEET_HOOD: HoodNote = {
  why: [
    'A spreadsheet is the natural view for reviewing many reserves at once, but it has no special write path: each changed row is the same guarded command as on the claim page.',
    'Rows save one after another with their own result; a stale row (412) is re-read and a failed row never stops the next one.',
  ],
  how: [
    'Loads open claims with GET /claims, one open status at a time, paged, oldest first. Only the reserve column is editable.',
    'Save sends PUT /claims/{id}/reserve per changed row with that claim’s ETag as If-Match (xmin); there is no bulk endpoint (D-059).',
    'The command is checked by the aggregate (409), saved with its ClaimReserveSet event in one SaveChanges, and the reply is the claim with its new ETag.',
    'The amount is a bigint in minor units in the claim’s own currency; nothing is summed or converted across currencies.',
  ],
  adrs: [
    { label: 'ADR-006 Data access: xmin is the ETag', path: 'docs/adr/ADR-006-ef-core-reads-dapper-writes.md' },
    { label: 'ADR-011 Outbox in the same transaction', path: 'docs/adr/ADR-011-outbox-and-signed-webhooks.md' },
  ],
  tests: [
    { layer: 'unit', path: 'tests/Fugue.Tests/Domain/ClaimTests.cs', name: 'Setting_the_reserve_raises_a_thin_event_without_the_amount', asserts: 'Setting the reserve raises one event that carries no amount, so the webhook never leaks it.' },
    { layer: 'integration', path: 'tests/Fugue.Tests/Claims/ClaimsWorkflowTests.cs', name: 'Only_the_assigned_officer_or_a_manager_may_reserve_ask_or_decide', asserts: 'Another officer gets 409 claim.not_assigned_to_you on reserve, question and decision; a manager may override.' },
    { layer: 'api', path: 'bruno/fugue/claims/11-officer-sets-reserve.bru', name: 'Officer sets the reserve (SGD 1,000.00 = 100000 minor units)', asserts: 'PUT reserve with If-Match answers 200 with the claim and a new ETag.' },
    { layer: 'web', path: 'web/src/app/features/claims/reserve-sheet-logic.spec.ts', name: 'never sums or converts across currencies: each command carries its own row amount only', asserts: 'Each changed row becomes one command carrying only its own amount; nothing is added or converted across currencies.' },
    { layer: 'e2e', path: 'web/e2e/07-grid.spec.ts', name: 'the sheet shows open claims, saves per row and maps a stale write to a reload', asserts: 'Edited rows save one by one, and a row changed elsewhere shows a stale result and is re-read.' },
  ],
  decisions: [
    { label: 'D-059 Univer reserve sheet, row-by-row save', path: 'docs/decisions.md' },
    { label: 'D-057 Money: ISO code, two decimals', path: 'docs/decisions.md' },
    { label: 'Concurrency and atomicity', path: 'docs/CLAIMS.md#4-concurrency-duplicates-and-atomicity' },
    { label: 'Money in minor units', path: 'docs/CLAIMS.md#7-money' },
  ],
  guards: [
    {
      status: '403',
      name: 'permission',
      path: 'src/Fugue.Api/Controllers/ClaimsController.cs',
      code: `[HttpPut("{id}/reserve")]
[HasPermission(Permissions.ClaimsHandle)]
[IfMatch]`,
      note: 'Without CLAIMS_HANDLE the reserve is refused; the route itself needs it too.',
    },
    {
      status: '409',
      name: 'not your claim',
      path: 'src/Fugue.Application/Features/Claims/ClaimFlow.cs',
      code: `bool allowed = claim.AssignedOfficerId is not { } assigned
    || (user.UserId is { } me && assigned.Value == me)
    || user.HasPermission(Permissions.ClaimsSettle);
return allowed ? Result.Ok() : Result.Fail(ClaimErrors.NotAssignedToYou());`,
      note: 'The row shows the API’s message; only the assigned officer or a manager may change the reserve.',
    },
    {
      status: '409',
      name: 'wrong status',
      path: 'src/Fugue.Domain/Claims/Claim.cs',
      code: `if (Status is not (ClaimStatus.UnderReview or ClaimStatus.InfoRequested))
{
    return Result.Fail(FugueErrors.Conflict(InvalidTransitionCode, $"The reserve cannot be set while the claim is {Status}."));
}`,
      note: 'The reserve can be revised only while the claim is under review or waiting for the claimant.',
    },
    {
      status: '412',
      name: 'stale',
      path: 'src/Fugue.Application/Features/Claims/ClaimFlow.cs',
      code: `if (claim.RowVersion != expected)
{
    return Result.Fail<ClaimDto>(ClaimErrors.VersionMismatch());
}`,
      note: 'That row is re-read and the sheet shows the claim as it now stands; the other rows still save.',
    },
  ],
};
