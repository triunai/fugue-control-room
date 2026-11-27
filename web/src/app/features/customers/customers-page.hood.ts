import { HoodNote } from '../hood/hood-note';

export const CUSTOMERS_HOOD: HoodNote = {
  why: [
    'The starter’s worked example, built before the claims window: one small aggregate that walks every house pattern end to end.',
    'Owners see only their own rows (repository scoping: someone else’s id is a 404); staff with CUSTOMERS_ALL see every row.',
  ],
  flow: [
    {
      layer: 'web',
      text: 'Create sends POST /customers with an Idempotency-Key; a retry of the same submission replays the first response.',
      path: 'web/src/app/api/customers.api.ts',
    },
    {
      layer: 'api',
      text: 'CustomersController.Create is a one-liner behind CUSTOMERS_INSERT, inside [Module(CUSTOMERS)].',
      path: 'src/Fugue.Api/Controllers/CustomersController.cs',
    },
    {
      layer: 'handler',
      text: 'Validation runs in the pipeline; the handler sets the owner (only CUSTOMERS_ALL may assign someone else).',
      path: 'src/Fugue.Application/Features/Customers/Commands/CreateCustomer.cs',
    },
    {
      layer: 'db',
      text: 'ADR-006 insert path: a Dapper insert through SqlExecutor, in the caller’s unit of work or a short transaction of its own.',
      path: 'src/Fugue.Infrastructure/Persistence/Customers/CustomerRepository.cs',
    },
    {
      layer: 'audit',
      text: 'AuditTrail writes the audit row on the same connection and transaction.',
      path: 'src/Fugue.Infrastructure/Audit/AuditTrail.cs',
    },
    {
      layer: 'outbox',
      text: 'customer.created.v1 is enqueued in that transaction too: the event exists if and only if the insert commits.',
      path: 'src/Fugue.Infrastructure/Outbox/OutboxWriter.cs',
    },
    {
      layer: 'db',
      text: 'Edits are EF tracked updates with xmin as the ETag; the same SaveChanges carries the audit row and the event.',
      path: 'src/Fugue.Application/Features/Customers/Commands/UpdateCustomer.cs',
    },
  ],
  how: [
    'Calls GET /customers (keyset paged; search escapes LIKE wildcards) and GET, PUT, DELETE /customers/{id}, plus suspend and reactivate.',
    'Every write but create needs If-Match (ETag = row version): missing is 428, stale is 412; PUT, suspend, reactivate answer 204 + ETag.',
    'Create takes an optional Idempotency-Key (the console always sends one): the same key and body return the stored response.',
    'Suspending twice is 409 invalid transition; an email already used under the same owner is 409.',
  ],
  adrs: [
    { label: 'ADR-006 EF Core reads, Dapper writes', path: 'docs/adr/ADR-006-ef-core-reads-dapper-writes.md' },
    {
      label: 'ADR-002 Controllers over ApiControllerBase',
      path: 'docs/adr/ADR-002-controllers-with-api-controller-base.md',
    },
    { label: 'ADR-013 Idempotency keys', path: 'docs/adr/ADR-013-idempotency-keys.md' },
  ],
  tests: [
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Persistence/CustomersApiTests.cs',
      name: 'Lifecycle_create_get_update_suspend_reactivate_delete',
      asserts: 'The full lifecycle over HTTP: create, get, update, suspend, reactivate, delete.',
    },
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Persistence/CustomerSeamTests.cs',
      name: 'Unit_of_work_makes_dapper_and_ef_writes_atomic',
      asserts: 'A Dapper insert and an EF update in one unit of work commit or roll back together.',
    },
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Identity/OwnershipTests.cs',
      name: 'A_cross_owner_miss_is_indistinguishable_from_an_absent_id',
      asserts: 'Someone else’s customer answers exactly like an id that does not exist.',
    },
    {
      layer: 'api',
      path: 'bruno/fugue/06-update-stale.bru',
      name: 'Update with a stale ETag (412)',
      asserts: 'An update with an old If-Match is 412.',
    },
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Audit/AuditCaptureTests.cs',
      name: 'A_customer_insert_is_a_Dapper_write_and_still_emits_one_rich_audit_row',
      asserts: 'The Dapper customer insert still emits exactly one rich audit row.',
    },
  ],
  decisions: [
    { label: 'D-025 PUT/suspend/reactivate answer 204 + ETag', path: 'docs/decisions.md' },
    { label: 'D-019 Ownership by repository scoping, 404 cross-owner', path: 'docs/decisions.md' },
    { label: 'D-034 Customer email unique per owner', path: 'docs/decisions.md' },
    { label: 'D-050 Idempotency keys with stored-response replay', path: 'docs/decisions.md' },
  ],
  guards: [
    {
      status: '403',
      name: 'permission (CUSTOMERS_INSERT)',
      path: 'src/Fugue.Api/Controllers/CustomersController.cs',
      code: `[HttpPost]
[HasPermission(Permissions.CustomersInsert)]
[Idempotent]`,
      note: 'Each action names its own permission: view, edit, suspend, delete.',
    },
    {
      status: '428',
      name: 'If-Match required',
      path: 'src/Fugue.Application/Features/Customers/Commands/UpdateCustomer.cs',
      code: `if (command.ExpectedVersion is null)
{
    return Result.Fail<CustomerDto>(CustomerErrors.IfMatchRequired());
}`,
    },
    {
      status: '409',
      name: 'invalid transition',
      path: 'src/Fugue.Domain/Customers/Customer.cs',
      code: `if (Status != CustomerStatus.Active)
{
    return Result.Fail(InvalidTransition("suspend"));
}`,
      note: 'The rule lives in the aggregate, not in the controller.',
    },
    {
      status: '409',
      name: 'email taken',
      path: 'src/Fugue.Infrastructure/Persistence/Customers/CustomerRepository.cs',
      code: `catch (PostgresException ex) when (ex.SqlState == UniqueViolation)
{
    return Result.Fail<CustomerDto>(CustomerErrors.EmailTaken());
}`,
      note: 'The per-owner unique index decides; the insert and its audit row roll back.',
    },
  ],
};
