# Evidence tree (`docs/ai/`)

Raw output from audits, traces, reviews and experiments. Proof lives here; conclusions live in decisions and findings.

Rules:
- One folder per thread: `docs/ai/<thread-slug>/`. Each folder has a `README.md` that routes to its files, newest first.
- Lanes write their deliverables here under names given in the brief. The orchestrator adds the line to the README router.
- Never edit a historical file after the fact. To correct one, write a new file and link to it from the README.
- Name files `<YYYY-MM-DD>-<topic>.md`. Do not name a deliverable `REPORT-*.md`.
- Every claim of a count or "not found" states the command that produced it.
- Every folder is listed below. No orphan folders.

## Index (newest first)
| Folder | Thread | What it holds |
|---|---|---|
| `reviews/` | review-fix loop | One file per review round: `<YYYY-MM-DD>-<WS>-r<N>.md` |
| (add a row per new folder) | | |

Deferred debt is tracked in `docs/ai/tax-ledger.md`.
