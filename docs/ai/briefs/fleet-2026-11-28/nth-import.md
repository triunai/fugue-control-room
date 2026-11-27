# Lane nth-import (item 11a) — Opus — worktree .worktrees/nth-import, branch nth-import, base dacfb06

Objective: staff can turn spreadsheets, exported emails and WhatsApp chat exports into DRAFT claims that a person reviews
before anything is created — honest file imports standing in for live connectors.

Context: the take-home brief says claimants submit by phone or email and staff work from shared inboxes and spreadsheets.
Read the full item 11a text in `docs/plans/overnight-2026-11-28.md` (read only) and follow it.

Desired state (summary of 11a): staff-only page "Import" with three sources —
(a) .xlsx/.csv upload, column mapping, per-row validation preview using the SAME rules as Report an incident (reuse the
    submit validator), import the valid rows;
(b) .eml/.mbox upload, route by sender domain to market via a domain→market table in config bound with IOptions, parse
    subject/body into a draft;
(c) WhatsApp chat export .txt, parse into a draft with the transcript attached as a note.
Drafts are reviewed by a person and only become claims through the normal submit path (never auto-submitted). Page and a
proposed D-row say plainly these are FILE IMPORTS standing in for Graph/Gmail/WhatsApp Business connectors (no external
calls). Audited; idempotent (same file twice does not duplicate — e.g. a content hash per file/row); tests per parser.

Design choice is yours in Phase 1, recorded in the report: drafts held client-side until the reviewer submits through the
existing submit endpoint (no migration — preferred if it meets the brief), or a staging table (migration allowed, rule 4).
Idempotency must be server-side either way. Permission: reuse an existing staff permission or add one only with a migration.

Acceptance: parser unit tests (xlsx, csv, eml, mbox, whatsapp; malformed inputs); integration test: same file twice →
no duplicate; claimant/self-service gets 403 on import endpoints; Bruno folder `bruno/fugue/import/` green against your API
on 5203; headless: staff uploads a sample of each kind (commit small fixtures under tests or web/check fixtures), reviews,
creates one claim per source; 0 console errors; screenshots.

Library: parsing xlsx on the server needs a package — prefer what is already referenced (check Directory.Packages.props /
web exceljs). If a NEW NuGet package is unavoidable, it must be MIT/Apache; note its licence in the report. No new npm package.
Non-goals: no live connectors, no email sending, no changes to the existing submit rules, no shared UI controls (nth-forms
owns fg-select/fg-date; use native controls if theirs is not merged — the integrator will swap later).
Stop if: the submit validator cannot be reused without changing its behaviour.
Commits: 1 parsers + tests, 2 API (endpoints, idempotency, audit), 3 web page, 4 Bruno + fixtures.

Recite: three file sources → reviewed drafts → normal submit; honest labels; idempotent; audited; staff-only; tested.
