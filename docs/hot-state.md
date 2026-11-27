# Fugue — hot state (the resume point)

OVERWRITTEN at every wrap or pause, never appended; at most 40 lines. After a wrap or days away: read this first, then do "Next action" exactly. History lives in `docs/project-log.md` and `CHANGELOG.md`.

## ▶ RESUME HERE — 2026-11-27 19:25 +08 (nice-to-haves wave; submission 2026-11-28 15:00)
Branch `main`, pushed to `origin` = github.com/triunai/fugue-control-room (public; the owner's old main is kept as branch `control-room-archive`). Push after each merge (pre-push runs the suite).
Last green sha: 8a103cc. Lead runs: build 0 warnings; ArchTests 37 + Tests 738; web 159, lint, build clean (CommonJS allow-list for Univer/ExcelJS in angular.json); Bruno `--env compose` 101/101 requests, 123/123 tests against the rebuilt stack (Meilisearch on); live checks: audit search 75 hits, users list 6, officer landing and claim record screenshotted.

## Last action done
Merged lanes nth-search, nth-api, nth-quality, nth-api2, nth-shell, nth-claimui (+ round 3 recovered by sha), nth-grid, nth-wire; assessment worksheet wired into the record; Fable review filed (D-061) and its docs done (CLAIMS.md boundaries/ER/sync-async, DEMO.md); roles matrix restyled (8a103cc).

## Next action (exact)
1. When lane **nth-kafka** (`.worktrees/nth-kafka`, 4 commits at dbe7b7c) replies: verify (scope, `dotnet test`, `docker compose config` + `--profile kafka config`), merge `--no-ff`, rebuild, push. The owner asked "why Kafka, not my stack": keep it OPTIONAL (profile `kafka`); drop it only if the owner says so.
2. When lane **nth-polish** (`.worktrees/nth-polish`, base 8a103cc) replies: verify, merge, rebuild web, push. It matches the Univer sheets to the owner's reference (ribbon, toolbar, formula bar, tinted inputs) and unifies breadcrumb and group-header typography.
3. Commit the uncommitted `README.md` (personas + "Beyond the brief" table; it lists Kafka: keep only if merged), then wrap: close W-022/W-024/W-019/W-021 with `close-workstream.mjs`, `npm run trace` alone, push.

## Uncommitted work in progress
`README.md` (main): persona rows and "Beyond the brief" table, waiting for the Kafka merge.

## In flight (open W ids)
W-022 nice-to-haves (lanes nth-kafka, nth-polish) · W-024, W-019, W-021 done in code, to close · W-023 versioning/cache/OTel · W-010 Codex push gate (a remote now exists).

## Owner decisions 19:27 (record as D-062 at wrap)
- FEATURE FREEZE: polish only from here. Kafka: keep it (optional profile) but keep it out of the demo and README highlights.
- Version: web shows v0.0.2 (nth-polish); at wrap add a `## v0.0.2 — 2026-11-27` release entry to CHANGELOG.md and tag `v0.0.2`.

## Pending owner questions
- The owner's own words for trimming Univer and the calendar on memory-capped deploys (still not invented).

## Demo notes
`docker compose up -d --build` (needs `MEILI_MASTER_KEY` in `.env`; the quickstart sed fills it). Console http://localhost:8080 (`?tour=off` while presenting) · API :5081 · Meilisearch :7700. Script: `docs/DEMO.md`. Private prep: `~/Repos/personal-work/interview-prep/walkthrough-prep.md` sections 0b and 12.

## Owed inbox
- Failure modes 30-32 recorded; stray pnpm files keep appearing in new worktrees (delete).
- nth-quality flagged two doc contradictions it did not fix: context-reset threshold 50% (`docs/conventions/doc-spine.md`) vs 60% elsewhere; `.githooks/README.md` says to set `core.hooksPath`, which AGENTS.md forbids.
