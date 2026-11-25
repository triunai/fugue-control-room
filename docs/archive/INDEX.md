# Archive index (cold tier)

Cold storage. Agents NEVER bulk-read this folder: grep this index first and open only a file it points to. Every file in `docs/archive/` has one line here. Nothing in the active, warm or spine tiers may be moved here without a line added in the same commit.

| File | What it holds | Revive or read when |
|---|---|---|
| `deferred.md` | Far-ahead and deferred items pruned from the roadmap and board, each with a "revive when" trigger | planning past S5, or a trigger in the file fires |
