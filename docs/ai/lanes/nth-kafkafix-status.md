# Lane nth-kafkafix status (Refs D-064)

## Commits
- fix(messaging): the no-broker Kafka test relays only its own outbox row (this commit, with this status file)

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
- Full suite under the lock: see the lane report.

## Shared files changed
- None outside `tests/Fugue.Tests/Messaging/**` and this file. Product code unchanged.

## Next
- Full suite under the lock, then report.
