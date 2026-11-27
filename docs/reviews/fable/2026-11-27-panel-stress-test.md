# Fable review: fresh-panelist stress test, 2026-11-27

A read-only review by a model with no stake in the build, playing a skeptical senior engineer who sees the take-home tomorrow. Unlike the earlier interview-readiness review, every flow here was driven live against the Docker stack (console :8080, API :5081) between 20:05 and 20:20 +08, in separate browser contexts per persona, plus curl against the API. Screenshots are named `NN-*.png`; they sit in the session scratchpad folder `panel/` (not committed). Anything not checked in code or in the running app is marked UNVERIFIED. The stack restarted once at about 20:08 during the run; nothing below is a restart artefact unless it says so.

## 1. Verdict

1. **Hire signal: yes on the backend, conditional on the walkthrough.** The claim state machine, scope filter, ETag and idempotency guards all behaved exactly as documented under hostile clicking and curl (404 for another claimant, 403/428/409/412/422 in the right places, the stale write recorded as an audit failure). That part is defensible line by line.
2. **Risk 1: two things the demo and the owner's spreadsheet story lean on were broken in the Docker build.** Every `claim.*` webhook delivery fails (the seeded dev-sink subscription points at a port nothing listens on inside the container), so demo step 13 shows no claim events; and "Open in Excel" threw on both sheets. The export fix is in flight; the sink is not fixed as of writing.
3. **Risk 2: the time and ownership story.** About 195 commits by a fleet of agents in roughly three hours, a search engine, two spreadsheet surfaces, a calendar, mutation testing and a Kafka adapter, against a brief that asks for ruthless prioritisation in a short sprint and says the candidate signs off every line. The journal is written in the lead agent's voice about "the owner". A panelist will ask which parts the candidate has personally read.
4. **Risk 3: the manager's picture is good but not yet "real-time" or complete.** The page never refetched (0 calls in 40 s), has no paid/settled figure, no drill-down by market, and shows three different numbers for "no reserve". Fix in flight for the refetch only.
5. Everything else a panelist will click as claimant, officer and manager worked, read clearly and updated in the other window within 4 to 15 seconds without a refresh.

## 2. Flow results (live)

