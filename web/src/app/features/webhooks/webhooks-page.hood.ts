import { HoodNote } from '../hood/hood-note';

export const WEBHOOKS_HOOD: HoodNote = {
  why: [
    'Subscribers get signed HTTP callbacks for domain events. An event exists if and only if its change commits: a transactional outbox.',
    'Delivery is at-least-once with backoff and a dead letter; every attempt is counted and visible on the Deliveries tab.',
  ],
  flow: [
    {
      layer: 'outbox',
      text: 'A change raises a domain event; the outbox interceptor adds it to the SAME SaveChanges (a Dapper insert enqueues it in its transaction).',
      path: 'src/Fugue.Infrastructure/Outbox/OutboxSaveChangesInterceptor.cs',
    },
    {
      layer: 'job',
      text: 'The dispatcher service runs a cycle on a loop; relay claims pending messages with FOR UPDATE SKIP LOCKED.',
      path: 'src/Fugue.Infrastructure/Persistence/Sql/webhooks/claim-messages.sql',
    },
    {
      layer: 'webhook',
      text: 'The fan-out publisher inserts one delivery per active subscription whose filter matches; the message is marked dispatched.',
      path: 'src/Fugue.Infrastructure/Webhooks/WebhookFanOutPublisher.cs',
    },
    {
      layer: 'job',
      text: 'Deliver: due deliveries are leased (the attempt is counted at claim) and sent with no transaction open.',
      path: 'src/Fugue.Infrastructure/Webhooks/OutboxDispatcher.cs',
    },
    {
      layer: 'webhook',
      text: 'WebhookSender signs v1 = HMAC-SHA256(secret, timestamp.rawBody) and POSTs; only a 2xx is success, redirects are not followed.',
      path: 'src/Fugue.Infrastructure/Webhooks/WebhookSender.cs',
    },
    {
      layer: 'db',
      text: 'A failure waits an exponential backoff with jitter; at the attempt limit the delivery is dead. Redeliver revives it.',
      path: 'src/Fugue.Infrastructure/Webhooks/Backoff.cs',
    },
    {
      layer: 'api',
      text: 'In Development the sink endpoint receives the signed calls (the signature is the credential); the Sink tab shows them.',
      path: 'src/Fugue.Api/Controllers/DevWebhookSinkController.cs',
    },
  ],
  how: [
    'Subscriptions: POST/GET/PUT/DELETE /webhooks/subscriptions, all WEBHOOKS_MANAGE; PUT, DELETE and rotate-secret need If-Match.',
    'The secret is shown once, on create and on rotate; only Data Protection ciphertext is stored, and never in the audit log.',
    'Destination URLs are SSRF-guarded at subscribe time and again at connect time (private, loopback, link-local, metadata).',
    'GET /webhooks/deliveries lists attempts; POST /webhooks/deliveries/{id}/redeliver revives a dead one.',
  ],
  adrs: [{ label: 'ADR-011 Outbox and signed webhooks', path: 'docs/adr/ADR-011-outbox-and-signed-webhooks.md' }],
  tests: [
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Webhooks/WebhookDeliveryTests.cs',
      name: 'A_delivery_carries_a_valid_signature_and_every_documented_header',
      asserts: 'A delivery carries a valid v1 signature and every documented header.',
    },
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Webhooks/WebhookDeliveryTests.cs',
      name: 'Failures_up_to_the_limit_dead_letter_the_delivery_and_redeliver_revives_it',
      asserts: 'Failures up to the limit dead-letter the delivery; redeliver revives it.',
    },
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Webhooks/WebhookDeliveryTests.cs',
      name: 'Two_dispatchers_over_the_same_messages_deliver_each_exactly_once',
      asserts: 'Two dispatchers over the same messages deliver each exactly once (SKIP LOCKED and leases).',
    },
    {
      layer: 'integration',
      path: 'tests/Fugue.Tests/Webhooks/OutboxCaptureTests.cs',
      name: 'Rolling_back_a_unit_of_work_removes_the_EF_path_event_with_the_change',
      asserts: 'Rolling back a unit of work removes the outbox event together with the change.',
    },
    {
      layer: 'api',
      path: 'bruno/fugue/webhooks/05-poll-the-sink-for-the-signed-event.bru',
      name: 'Poll the sink until the signed customer.created.v1 event arrives',
      asserts: 'Creating a customer ends with the signed customer.created.v1 event at the Development sink.',
    },
  ],
  decisions: [
    { label: 'D-037 Transactional outbox, same transaction', path: 'docs/decisions.md' },
    { label: 'D-038 Secrets in Data Protection, SSRF guard', path: 'docs/decisions.md' },
    { label: 'D-039 Dispatcher: leases, SKIP LOCKED, outbox is the retry authority', path: 'docs/decisions.md' },
    { label: 'D-041 Review fixes: claim-token fence, spent-lease dead letter', path: 'docs/decisions.md' },
  ],
  guards: [
    {
      status: '403',
      name: 'permission (WEBHOOKS_MANAGE)',
      path: 'src/Fugue.Api/Controllers/WebhooksController.cs',
      code: `[HttpPost("subscriptions")]
[HasPermission(Permissions.WebhooksManage)]`,
      note: 'Every subscription and delivery endpoint carries the same attribute.',
    },
    {
      status: 'rule',
      name: 'SKIP LOCKED relay',
      path: 'src/Fugue.Infrastructure/Persistence/Sql/webhooks/claim-messages.sql',
      code: `WHERE status = 'pending' AND next_attempt_at <= @Now
ORDER BY next_attempt_at, id
LIMIT @Batch
FOR UPDATE SKIP LOCKED`,
      note: 'A second dispatcher skips the rows the first holds, so many can run at once.',
    },
    {
      status: 'sig',
      name: 'HMAC-SHA256 v1',
      path: 'src/Fugue.Application/Features/Webhooks/WebhookSecretFormat.cs',
      code: `byte[] prefix = Encoding.UTF8.GetBytes(unixSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
…
return SignatureVersion + "=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), message)).ToLowerInvariant();`,
      note: 'The receiver recomputes it over the timestamp and the raw body.',
    },
    {
      status: 'dead',
      name: 'dead letter',
      path: 'src/Fugue.Infrastructure/Webhooks/OutboxDispatcher.cs',
      code: `bool dead = deadNow || delivery.Attempts >= opts.MaxAttempts;
…
Status = dead ? WebhookDeliveryStatuses.Dead : WebhookDeliveryStatuses.Pending,
NextAttemptAt = dead ? now : now + Backoff.Delay(delivery.Attempts, opts, Random.Shared.NextDouble()),`,
      note: 'An inactive subscription or an undecryptable secret dead-letters at once.',
    },
  ],
};
