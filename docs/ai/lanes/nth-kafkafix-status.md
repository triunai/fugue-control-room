# Lane nth-kafkafix status (Refs D-064)

## Commits
- fix(messaging): the no-broker Kafka test relays only its own outbox row (`7e50a5b`)
- docs(messaging): nth-kafkafix lane status with the full-suite counts (this commit)

## Root cause (evidence)
- During a live full run (another lane's, 12:01), a read-only query on its Testcontainers Postgres showed
  `pending|227` rows in `outbox_messages` and one backend `idle in transaction` for `00:04:04` on
  `-- Relay: opens the per-message savepoint. Caller: OutboxDispatcher.` / `SAVEPOINT r`.
- `WebhookHarness.StartAsync` moves the clock to T0 (2040), so every row other tests left pending is due.
  `claim-messages.sql` claims up to `BatchSize` 500 of them, and the relay publishes them one by one to `127.0.0.1:1`.
  Each waits the full `MessageTimeoutMs` (3000), all in ONE transaction: 227 x 3 s is over 11 minutes for one test.
- Alone the table is nearly empty, so Messaging passes 2/2 in 8 s. The earlier green run (11:29) had fewer leftover rows.

## Fix
- `tests/Fugue.Tests/Messaging/KafkaPublisherTests.cs`: before seeding, the no-broker test marks the DUE leftover pending
  rows dispatched (what the working-relay tests already do to them), and asserts `summary.Relayed == 1`.
- Guard proof: replacing the update with one extra pending row makes the test fail `Expected: 1 Actual: 2` (9 s instead of 8 s).

## Gates (copied from output)
- `dotnet build`: 0 Warning(s), 0 Error(s)
- `dotnet format --verify-no-changes`: exit 0
- Messaging: `Passed!  - Failed: 0, Passed: 2, Skipped: 0, Total: 2, Duration: 8 s - Fugue.Tests.dll (net8.0)`
- Full suite under the lock, run 1 (12:09:36-12:11:09, `--blame-crash --blame-hang-timeout 5m`):
  `Passed!  - Failed: 0, Passed: 37, Skipped: 0, Total: 37 - Fugue.ArchTests.dll`
  `Passed!  - Failed: 0, Passed: 849, Skipped: 0, Total: 849, Duration: 1 m 30 s - Fugue.Tests.dll`
- Full suite under the lock, run 2 (12:11:24-12:12:54): `Total tests: 37 Passed: 37`, `Total tests: 849 Passed: 849`;
  `Passed ...With_no_broker_the_outbox_row_is_not_marked_dispatched_and_is_retried_later [3 s]`

## Shared files changed
- None outside `tests/Fugue.Tests/Messaging/**` and this file. Product code unchanged.

## Next
- None: lane READY.
