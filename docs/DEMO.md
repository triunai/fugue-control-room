# Demo script (15 to 20 minutes)

The walkthrough order, one line to say at each step. Every ask in the brief is served by steps 4 to 13. Setup: `docker compose up -d --build` (runbook 00), a fresh seed, two browser windows side by side (claimant left, staff right) from step 4. Passwords: runbook 05. Add `?tour=off` to the first URL in each window so the reviewer tour does not start while presenting.

| # | Min | Screen or doc | Say |
|---|---|---|---|
| 1 | 0:00 | `README.md`, the disclosure | "The starter was built before the window and disclosed; everything after the `claims-window-start` tag is the timed build, one `git diff`." |
| 2 | 1:00 | `docs/CLAIMS.md` section 1, the service-boundaries picture | "One deployable with a claims module. The boundary is the transaction: state, timeline, audit and event commit together. Solid edges are synchronous, dotted are events; notifications split out first." |
| 3 | 2:30 | `docs/CLAIMS.md` section 2 (state machine, entities) and section 5 (sync/async table) | "Commands are REST because the caller needs the guard's verdict now; every transition is also an event through the outbox, and that is where Kafka plugs in." |
| 4 | 4:00 | Claimant `self@fugue.test`: the landing, "We need one answer from you" | "A claimant lands on the one thing that needs them. Staff names and the reserve never reach this view." |
| 5 | 5:00 | Claimant: answer the question; the claim track on the banner moves | "Answering is a guarded command on their own claim only; another claimant probing it gets 404, not 403." |
| 6 | 6:00 | Claimant: Report an incident | "Submit requires an Idempotency-Key; a retry replays the first response, because a duplicate claim is real harm." |
| 7 | 7:00 | Officer `staff@fugue.test`: My work, "Pick up next" | "Same Angular app, different landing: navigation and buttons come from permission codes on `/me`, never role names." |
| 8 | 8:00 | Officer: the claim record. The next-step card says "Blocked: set a reserve"; set it; "Ready"; approve | "The card mirrors the aggregate's guards and the API still enforces them; approval cannot exceed the reserve." |
| 9 | 9:30 | The claimant window, untouched: the status changes on its own | "The async side on the frontend: refetch on focus and polling today; server-sent events fed by the same outbox events with more time." |
| 10 | 10:30 | Manager `manager@fugue.test` acts on a claim the officer just changed: the 412 banner | "ETag and If-Match on every command; stale is checked before the state machine, so you are told to re-read." |
| 11 | 11:30 | Manager: Team and exposure; settle from "To settle" | "Exposure is reserve on open claims plus approved-not-settled, per currency. No grand total: SGD plus IDR needs a rate and a rate date, a finance decision." |
| 12 | 13:00 | Same page: "unreserved" counts | "An open claim with no reserve is unknown exposure, so the page counts it instead of showing zero." |
| 13 | 14:00 | Admin `admin@fugue.test`: the claim's audit history, then Webhooks, the dev sink tab with `claim.*` events | "Three records, three audiences: the timeline for the claimant, the audit diff for admins, events for other systems, all in one transaction." |
| 14 | 15:30 | `docs/ai-journal.md`: the 17:20, 17:50 and 18:00 entries | "The journal records where a check contradicted an agent's report, and what I overrode. A review is evidence, not a verdict." |
| 15 | 17:00 | `docs/CLAIMS.md` section 8 | "What I cut, in the order I cut it, and what comes next." |

## Keep in the product, off the screen (unless asked)
Customers (the starter's sample), webhook subscription management, groups and overrides and the roles matrix, audit search, the reserve sheet and SLA calendar, the reviewer tour, mutation/pairwise/load reports, module toggles (20 seconds, only if "is it really modular?" comes up), runbooks.
