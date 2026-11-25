# Landing ledger — template

Use when merging work from many lanes. Copy to `docs/ai/<thread-slug>/landing-ledger-<YYYY-MM-DD>.md`. One row per promise made to the owner.

Process: check the merged work on a throwaway `verify/*` branch. Fill the Proof column from that check. A row may carry `✅` only if the proof comes from someone other than the builder (a different agent, a different model family, or a human).

Status key:
- `✅` proven by a non-builder (SHA or check branch named in Proof)
- `🟡` built, proof pending
- `❌` failed the check; fix round open (cite the finding ID)
- `⏭` dropped on purpose; the reason is in Proof

| Row id | Promise (one line, plain words) | Proof (SHA or check branch, and who checked) | Status |
|---|---|---|---|
| L-01 | (example) Login rejects an expired token | `a1b2c3d`, checked by reviewer lane on `verify/2026-11-11` | 🟡 |
