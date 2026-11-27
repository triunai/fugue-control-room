# Fleet ownership map (base: main dacfb06)

| Lane | Model | Items | Owns (may change) | Ports web/api/pg |
|---|---|---|---|---|
| nth-tooling | Sonnet | 17a | `docker-compose.yml`, `scripts/db/**`, `.env.example`, `.mcp.json`, `docs/runbooks/00-for-reviewers.md`, `docs/runbooks/03-local-database.md`, new `docs/runbooks/agent-tooling.md`, README "tooling" section only | -/-/55501 |
| nth-forms | Opus | 5 | `web/src/app/ui/select*`, `web/src/app/ui/date-picker*` (new), `web/src/app/ui/examples*` (new), form templates/components in features: claims report + dialogs (request info, set reserve, decide/reject, claimant answer), webhooks subscription form, customers form, access add-user drawer | 4402/-/- |
| nth-import | Opus | 11a | new `src/**/Features/Import/**`, new `web/src/app/features/import/**`, new SQL under `Sql/import/`, new Bruno `bruno/fugue/import/`, new tests; one nav entry + one route line; config section in appsettings for domain→market | 4403/5203/55503 |
| nth-searchx | Opus | 10 | `src/**` claims-search indexer/settings/query code (Meilisearch), its tests, header search box + results in `web/src/app/features/shell/**search*` | 4404/5204/55504 |
| nth-fixes | Sonnet | 1b, 3a, 7a, 11f, claimant wording | `web/src/app/features/modules/**`, the claim header stepper + "How a claim moves" modal in `web/src/app/features/claims/**` (only those components), the role×permission matrix in `web/src/app/features/access/**` (matrix cells only), home page buttons only if 1b fails | 4405/-/- |
| nth-pdf | Opus | 11b | new `src/**/Features/Letters/**` (or the claims feature's letter endpoint), PDF template, its tests, Bruno `bruno/fugue/letters/`, ONE download button on the claim page header actions | 4406/5206/55506 |
| nth-hooda | Opus | 11e (A) | `web/src/app/features/hood/**` (model + sidebar, incl. the floating request tooltip bug), `*.hood.ts` for audit, audit/trace (The Score), access, modules, webhooks, customers, profile (`/me`); their `data.hood` lines in `app.routes.ts` | 4407/-/- |
| nth-hoodb | Sonnet | 11e (B) | `*.hood.ts` for home, claims queue, claim detail, my claims, report, team, reserve sheet, SLA calendar, assessment; their `data.hood` lines in `app.routes.ts`. Must NOT edit `features/hood/**` | 4408/-/- |
| nth-decisions | Sonnet | 11d | new `web/src/app/features/decisions/**`, a generator script under `scripts/` that reads `docs/adr/*.md`, `docs/decisions.md`, `CHANGELOG.md` and writes a JSON into the web app, one route + one nav entry | 4409/-/- |

Shared files several lanes touch by one or two lines: `web/src/app/app.routes.ts`, the shell nav config. Keep your change there
minimal and contiguous; the integrator resolves adjacent-line conflicts. Nobody else edits `features/hood/**` except nth-hooda.
Nobody touches the doc spine (see HEADER rule 3).

Addendum 1 (03:33): every lane may also change `docs/ai/lanes/<lane>-status.md` (its own, new) and the Status cell of its
own row in `docs/branch-ledger.md`. Nothing else in docs/ outside its list above.

Addendum 2 (03:38): | nth-paid | Opus | 11c (#009 paid figure) | exposure query/handler/DTO/SQL + tests, `bruno/fugue/claims/` exposure assertions, `web/src/app/features/claims/team-page.*` except `team-page.hood.ts`, OpenAPI/client regen | 4410/5210/55510 | — base f3d7637.
