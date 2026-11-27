# Codex diff review: W-007 idempotency (7c8d082..c54a977 rebased), 2026-11-27

`codex exec -s read-only`. Verdict REVISE (5 MEDIUM, 1 LOW, no HIGH). All six fixed in dde6eb6 and d9b67fb with regression tests; the raw-sql-writes baseline SHRANK to one file instead of growing. Not added: deterministic expiry/replacement/purge contention tests (follow-up).


1. **MEDIUM — `src/Fugue.Api/Idempotency/IdempotencyFilter.cs:35`**  
   Unkeyed webhook creates bypass the filter’s `Cache-Control: no-store` assignment and return the plaintext signing secret without it. This violates decision F. **Fix:** apply `no-store` to webhook-create responses independently of header presence; test keyed and unkeyed creates.

2. **MEDIUM — `web/src/app/features/webhooks/sink-tab.ts:94`**  
   Sink creation generates its key inside `mutationFn`, so every retry gets another key. After a committed create whose response is lost, retrying while the subscriptions query remains stale can create another subscription. `sendTest` similarly regenerates both body and key. **Fix:** retain the submission body and key across attempts, resetting after success or an explicit new submission; add component-level retry tests.

3. **MEDIUM — `src/Fugue.Api/Idempotency/IdempotencyKeyHeader.cs:35`**  
   A present empty or whitespace-only header is treated as absent, allowing ordinary execution without claiming a key. Decision B requires invalid supplied keys to return 400; only an absent header bypasses idempotency. **Fix:** return `HeaderParse.Invalid` here and correct the tests that currently require this bypass.

4. **MEDIUM — `tests/Fugue.ArchTests/Baselines/raw-sql-writes.txt:11`**  
   Adding `IdempotencyStore.cs` widens the explicitly shrink-only baseline, contrary to decision H. It also exempts future unrelated SQL writes added to this file. **Fix:** remove the entry and introduce a narrowly enforced operational-write exemption restricted to the `[NoAudit]` idempotency table, with a fitness test preventing broader writes.

5. **MEDIUM — `tests/Fugue.Tests/Idempotency/IdempotencyTests.cs:402`**  
   The injected 5xx occurs in URL validation, before any business commit. The oversize-response test covers one post-commit branch, but completion-store failure, post-commit cancellation, and serialization/response-write exceptions remain untested. **Fix:** inject those failures after committing, then assert the key remains occupied and retries produce exactly one business row, audit row, and applicable outbox event. Add deterministic expiry/replacement/purge contention tests and chunked-body limit coverage.

6. **LOW — `src/Fugue.Api/OpenApi/FugueOperationFilter.cs:189`**  
   `maxLength: 255` constrains the serialized header, while the parser limits the decoded key. A valid 255-character quoted key occupies 257 characters, or up to 512 with escapes. **Fix:** describe decoded length separately and allow the full encoded length in the schema; regenerate the snapshot/client.

Static review only; tests were not run. `git diff --check 7c8d082..HEAD` passed.

VERDICT: REVISE