| # | Flow | Persona | Result | Evidence | Fix if needed |
|---|---|---|---|---|---|
| 0 | Sign-in page | all | **Awkward.** Hero copy is the starter's ("admin console ... edit a customer"); the demo-account list has admin, staff, support, self and **no manager**; self is described as "only their own customer". The README says the page lists the seeded accounts. | `00-signin.png` | Add the manager card and claims wording (`web/src/app/features/sign-in/sign-in.html`). |
| 1 | Report an incident, Malaysia + Property, then Philippines + Motor | claimant | **Worked.** Empty submit shows per-field messages; currency follows the market (MYR, PHP); 201 and straight onto the claim record with both tracks. | `03`, `04`, `05` | None. Property has no property-specific field (declared cut). |
| 2 | Track: where is it, what happens next | claimant | **Worked.** "Handling 1 of 4", "Payout 0 of 3", "Next: claims team", a plain next-step card. Nit: the claimant nav has an item called "SLA calendar" that explains internal targets (pickup 2 days, decision 14). | `05`, `32`, `33` | Hide it from claimants or rename it (route is gated only by `CLAIMS_READ`, `app.routes.ts:93-97`). |
| 3 | Officer asks; claimant sees it without refreshing; answers | both | **Worked.** Question appeared in the untouched claimant window after 10.1 s with "Action needed" and an answer box; the answer reached the officer window in 3.6 s and the card went back to "Blocked: set a reserve". | `09`, `10`, `11`, `12`, `13` | None. |
| 4a | Approve; claimant receives it | both | **Worked.** Approved status, amount and reason on the claimant page after 9.1 s, no refresh; timeline row in plain words. | `19`, `20`, `21` | None. In-app only: nothing leaves the app (see section 5, Q6). |
| 4b | Reject a second claim; claimant receives it | both | **Worked.** "Your claim was not approved" plus the reason, after 15.2 s. Nit: the staff view says "No payment. Reserve released." on a claim that never had a reserve (`claims-logic.ts:269`). | `26`, `27`, `28` | Optional wording. No appeal or "what now" for the claimant: prepare an answer. |
| 4c | Settlement visibility | claimant | **Worked.** "Paid", 3 of 3 after the manager settles. No payment reference, date of transfer or method: settle is one move. | `24`, `25` | Declared cut; say so. |
| 5 | Pick up, assess, what blocks | officer | **Worked.** My work lands on "closest to its SLA", "Pick up next", "Waiting on claimant"; the card says Blocked/Ready with a checklist; a claim that is someone else's shows no buttons and says why. The worksheet says in its own text that it is not saved anywhere. | `06`, `07`, `08`, `17`, `37`, `38` | Prepare the worksheet-persistence answer. |
| 6 | Two windows, stale action | manager vs officer | **Worked.** Manager's reserve on a stale copy: PUT 412, banner "Changed since you opened it ... not applied", a Reload button, and an audit row `claims.reserve failure`. The typed amount is lost with the dialog. | `15`, `18`, `42` | None. |
| 7 | Team load and performance | officer | **Adequate.** Per officer: open load, oldest open (a link), decided 7/30 days, median days to decide; KPIs for unassigned past 2 days and open past 14 days; ageing bands; every number is a link into the queue. No per-officer SLA breaches, no trend, no rejection rate. | `31`, `34` | Prepare the answer; do not build. |
| 8a | Outstanding claims and exposure | manager | **Adequate, three holes.** Per market and line: outstanding reserve, approved-not-settled, unreserved count, per-currency total, no grand total. (1) The page made **zero** API calls in 40 s and "as of just now" never moved. (2) No paid/settled figure, so no reserve-versus-paid. (3) Cells are not links. | `14`, `30` | Refetch: in flight. Cell links are a small change (the queue already reads `market` and `line` from the URL, `claims-query-params.ts`). |
| 8b | "Open with no reserve" KPI | manager | **Inconsistent.** KPI said 3; its link opens `?status=UnderReview` (4 claims, one without a reserve); the matrix under it summed to 7 unreserved. Three definitions: worked claims only (`team-logic.ts:27`), a status filter, and all open claims (API). | `30`, `35` | Relabel the KPI ("Picked up, no reserve") or use the API's count; make the link honest. |
| 8c | Settle an approved claim | manager | **Worked.** Confirm dialog says it cannot be undone; 200; closed. A second settle is 409; an officer's settle is 403. | `22`, `23`, `24` | None. |
| 9 | Event side: dev sink shows `claim.*` (demo step 13) | admin | **Broken in Docker.** The sink tab showed one `customer.created.v1` and no claim event after about 14 transitions. Deliveries list: every `claim.*` row pending with "Could not connect to the destination (localhost:5080)". The API log says "Dev webhook sink subscribed to claim.* at http://localhost:5080"; the container listens on 8080 (`ASPNETCORE_HTTP_PORTS=8080`, no `ASPNETCORE_URLS`), so `LocalBase()` in `DevelopmentSeedHostedService.cs` falls back to 5080. | `41` | Set `Seed__DevSinkUrl: http://localhost:8080/api/v1/dev/webhook-sink` on the api service in `docker-compose.yml` (the seeder rewrites the URL on start). |
| 10 | Audit history of the claim | admin | **Worked.** Ten rows, field diffs, the two refused commands included. Staff get 403 on `/audit`, so the demo line "the audit diff for staff" is really "for an admin". | `42` | Fix the sentence in `docs/DEMO.md` step 13. |
| 11 | "Open in Excel", reserve sheet and assessment worksheet | officer | **Broken in Docker at 20:15.** No download; console `TypeError: i is not a constructor` in `openInExcel`; nothing shown to the user. Cause: the named import of the spreadsheet library in `xlsx-export.ts`. Reported to the lead at 20:17; a fix is in the working tree. | `36`, `38` | Verify in the BUILT container with a real click, not only a unit test. |

Side note, not scored: during the restart a "Send question" closed its dialog and no POST reached the API. Whether the user saw an error is UNVERIFIED. Worth one rehearsal: stop the API, click an action, read what the screen says.

