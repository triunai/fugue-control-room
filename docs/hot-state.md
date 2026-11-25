# Fugue — hot state

Rules: ONE current `▶ RESUME HERE` block, at most ONE `(previous)` block. When a new block is written, the older one moves into today's `docs/project-log.md` entry. No state lives anywhere else. Keep this file under about 150 lines.

## ▶ RESUME HERE — 2026-11-25
- Tree: `main` (path: repo root)
- Doing: S0, S1 and S2 finished (see `docs/plans/roadmap.md`). Nothing in flight.
- Next action: S3 identity (JWT, policies). Start with "new task: …".
- Open threads: none active. Owner action: implement `ApiControllerBase.MapStatus`. Known red tests until then: the 8 `MapStatusTests` cases, `Failure_is_a_problem_json_with_code_type_and_trace_id`, `Validation_failure_carries_an_errors_dictionary`, `Ping_with_an_out_of_range_count_is_a_400_validation_problem`, and 5 in `CustomersApiTests` (stale 412, missing If-Match 428, suspend twice 409, duplicate email 409, unknown id 404). Bruno: requests 06, 07, 09, 13 (8 assertions).
- Local env: runbook 03 (Docker env vars, `.env`, port clash on 5432 handled with `POSTGRES_PORT`).

## Owed inbox
One line per item that the next wrap must handle. Format: `- [ ] <what> — <who/why> — <since YYYY-MM-DD>`. Tick and remove when done.
- (empty)

## Hydration ladder
Read only as deep as the task needs. Stop when you can act.

| Level | Read | You get |
|---|---|---|
| 0 | the `▶ RESUME HERE` block above | where to resume |
| 1 | newest `docs/NEXT-SESSION-SEED-PROMPT-*.md` | traps, facts, what is not done |
| 2 | `docs/workstreams.md` thread block + its `Refs:` | one thread in full |
| 3 | `docs/decisions.md`, `docs/state-backlog.md`, `docs/project-log.md` | why, what is open, what happened |
| 4 | `docs/ai/` evidence, `docs/findings/` | raw proof behind a claim |
