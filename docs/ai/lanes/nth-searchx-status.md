# Lane nth-searchx status (Refs D-064, overnight item 10: claims search hardening)

## Commits
- slice 1/3: feat(api): claims index settings applied idempotently, market names, synonyms (this commit)

## Gates (copied from output)
- `dotnet build`: Build succeeded. 0 Warning(s) 0 Error(s)
- `dotnet format --verify-no-changes`: exit 0
- `dotnet test --filter ClaimSearchTests`: Passed! - Failed: 0, Passed: 10, Skipped: 0, Total: 10
- Injected `Matches => false`: the idempotency test fails (Expected: 1, Actual: 2), restored.

## What changed in shared files (exactly)
- none (all changes under `src/Fugue.Infrastructure/Search/` and `tests/Fugue.Tests/Claims/ClaimSearchTests.cs`)

## Behaviour and design choices
- Settings live in `ClaimIndexSettings`; at startup (the indexer's first cycle) the adapter reads the engine's settings and sends an
  update only when searchable (ordered), filterable, sortable or synonyms differ. A restart sends none.
- New document field `marketName` ("Singapore"); `market` and `line` are now searchable. A document shape version in the checkpoint
  re-sends every claim once when the shape changes (old checkpoints have none), so existing indexes pick up `marketName`.
- Synonyms (two-way, lower case): each of the six market codes and its name (sg/singapore, my/malaysia, th/thailand, hk/hong kong,
  id/indonesia, ph/philippines), car/motor, home/property.
- `typoTolerance.disableOnAttributes` is NOT used: on the pinned v1.12.8 a word only present in such an attribute of a document
  added after the setting is never found (minimal repro: 1.12.8 returns [] for the exact number, 1.13.3 returns the claim).

## Deviations and gaps
- No row for nth-searchx in `docs/branch-ledger.md`; not added (the addendum allows only the Status cell of an own row).

## Next slice
- 2/3: query side: exact identifiers as phrases, matchingStrategy all, highlights and facets in the API; OpenAPI regen.