**State of the demo data.** The database is not a fresh seed: the queue holds a claim with policy number "s" filed by the admin, the claimant owns several `BRU-...` claims from the Bruno suite, "Dev Admin" and "Dev Manager" appear as officers, and Dev Staff's median days to decide reads "0 d". The seeded question on two claims still asks the claimant to "upload photos" although `ClaimSeeder.cs` no longer contains that text: the volume predates the fix.

## 3. Scorecard against the brief's questions

| Question (paraphrased) | Where it is answered | Grade | Hardest follow-up, and can the repo back the answer |
|---|---|---|---|
| Core entities and how they relate | `docs/CLAIMS.md` section 2 (ER and state diagrams), `src/Fugue.Domain/Claims/Claim.cs` | strong | "Where are policy, coverage, parties, payments, documents?" Backed: the doc says they are references owned by other systems. Weak spot: "information when asked" is text only, so a photo or police report cannot be given. |
| One service or several, and why | `docs/CLAIMS.md` section 1 (picture, split order), plan decision A | strong | "Prove the module line is real." Backed: `[Module(ModuleCodes.Claims)]` on the controller and the runtime toggle. "What breaks first at 10x?" Reporting on live SQL: the doc already names it as the second split. |
| Synchronous versus event-driven | `docs/CLAIMS.md` section 5 table, ADR-011 and its Kafka amendment | strong on paper, **weak live** | "Show me an event leaving." Today in Docker it does not (flow 9). After the fix: sink tab, or runbook 12. "Who consumes?" Nobody in the repo: producer only. Say so. |
| Two user types, one Angular codebase | `/me` permission codes drive nav, guards and buttons (ADR-012, `features/shell/nav.ts`, `app.routes.ts`); landings per persona | strong | "A claimant types the staff URL?" Verified: `/claims/team` redirects to a "Not available" page and the API answers 403. "Bundle size for claimants?" Sheets and calendar are lazy routes; quote the number from `web/README.md` (UNVERIFIED here). |
| The FE/BE contract and who defines it | `docs/CLAIMS.md` section 1 last paragraph, ADR-012, `docs/api/openapi.v1.json`, `npm run api:gen` | strong | "What stops a breaking change?" The snapshot test. "Do events have a contract too?" Only names and the envelope; no schema registry. Admit it. |
| Async nature of claims across both tiers | `docs/CLAIMS.md` "across both tiers"; `live-poll.ts` (15 s, paused while a dialog is open) | adequate | "Polling is not async handling. What would you push?" Backed in prose (SSE fed by the outbox). "What if the claimant is not on the page?" Nothing: no email or SMS consumer. |
| What an officer needs to see | My work landing, next-step card, queue (Open tab default), Team page | strong | "How does work get reassigned when someone is off sick?" Not built: only self pick-up plus the manager acting on any claim. "Why can an officer in one market see all six?" `ClaimScope.cs` has no market dimension. |
| What the system must know about exposure | `docs/CLAIMS.md` section 6, `GET /claims/exposure`, Team page matrix | adequate | "Is that liability? Where are paid-to-date, reserve movements over time, reopened claims, IBNR?" Only current case reserve plus approved-unpaid. The `claim.reserve.set.v1` event exists (seen in the deliveries list), so the history is recoverable from events; nothing shows it. |
| Technology: REST and Kafka, what uses which | `docs/CLAIMS.md` section 5, ADR-011 amendment, `src/Fugue.Infrastructure/Messaging/KafkaEventPublisher.cs`, runbook 12, `tests/Fugue.Tests/Messaging/KafkaPublisherTests.cs` | adequate | "It is off by default, so is it real?" The adapter and a broker-backed test exist; a live run of the `kafka` profile is UNVERIFIED in this review. "Does the relay keep per-claim order under retry?" The ADR's non-guarantees say no, honestly. Know that paragraph. |

Did the earlier review's 15 changes land? Twelve verified live or in code (diagrams, polling on three claim screens, banner and next-step card, landings, merged team page with unreserved counts, Open tab, reserve event, drill-down filters, idle officers, Customers moved to "Starter example", Kafka adapter, `docs/DEMO.md`). Three did not hold: the seeded `claim.*` sink subscription exists but cannot deliver in Docker; the photo question is fixed in code but not in the running database; there is still only the `claims-window-start` tag.

