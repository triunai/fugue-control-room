import { HoodNote } from '../hood/hood-note';

export const REPORT_HOOD: HoodNote = {
  why: [
    'One form, one submit: the server validates and answers field errors (400) that land on the fields.',
    'Submit needs an Idempotency-Key. A retry of the same form reuses its key and replays the first reply; an edited form gets a new key.',
  ],
  flow: [
    {
      layer: 'web',
      text:
        'Submit sends the form with an Idempotency-Key: one key per form, kept on a retry of the same body, fresh when the body changes.',
      path: 'web/src/app/core/http/idempotency.ts',
    },
    {
      layer: 'api',
      text:
        'The [Idempotent(Required = true)] filter claims the key before the action runs; the same key with another body is 422, a repeat replays the first reply.',
      path: 'src/Fugue.Api/Idempotency/IdempotencyFilter.cs',
    },
    {
      layer: 'api',
      text:
        'POST /claims needs CLAIMS_SUBMIT and hands the body to the mediator as SubmitClaimCommand.',
      path: 'src/Fugue.Api/Controllers/ClaimsController.cs',
    },
    {
      layer: 'handler',
      text:
        'The validator checks the fields and the incident window (not future, not over a year back) before the handler runs: 400 validation.failed on a miss.',
      path: 'src/Fugue.Application/Features/Claims/Commands/SubmitClaim.cs',
    },
    {
      layer: 'handler',
      text:
        'The handler takes the caller as claimant, numbers the claim and builds it in status Submitted, then asks the repository to add it.',
      path: 'src/Fugue.Application/Features/Claims/Commands/SubmitClaim.cs',
    },
    {
      layer: 'db',
      text:
        'One transaction inserts the claim and its first timeline row with SQL (Dapper writes, ADR-006).',
      path: 'src/Fugue.Infrastructure/Persistence/Claims/ClaimRepository.cs',
    },
    {
      layer: 'audit',
      text:
        'The same transaction records the audit row (claims.submit), and the 201 reply is stored against the key.',
      path: 'src/Fugue.Infrastructure/Persistence/Claims/ClaimRepository.cs',
    },
    {
      layer: 'outbox',
      text:
        'The same transaction queues the claim event in the outbox, so the event exists if and only if the claim committed (ADR-011).',
      path: 'src/Fugue.Infrastructure/Outbox/OutboxWriter.cs',
    },
    {
      layer: 'webhook',
      text:
        'Later the outbox dispatcher relays the event: one pending delivery per matching active subscription, then a signed POST.',
      path: 'src/Fugue.Infrastructure/Webhooks/WebhookFanOutPublisher.cs',
    },
    {
      layer: 'search',
      text:
        'The claim indexer copies changed claims into Meilisearch, so the new claim becomes searchable after one interval.',
      path: 'src/Fugue.Infrastructure/Search/ClaimIndexer.cs',
    },
  ],
  how: [
    'Calls POST /claims with an Idempotency-Key header; it needs CLAIMS_SUBMIT and answers 201 with the claim and its ETag.',
    'Same key and same body replays the stored reply; same key with a different body is 422.',
    'The caller becomes the claimant and the claim starts Submitted; the incident date must be within the last year, not in the future.',
  ],
  adrs: [
    { label: 'ADR-013 Idempotency keys', path: 'docs/adr/ADR-013-idempotency-keys.md' },
    { label: 'ADR-004 Errors: FluentResults with ErrorType', path: 'docs/adr/ADR-004-fluentresults-with-errortype.md' },
  ],
  tests: [
    { layer: 'integration', path: 'tests/Fugue.Tests/Claims/ClaimsContractTests.cs', name: 'Submit_without_an_Idempotency_Key_is_400_and_creates_nothing_while_customers_create_stays_optional', asserts: 'Submit without an Idempotency-Key is 400 idempotency.key_required and creates no row; customer create keeps it optional.' },
    { layer: 'integration', path: 'tests/Fugue.Tests/Idempotency/IdempotencyTests.cs', name: 'The_same_key_with_a_different_body_is_422_key_reused_and_creates_nothing', asserts: 'The same key with a different body is 422 idempotency.key_reused and creates nothing (shown on customer create, same filter).' },
    { layer: 'integration', path: 'tests/Fugue.Tests/Claims/ClaimsContractTests.cs', name: 'Submit_validates_its_fields_and_the_incident_window', asserts: 'Bad fields or an incident date outside the window answer 400 validation.failed with the error on that field.' },
    { layer: 'api', path: 'bruno/fugue/claims/26-submit-without-a-key-is-400.bru', name: 'Submit without an Idempotency-Key is 400 (idempotency.key_required)', asserts: 'Submit with no key answers 400 problem+json with code idempotency.key_required.' },
    { layer: 'web', path: 'web/src/app/core/http/idempotency.spec.ts', name: 'gives a changed body a fresh key, because the same key with another body is refused with 422', asserts: 'An edited body gets a fresh Idempotency-Key, since the same key with another body would be refused.' },
  ],
  decisions: [
    { label: 'D-050 Idempotency keys with stored replay', path: 'docs/decisions.md' },
    { label: 'D-056 Submit key is required', path: 'docs/decisions.md' },
    { label: 'Duplicates and atomicity', path: 'docs/CLAIMS.md#4-concurrency-duplicates-and-atomicity' },
  ],
  guards: [
    {
      status: '400',
      name: 'idempotency.key_required',
      path: 'src/Fugue.Api/Idempotency/IdempotencyFilter.cs',
      code: `if (!header.IsPresent)
{
    if (context.ActionDescriptor.EndpointMetadata.OfType<IdempotentAttribute>().Any(a => a.Required))
    {
        context.Result = Problem(
            http, StatusCodes.Status400BadRequest, "idempotency.key_required",`,
      note: 'Submitting without a key is refused, so a retry cannot create a second claim.',
    },
    {
      status: '400',
      name: 'validation',
      path: 'src/Fugue.Application/Features/Claims/Commands/SubmitClaim.cs',
      code: `RuleFor(x => x.PolicyNumber).NotEmpty().MaximumLength(64);
RuleFor(x => x.IncidentDescription).NotEmpty().MaximumLength(4000);
…
.Must((command, date) => date <= Markets.LocalToday(command.Market, clock.GetUtcNow()))
.WithMessage("The incident date cannot be in the future.")`,
      note: 'Field errors come back as a validation problem and land on the fields.',
    },
    {
      status: '422',
      name: 'same key, different body',
      path: 'src/Fugue.Api/Idempotency/IdempotencyFilter.cs',
      code: `case IdempotencyDecision.KeyReused:
    context.Result = Problem(http, StatusCodes.Status422UnprocessableEntity, "idempotency.key_reused", "This Idempotency-Key was already used with a different request body.");`,
      note: 'The stored request hash differs from this body.',
    },
    {
      status: '403',
      name: 'permission',
      path: 'src/Fugue.Api/Controllers/ClaimsController.cs',
      code: `[HttpPost]
[HasPermission(Permissions.ClaimsSubmit)]
[Idempotent(Required = true)]`,
      note: 'Without CLAIMS_SUBMIT the submit is refused with 403.',
    },
  ],
};
