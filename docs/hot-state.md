# Fugue — hot state

Rules: ONE current `▶ RESUME HERE` block, at most ONE `(previous)` block. When a new block is written, the older one moves into today's `docs/project-log.md` entry. No state lives anywhere else. Keep this file under about 150 lines.

## ▶ RESUME HERE — 2026-11-25
- Tree: `main` (path: repo root)
- Doing: S0 and S1 finished (see `docs/plans/roadmap.md`). Nothing in flight.
- Next action: S2 persistence (local Postgres, EF Core 8). Start with "new task: …".
- Open threads: none active. Owner action: implement `ApiControllerBase.MapStatus`. Known red tests until then: the 8 `MapStatusTests` theory cases, `Failure_is_a_problem_json_with_code_type_and_trace_id`, `Validation_failure_carries_an_errors_dictionary`, `Ping_with_an_out_of_range_count_is_a_400_validation_problem`.

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