## 4. Make airtight before 15:00 tomorrow

### Fix in code (small, safe, freeze-compatible), ranked

| # | Effort | What and where | Why a panelist hits it |
|---|---|---|---|
| 1 | S | Dev sink URL in Docker: `Seed__DevSinkUrl` on the api service in `docker-compose.yml` (or make `LocalBase()` read `ASPNETCORE_HTTP_PORTS`). Then move a claim and SEE `claim.*` rows in Webhooks, Dev sink. | Demo step 13 is the only live proof of the event-driven half. Today it shows nothing and the Deliveries tab shows a wall of failures. |
| 2 | S | Excel export (`web/src/app/features/claims/xlsx-export.ts`): finish the in-flight fix, rebuild the web container, click both buttons, open the file. | The owner's spreadsheet story; a dead button with no message is worse than no button. |
| 3 | S | Team and exposure, and the manager landing, refetch like the other claim screens (`team-page.ts`, `home-page.ts`, `live-poll.ts`); "as of" must move. | The brief's manager complaint is the lack of a real-time picture. The first question is "how fresh is this?" and the true answer was "as of when you opened it". |
| 4 | S | Demo on a fresh database: a throwaway compose project and volume (runbook 10 warns against `down -v` on the shared one), then rehearse on exactly that stack. | Junk claims, test officers, "0 d" medians and a question asking for photos the UI cannot take. |
| 5 | S | "Open with no reserve": one definition. Relabel the KPI or use the exposure API's count; fix its link (`team-page.ts:85-90`, `team-logic.ts:27`). | A manager adds up the matrix (7), reads the KPI (3), clicks it and gets 4 rows. |
| 6 | S | Sign-in page: add `manager@fugue.test`, describe personas in claims terms, drop "edit a customer" (`features/sign-in/sign-in.html`). | It is the first screen of the demo and it describes a different product. |
| 7 | S | Exposure cells link to `/claims/queue?market=..&line=..` (the queue already parses both). | "Which claims are behind HKD 12,000?" is the obvious next click; the doc lists drill-down as cut although the parts exist. |
| 8 | S | Claimant nav: remove or rename "SLA calendar" for callers without `CLAIMS_ALL` (`features/shell/nav.ts:53`). | "Why does a customer see your internal SLA targets?" |
| 9 | S | `git tag claims-window-end` at the last in-window commit; one README sentence on what came after. `docs/DEMO.md` step 13: "audit for an admin", not "for staff". Webhooks page text still says "every customer change". | Step 1 of the demo is the disclosure; it should be checkable with two tags. |

### Prepare an answer (do not build)

| # | Topic | The honest answer in one breath |
|---|---|---|
| A | Time and scope | "The claims core, API, tests and screens are the window. The search engine, sheets, calendar, mutation tests and tour were added after the core at my own request and are listed under 'Beyond the brief'. If I had to cut to three hours I would ship sections 1 to 6 of CLAIMS.md and none of those." Know the commit that ends the core. |
| B | "You sign off every line" | Name the files you can walk without notes: `Claim.cs` (286 lines), `ClaimFlow.cs`, `ClaimScope.cs`, `ClaimsController.cs`, `ClaimRepository.cs`, `live-poll.ts`. Say which areas you reviewed by test and gate rather than by reading (the shell, Univer glue). Do not claim more. |
| C | The journal's voice | It is written by the lead agent about "the owner". Say that up front, and point at the entries that are yours: the 17:12 code review, the 18:40 "it feels incomplete", the overrides in D-054, D-055 and D-058. |
| D | Starter provenance | The journal's first entry mentions auditing an earlier work-derived template. Expect "is any of this someone else's code?". The recorded answer is a clean-room rewrite, patterns not files. Be ready to say it plainly. |
| E | No notifications | The decision reaches the claimant only if the page is open. Events exist; the consumer is not built; which channel per market is a product decision. |
| F | No attachments, text-only answers | Cut; object storage plus virus scan plus a size and type policy; the info-request aggregate already has the slot for it. |
| G | Market scoping | Every officer sees all six markets. A market claim on the user and one more predicate in `ClaimScope` is the change; data residency would push it to a database per market. |
| H | Worksheet is not saved | By design tonight: it is a calculator whose only durable output is the reserve command. Persisting it means a `claim_assessments` table with its own version. |
| I | CI | The workflow exists and has never run on a remote (README says so). Gates ran locally. |

