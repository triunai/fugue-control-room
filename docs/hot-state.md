# Fugue — hot state (the resume point)

OVERWRITTEN at every wrap or pause, never appended; at most 40 lines. After a wrap or days away: read this first, then do "Next action" exactly. History lives in `docs/project-log.md` and `CHANGELOG.md`.

## ▶ RESUME HERE — 2026-11-27 19:50 +08 (context-cap wrap; window hard stop 21:56; submission 2026-11-28 15:00)
Branch `main` at 80a1fbe (+ this wrap commit), remote `origin` = github.com/triunai/fugue-control-room (public; owner's old main kept as `control-room-archive`). **origin is at 03d7002; local main is NOT pushed: the console is BLANK in the browser (NG0203 at bootstrap)** although unit 191 / lint / build pass.
Last green sha (backend + Bruno + live browser): 8d486c0 (ArchTests 37 + Tests 738; Bruno 101/101, 123/123; live officer landing and claim record rendered). Web unit at 80a1fbe: 191, lint, build clean.

## In flight (background agents of the previous session; check git, do not trust their replies)
- **ng0203** debugger: fixing the blank console on main (cause in nth-polish 03d7002 or nth-hood 80a1fbe merge); commits a `fix(web)` on main by pathspec. Verify: `git log --oneline -3`, `cd web && npm test && npm run build`, then `docker compose up -d --build web` and LOAD http://localhost:8080 in a browser (sign-in page lists accounts; sign in as staff@fugue.test; no console errors).
- **nth-kafka** (`.worktrees/nth-kafka`, 4 commits at dbe7b7c, never reported): verify scope, `dotnet test`, `docker compose config` and `--profile kafka config`, merge `--no-ff`. Owner: keep Kafka, ignore it (out of demo and README highlights).
- **nth-hood** worktree `.worktrees/nth-hood`: merged in 80a1fbe; retire it (`git log main..nth-hood` must be empty) and its ledger row.

## Next action (exact, in order)
1. Blank console fixed and seen rendering live -> push. 2. Merge nth-kafka, rebuild stack, full Bruno (`cd bruno/fugue && npx bru run --env compose`), push. 3. Commit `README.md` (uncommitted: personas + "Beyond the brief"; drop "highlights" wording for Kafka per owner). 4. D-062 (feature freeze, keep Kafka but ignore it, v0.0.2); failure mode 33 (a web merge can pass unit/lint/build and still not boot: load the page before pushing). 5. CHANGELOG `## v0.0.2 — 2026-11-27` release entry + `git tag v0.0.2`; close W-022, W-024, W-019, W-021 with `close-workstream.mjs`; `npm run trace` alone; push with tags.

## Owner decisions (19:27-19:40)
Feature freeze (polish only). Kafka kept, ignored. v0.0.2 in sidebar (done) + changelog/tag. Under-the-hood sidebar approved as layout A, live calls first (built, merged 80a1fbe, unverified live).

## Pending owner questions
The owner's own note on trimming Univer and the calendar for memory-capped deploys (not invented).

## Demo notes
`docker compose up -d --build` (MEILI_MASTER_KEY filled by the quickstart sed). Console :8080 (`?tour=off&hood=off` while presenting), API :5081, Meilisearch :7700. Script `docs/DEMO.md`; private prep `~/Repos/personal-work/interview-prep/walkthrough-prep.md` sections 0b, 12.

## Owed inbox
nth-quality flagged: context threshold 50% (`docs/conventions/doc-spine.md`) vs 60% elsewhere; `.githooks/README.md` says set `core.hooksPath` (AGENTS.md forbids). nth-polish: banner/tracks/Help sit inside commit c193a2e (message names only the SLA fix); assessment label clipping not reproduced, not proven fixed.
