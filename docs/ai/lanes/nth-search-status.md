# Lane nth-search status

Refs: W-021, D-058. Base b875f49.

## Commits
- `78a5b57` docs(runbooks): audit search (runbook 06 section "Search"). Landed first because the first attempts of the two feat commits were rejected (hook: dotnet off PATH, then a body line over 100 chars); history is not reordered (no amend).
- `2bc5cd3` feat(infra): Meilisearch audit index and indexer.
- `addaebe` feat(api): audit search endpoint (snapshot, regenerated client, Bruno, tests).

## Validation (copied from output)
- `dotnet build`: 0 Warning(s), 0 Error(s).
- `dotnet test`: Fugue.ArchTests 37 passed; Fugue.Tests 632 passed (623 + 9 new in `AuditSearchTests`, real Meilisearch v1.12.8 via Testcontainers).
- `dotnet format --verify-no-changes`: clean. `npm run api:openapi`: snapshot +184 lines. `cd web && npm run api:gen && npm test` 62 passed, `npm run build` ok.
- `docker compose config` (with MEILI_MASTER_KEY and POSTGRES_PASSWORD set): valid.

## Design facts
- No migration. Checkpoint = one document `audit` in the engine index `audit_checkpoint` (occurredAt + id); moved only after the batch task succeeded (at-least-once, idempotent upsert by id).
- Indexed: action, entity type/id, actor display/id, outcome, error code/message, route, time. `changes` and `metadata` are not indexed.
- Off (no `Search:Meilisearch:Url`): `DisabledAuditSearch`, endpoint 503 `search.unavailable`; an unreachable engine gives the same 503. Missing 4 hits: none.
- New runtime SQL `audit/index-batch.sql` (header `Caller: AuditIndexSource`); fitness rules green, no baseline change.
- Image pinned `getmeili/meilisearch:v1.12.8` in compose and in `AuditSearchTests`.

## Deviations and gaps
- Cut order not needed: highlights and date filters shipped.
- compose now requires `MEILI_MASTER_KEY` in `.env` (`:?`, like POSTGRES_PASSWORD), so the lead must add it before `docker compose up` (16+ bytes, `openssl rand -base64 24`). The api waits for meilisearch healthy.
- Highlights are not HTML-escaped (documented in the DTO and runbook); a console must escape all but `<em>`.
- Not verified: the full compose stack (not started, by instruction), the host-run API against Meilisearch end to end (covered by the Testcontainers tests through the real host instead), the Bruno requests (not executed). Settle with `docker compose up -d` after merge, then `GET /api/v1/audit/search?q=...` as admin.
- `estimatedTotalHits` is the engine estimate, not an exact count.