### The spreadsheet question: does export land, and the import answer

Export will land once it works: the reserve sheet is a credible bridge for staff who live in spreadsheets, and saving is already one guarded command per row. Today it does not land because the button is dead in the built app, the export carries no claim id or version column (`reserve-sheet-logic.ts`), and the assessment worksheet is not stored.

Suggested answer for "how would you build import or round-trip from Excel":

> "Export adds two hidden columns, the claim id and its version, which is the same ETag the API already uses. Import is an upload that the server parses and validates row by row through the same commands the screens call, so the aggregate's guards and the assigned-officer rule apply unchanged; the first response is a dry-run report, nothing written. On confirm, each row is its own transaction (claim, timeline, audit row and outbox event together, as in CLAIMS.md section 4), sent with that row's version as If-Match, so a row someone changed since the export comes back as 412 for that row only: partial success with a per-row result, exactly how the reserve sheet's save already behaves (`reserve-sheet-page.ts`, "a failure never stops the next row"). The upload itself requires an Idempotency-Key, the file hash, using the existing filter (ADR-013), and a replayed row is harmless because its version is now stale. Every row's audit entry carries the import's correlation id (ADR-005, ADR-010), so one search shows the whole batch and who ran it. I would only ever import reserves and worksheet lines, never status changes: decisions stay in the app."

## 5. The ten hardest questions, with the answer the repo supports

1. **"Why not put commands on Kafka? Our platforms use it."** The caller needs the guard's verdict in the same round trip (409 with a reason, 412 stale). Events leave through the outbox in the same transaction. Kafka would carry partner intake at volume, in front of the same command with its own authorizer. `docs/CLAIMS.md` section 5; D-056.
2. **"Kafka is off by default. Is this a slide or code?"** Code: `KafkaEventPublisher.cs` (key = claim id, awaited acks, idempotent producer), selected by `Events:Publisher`, a compose profile, runbook 12, a Testcontainers test. Off because a broker nobody consumes adds a failure mode to the demo. Admit: no consumer exists.
3. **"What happens if the process dies after commit, before publish?"** The outbox row is in the claim's transaction; the dispatcher claims rows with `FOR UPDATE SKIP LOCKED` and retries with backoff; delivery is at least once and consumers dedupe on the event id. `OutboxDispatcher.cs`, ADR-011. Per-claim order is not guaranteed across a retry: the ADR says so.
4. **"Two officers approve at once."** `If-Match` is checked before the state machine, and the save repeats `WHERE xmin = @expected`; the loser gets 412 and a banner. Verified live (flow 6). `docs/CLAIMS.md` section 4; `ClaimsConcurrencyTests.cs`.
5. **"A claimant double-clicks submit on a bad connection."** Submit requires an Idempotency-Key; a retry replays the stored 201 (`Idempotent-Replayed: true`), a different body with the same key is 422. Verified by curl. ADR-013 amendment, `ClaimsController.cs:26`.
6. **"How does the claimant receive a decision?"** In the app: status, reason, amount and a timeline row, visible within one poll (15 s) without a refresh. Outside the app: not at all yet; `claim.decided.v1` is the hook. Do not oversell this.
7. **"Is this exposure number real-time, and is it liability?"** It is live SQL over claim rows, as fresh as the last commit (after fix 3, re-read every 15 s). It is case reserve on open claims plus approved-unpaid, per currency, never summed; unreserved claims are counted as unknown, not zero. It is not paid-to-date, not reserve development, not IBNR. `ClaimReportDtos.cs`, `ClaimReports.cs`.
8. **"Why can a handler be called without a permission check?"** Authorization lives once at the HTTP edge, pinned by `Fitness_Auth_every_controller_action_declares_HasPermission_Authorize_or_AllowAnonymous` (`tests/Fugue.ArchTests/FitnessTests.cs:158`); handlers only scope data. A second transport needs its own authorizer first. This was a declined review finding, with reasons: `docs/reviews/codex/2026-11-27-claims-seam.md`.
9. **"You built a search engine and a spreadsheet in a sprint. Where is the prioritisation?"** Answer A above. Point at the cut list in `docs/plans/claims-window.md` and the "Beyond the brief" table in the README, and say what you would delete.
10. **"What did you override the AI on?"** Three concrete ones from the journal: declined two of five cross-family review findings with reasons (17:50); the web lane disproved a premise in the lead's own brief about `If-Match` (18:00); "green gates, blank product" led to the rule that a page must be loaded before a push (19:55). Add tonight's: a seeded demo aid that could never deliver in Docker, found only by driving the app.

