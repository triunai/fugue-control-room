# Findings — staging ledger

A finding is a fact about how the system BEHAVES that must outlive the handoff. This is a staging area, not a home. Every row must name its permanent home. An empty Promoted-to cell means unfinished work.

Add a row only when a fact will matter after this session and is not already written down. Do not log opinions or tasks (tasks go to `docs/state-backlog.md`).

Layout: one folder per month, one file per week, named by that week's Monday: `docs/findings/<YYYY-MM>/<YYYY-MM-DD>.md`. Each weekly file has this table:

| Date | Finding | Evidence (command, file, query) | Thread | Promoted to |
|---|---|---|---|---|
| 2026-11-11 | (example) Service X returns 200 on a failed write | `curl` run, see `docs/ai/<thread>/README.md` | WS-NN | `docs/decisions.md` D-NNN or a doc path |

Promoted-to values: a decision (`D-NNN`), a backlog item (`#NNN`), a convention or architecture doc path, or `rejected: <one-line reason>`.
Review at wrap step 6: any row with an empty Promoted-to cell gets a home or a rejection.
