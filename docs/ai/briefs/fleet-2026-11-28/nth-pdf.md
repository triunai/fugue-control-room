# Lane nth-pdf (item 11b) — Opus — worktree .worktrees/nth-pdf, branch nth-pdf, base dacfb06

Objective: staff and the claimant can download a branded sample letter for a decided claim — a settlement letter for
approved/settled claims and a decision letter for rejections — with scope rules intact.

Read item 11b in `docs/plans/overnight-2026-11-28.md` (read only). Letter content: claimant, policy, incident, approved
amount in the claim currency, decision reason, officer, dates, claim number, correlation id; footer/label "Sample document,
not a legal letter". One fixed branded template (Fugue Console palette: navy, cream, gold; serif heading).

Choice (record it in the report with licence facts you verified): server-side (QuestPDF — check its licence terms for this
use: Community licence conditions; or another MIT/Apache .NET PDF library) vs client-side generation in the web app (check
bundle size impact; no new npm package without stopping first — rule in HEADER). Pick the one that keeps licences and the
bundle clean.

Scope rules: staff with claim read permission on any claim they can see; the claimant only on their own claim; anyone else
404 (same as existing claim scope: verify the existing rule and reuse it). Letters only for claims in a decided state;
otherwise 409 or the button hidden — match the existing state-machine error style.

Acceptance: tests for scope (staff 200, own claimant 200, other claimant 404, anonymous 401), state (undecided → 409/hidden),
content (the PDF contains the claim number and amount: text extraction in test); Bruno folder green against your API on 5206;
headless: download from the claim page as staff and as the claimant, file non-empty, opens (`%PDF` header), screenshot of a
rendered page (convert first page to PNG if a tool exists, else describe); 0 console errors.
Non-goals: no email sending, no storage of generated letters, no other claim page changes beyond ONE download button (the
header stepper and modals belong to nth-fixes).
Stop if: no PDF option is licence-clean without a new dependency approval — report the options and stop.
Commits: 1 template + generator + tests, 2 endpoint + scope + Bruno, 3 web button.

Recite: two letter types, scope enforced, decided-only, sample label, licence-clean, tested, downloadable by staff and claimant.
