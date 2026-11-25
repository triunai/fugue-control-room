# Seed template — copy to `docs/NEXT-SESSION-SEED-PROMPT-<YYYY-MM-DD>.md`

A seed is the first thing a cold session reads. Write it at wrap. Commit it in the SAME commit as the spine files. An untracked seed can be lost.
Keep the section order below. Traps come BEFORE findings. Keep it under about 120 lines. Plain words.

---

## 1. Read first
List 3 to 6 files in the order to read them. Say why each one. Start with `hygiene.md`.

## 2. State of the tree
- Live tree path and branch, last commit (short SHA + subject).
- Clean or dirty? List any uncommitted files and why.
- Other worktrees that matter.

## 3. TRAPS
Things that will bite a fresh session. One line each: the trap, the symptom, the fix or the guard. Examples: a path that looks right and is stale; a command that needs a flag; a check that passes but proves nothing.

## 4. FACTS — do not re-derive
Facts that were expensive to learn. One per line. Each carries the date and the method, so a later session can judge if it is stale.
Format: `- <fact> — verified <YYYY-MM-DD> by <command, query or file read>`.

## 5. NOT DONE
What is unfinished, per thread (`WS-NN`). For each: the literal next action.

## 6. Owner decisions owed
Calls only the owner can make. One line each: the question, the options, the cost of waiting.

## 7. Staffing rules
Which agent or model does what in this phase. Which lane may write which files. Reminder: lanes never edit spine files or mint numbers.

## 8. Start here
The first three actions for the next session, numbered.

## 9. How to write the next seed
Copy this template. Fill every section. Carry forward only facts still true. Drop what is done. Date the file with the wrap date. Add it to the wrap commit.