## 6. Rehearsal checklist (fresh database, the exact stack you will demo)

**Before anything**
- [ ] Fresh volume; sign-in page lists the manager; no `BRU-` or junk claims in the queue; the claimant's open question does not ask for photos.
- [ ] Webhooks, Deliveries: no failing `claim.*` rows. Do not click "Reconnect" on the sink tab and do not run Bruno after the last restart (both replace the sink's single secret; effect on `claim.*` is UNVERIFIED, so just avoid it).
- [ ] Two windows side by side, `?tour=off&hood=off` on the first URL of each.

**Claimant (`self@`)**
- [ ] Landing shows "We need one answer from you"; answer it; the track moves.
- [ ] Report an incident in a market other than Singapore, Property; currency fills itself; land on the record.
- [ ] Leave this window alone for the rest of the officer steps.

**Officer (`staff@`)**
- [ ] My work shows the new claim under "Pick up next"; pick it up from the record.
- [ ] Card says Blocked; ask a question; watch the claimant window show it (count to 15).
- [ ] Answer as claimant; officer card returns to Blocked on its own.
- [ ] Set a reserve; try to approve above it (read the message aloud); approve within it; claimant window shows the decision and reason.
- [ ] Open a claim assigned to someone else: no buttons, and the reason.
- [ ] Reject a second claim; claimant sees the reason.
- [ ] Assessment worksheet: type three amounts, "Use as reserve", "Open in Excel", open the file.
- [ ] Team page as officer: no money section; click "Unassigned" and an ageing band.

**Manager (`manager@`)**
- [ ] Open the officer's claim, open "Set reserve"; officer changes the reserve; manager submits: 412 banner, Reload.
- [ ] Team and exposure: say what each KPI counts; click the no-reserve KPI and check the rows match the number; wait and see "as of" move.
- [ ] Settle from "To settle"; the approved-not-settled figure for that currency drops; claimant window shows Paid.

**Admin (`admin@`)**
- [ ] Audit log filtered to that claim: the diff rows and the refused 412.
- [ ] Webhooks, Dev sink: `claim.submitted.v1` through `claim.settled.v1` for the claim you just moved. If it is empty, skip the screen and show the Deliveries tab or runbook 12 instead; do not debug live.

**Say-out-loud checks**
- [ ] The 90-second answer to "what would you cut to hit three hours".
- [ ] The import answer in section 4.
- [ ] The five files you can walk without notes.

## Lead disposition (2026-11-27 20:30 +08)

Each "fix in code" item was re-checked by the lead before acting.

| # | Disposition |
|---|---|
| 1 | Fixed in 1688126: `Seed__DevSinkUrl` on the api service; the seeder rewrote the subscription to :8080; a claimant submit raised succeeded deliveries 31 -> 32 and the stuck backlog began to drain. |
| 2 | Fixed in e66e082: `workbookClass()` resolves ExcelJS from either interop shape; on the rebuilt image the reserve sheet downloads `reserve-sheet.xlsx` with zero console errors (reproduced failing first). |
| 3 | Fixed in b451cd7: Team and exposure and the landings spread `livePoll`; 6 API calls in 17 s on the manager's team page; a spec fails if a live-claims page forgets it. |
| 4 | Owner action (resetting the demo database is gated for agents): the fresh-stack commands are in `docs/hot-state.md`. |
| 5-8 | Lane nth-airtight (web), each premise verified before the change. |
| 9 | `claims-window-end` tagged at 2b9758e (the commit that closed W-020); README points at both tags; DEMO.md step 13 says "for admins" and keeps Kafka out of the demo (D-062). |
