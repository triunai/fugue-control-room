# Lane nth-searchx status (Refs D-064, overnight item 10: claims search hardening)

## Commits
- `7c2ad8a` slice 1/3: feat(api): claims index settings applied idempotently, market names, synonyms
- `fe102f5` slice 2/3: feat(api): exact claim and policy numbers, every word must match, highlights, facets
- `77feee8` fix(api): claim documents no longer store an empty `_formatted` field (found in the headless run's index stats)
- `7b8f548` slice 3/3: feat(web): header claim hits show why they matched and the match counts
- docs(ai): this status update with the full-suite result

## Gates (copied from output)
- `dotnet build`: Build succeeded. 0 Warning(s) 0 Error(s)
- `dotnet format --verify-no-changes`: exit 0
- `dotnet test --filter ClaimSearchTests`: Passed! - Failed: 0, Passed: 10, Skipped: 0, Total: 10
- Injected `Matches => false`: the idempotency test fails (Expected: 1, Actual: 2), restored.
- Slice 2: `dotnet build` 0 Warning(s) 0 Error(s); format exit 0; `--filter OpenApi|ClaimSearch|AuditSearch`: Passed! Failed: 0,
  Passed: 47; `Fugue.ArchTests`: Passed! Failed: 0, Passed: 37; web `tsc` app and spec: exit 0.
- Injected "query sent unquoted": the exact-number test fails (DoesNotContain: the mistyped number found the claim), restored.
- Fix: new test `Indexed_claims_carry_no_stored_formatted_field` fails without the `JsonIgnore` (Failed: 1) and passes with it;
  `--filter ClaimSearchTests`: Passed! Failed: 0, Passed: 23; format exit 0.
- Slice 3 (web): `npm run lint`: All files pass linting; `npm test`: Test Files 44 passed, Tests 270 passed (4 new in
  `global-search.spec.ts`); `npm run build`: Initial total 458.34 kB raw, 120.66 kB transfer (base 120.56 kB).
  Injected the old bare-interpolation template: the new spec fails (Received " CLM-SG-2026- 000009"), restored.
- Headless (Playwright, staff and self on :4404 -> API :5204 -> Meilisearch v1.12.8): 13/13 checks PASS, 0 console errors.
- Full suite under the lock at `7b8f548`: Fugue.ArchTests `Passed! - Failed: 0, Passed: 37`; Fugue.Tests
  `Passed! - Failed: 0, Passed: 776, Skipped: 0, Total: 776, Duration: 11 m 15 s`.
- Live restart of the API against the same engine: no "settings differed" log line, `settingsUpdate` tasks for `claims` stay 1.

## What changed in shared files (exactly)
- slice 1: none (all changes under `src/Fugue.Infrastructure/Search/` and `tests/Fugue.Tests/Claims/ClaimSearchTests.cs`)
- slice 3: none outside `web/src/app/features/shell/global-search*`
- slice 2: `src/Fugue.Api/Controllers/ClaimsController.cs` (the Search action's doc comment only), `docs/api/openapi.v1.json` and
  `web/src/app/api/generated/**` (regenerated: `highlights` on a hit, `facets` on the result, both optional)

## Behaviour and design choices
- Settings live in `ClaimIndexSettings`; at startup (the indexer's first cycle) the adapter reads the engine's settings and sends an
  update only when searchable (ordered), filterable, sortable or synonyms differ. A restart sends none.
- New document field `marketName` ("Singapore"); `market` and `line` are now searchable. A document shape version in the checkpoint
  re-sends every claim once when the shape changes (old checkpoints have none), so existing indexes pick up `marketName`.
- Synonyms (two-way, lower case): each of the six market codes and its name (sg/singapore, my/malaysia, th/thailand, hk/hong kong,
  id/indonesia, ph/philippines), car/motor, home/property.
- `typoTolerance.disableOnAttributes` is NOT used: on the pinned v1.12.8 a word only present in such an attribute of a document
  added after the setting is never found (minimal repro: 1.12.8 returns [] for the exact number, 1.13.3 returns the claim).

- Query side: tokens with a digit and 5+ characters are sent as phrases (exact, no typo, no prefix); a claim number still being
  typed (`CLM-` but incomplete) stays a prefix. `matchingStrategy: all`: every word must match (through a synonym or a typo).
- Each hit has `highlights` (only the fields that matched, `<em>` tags, description cropped to 24 words around the match); the
  result has `facets` (status, market, line counts over all matches, under the same scope filter as the hits).

- Web: each claim hit shows the number, market, line, place and description with the matched words marked, a "Matched on"
  line for matches in fields the row does not show (policy, claimant, market name), and above the hits the counts over all
  matches ("2 matches · Market SG 2 · Line Motor 2 · Status Settled 1, Submitted 1"). Counts are read-only text, not filters.

## Deviations and gaps
- No row for nth-searchx in `docs/branch-ledger.md`; not added (the addendum allows only the Status cell of an own row).

- No new Bruno request: the endpoint is unchanged and the Bruno env usually runs without an engine (503 branch); indexing is
  eventually consistent, so a highlight assertion there would be timing-dependent. Covered by the integration tests instead.

## Next slice
- none: lane complete. Report: `.worktrees/_briefs/reports/nth-searchx.md`.
