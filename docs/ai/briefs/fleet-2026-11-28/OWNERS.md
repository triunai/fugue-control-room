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

Addendum 3 (03:54): | nth-hoodflow | Sonnet | 11e (flows for claims area) | the 9 claims-area `*.hood.ts` (home, claims queue, claim detail, my claims, report, team, reserve sheet, SLA calendar, assessment), the `withFlow` list ONLY in `web/src/app/features/hood/hood-notes.spec.ts` | 4411/-/- | — base c191a05.

Addendum 4 (03:59): | nth-formsfix | Sonnet | 5 follow-up + e2e fix | `web/src/app/features/claims/claims-queue-page.ts` (filter selects only), `web/src/app/features/claims/claims-calendar-page.ts` (day jump only), `web/e2e/**` (locators only), `web/src/app/ui/select.ts`/`date-picker.ts` only if a non-form usage needs an input/output they lack | 4412/-/- | — base 1e3d63d.

Addendum 5 (04:02): | nth-datetime | Sonnet | 5 follow-up (date+time) | `web/src/app/ui/date-picker.ts` + spec (sole owner from now; nth-formsfix must NOT edit it), the three `datetime-local` inputs in `features/access/access-page.html|ts` (overrides Until) and `features/audit/audit-page.html|ts` (From/To) | 4413/-/- | — base 8ed5cce.

Addendum 6 (04:16): | nth-calfix | Sonnet | 14:00 gate: calendar console error | `web/src/app/features/claims/claims-calendar*` (page + its css/spec), and ONLY for the fallback: the app's global ErrorHandler file + a new spec | 4414/-/- | — base db0eb20.

Addendum 7 (05:07): | nth-brunofix | Sonnet | demo hygiene | `bruno/fugue/users/04-admin-creates-user.bru`, `bruno/fugue/users/05-duplicate-email-409.bru` (role code + assertion + names only) | api :8080 read | — base 38348a3.

Addendum 8 (07:00): | nth-refactor | Opus | 12 | `src/Fugue.Application/Features/**` (moves + namespaces), `using` lines anywhere in `src/**` and `tests/**`, ArchTests namespace paths (meaning unchanged), `.editorconfig` only to enable IDE0130 for src if needed; NOTHING in web/, docs/api, migrations, SQL bodies | - | — base afc6fe9. EXCLUSIVE: no other lane touches src/ or tests/ while live.

Addendum 9 (08:04, integrator-approved, dispatcher-confirmed): nth-refactor may change path STRINGS ONLY in the 7 `web/src/app/features/**/*.hood.ts` files that quote moved source paths (ClaimFlow, ClaimScope, WebhookSecretFormat → Helpers/), as one separate commit gated on web lint + test.

Addendum 10 (11:30, owner asks at 11:26; base ddd2ded; nth-refactor is EXCLUSIVE on src/ and tests/ — these lanes are web/docs ONLY):
| nth-tour | Sonnet | owner: admin tour covers the core brief | the tour/onboarding step definitions + welcome dialog in `web/src/app/features/shell/**` (tour files only) | 4421 |
| nth-searchui | Sonnet | owner: search bar obvious + first-use explainer | `web/src/app/features/shell/global-search*` (+ its css/spec) only | 4422 |
| nth-importui | Sonnet | owner: Import page looks poor | `web/src/app/features/import/**` (web only; NO API change), sample files under `web/public/samples/import/` (copy from tests/Fugue.Tests/Import/Fixtures — read-only source) | 4423 |
| nth-changelog | Sonnet | owner: Decisions → "Changelog" | `web/src/app/features/decisions/**`, `scripts/gen-decisions*.mjs`, nav label + route (keep /decisions redirecting), web/package.json script only | 4424 |
| nth-explained | Sonnet | owner: "Fugue, Explained" page into the repo docs | new `docs/explained/**`, README one link line, `web/public/explained/**` (static copy) + ONE Help-menu link in `web/src/app/features/shell/help-button.ts` | 4425 |
| nth-overrides | Sonnet | owner: explain the override formula | the Overrides section of `web/src/app/features/access/access-page.*` (copy + an explainer only) and `access-page.hood.ts` text | 4426 |
| nth-scorefix | Sonnet | owner: The Score draws one request as many | `web/src/app/features/audit/score-*` (layout, page, specs) only; src/ READ-ONLY | 4427 |

Addendum 11 (11:32, owner asks 11:30):
| nth-k6 | Sonnet | k6 load + stampede tests with a percentile report | new `load/**` (k6 scripts, report template, results), new `docs/runbooks/load-testing.md`, README one link line; NO src/ (run the API with env overrides) | api 5431, pg 55531 |
| nth-legacydoc | Sonnet | legacy-sync adapter design (doc only) | new `docs/design/legacy-sync.md` only | - |

Addendum 12 (11:45, dispatcher aspnet-cc; base = main after nth-refactor merges; refactor exclusivity ends at its merge):
| nth-useredit | Opus | owner: edit users + show-password | `src/Fugue.Application/Features/Users/**`, `UsersController.cs`, their tests, `bruno/fugue/users/` (new files), OpenAPI/client regen, `web/src/app/features/access/**` users table + add/edit drawer only | 4432/5232/55532 |
| nth-hybridcache | Opus | owner: HybridCache + k6 "after" | package ref, Infrastructure DI + new `Caching/**`, `CacheOptions` + appsettings section, `Features/Me/**`, ONE claims read handler, invalidation calls in role/group/override/user write handlers (one line each), their tests, `load/results/**` + `load/report/compare.html` | -/5233/55533 |
Overlap: the user update handler (nth-useredit owns it; nth-hybridcache adds ONE invalidation line after useredit merges).

Addendum 13 (11:58, dispatcher): | nth-brunoci | Sonnet | FIX: CI api-contract red, sign-in 429 in import/ + letters/ | `bruno/fugue/**` sign-in pre-request scripts + collection.bru token logic ONLY (no assertions) | -/5234/55534 | — base 8e0993b.
Queued (slot after a lane finishes, or 13:00 fixes window): | nth-kafkafix | Opus | FIX: test-host crash in KafkaPublisherTests (librdkafka → 127.0.0.1:1) | `tests/Fugue.Tests/Messaging/**`, and only if root cause is there `src/Fugue.Infrastructure/**/Kafka*` | -/-/55535 | — worktree exists at 8e0993b.

Addendum 14 (12:31, dispatcher): | nth-readme2 | Sonnet | 11 README refresh + ClaimSearchHitDto comment | `README.md`, README-embedded screenshots, `src/Fugue.Application/Features/Claims/Repos/IClaimSearch.cs` (the ClaimSearchHitDto doc comment ONLY), regenerated `docs/api/**` + generated web client if the comment changes them | 4433/-/- | — base 7adb4d9; merges main late.

Addendum 15 (12:36, dispatcher; QUEUED until nth-hybridcache merges, fixes window): | nth-editfix | Sonnet | FIX: group-only user can't be edited (atLeastOneRole on edit) | `Features/Users/Commands/UpdateUser.cs` + its validator, `UserErrors.cs` if needed, `web/src/app/features/access/user-create*` (edit mode validation + hint), their tests, one new `bruno/fugue/users/*.bru` | 4434/5235/55535 |

Addendum 16 (12:45, dispatcher; dispatch on integrator's "stack recreated"): | nth-shots | Sonnet | wrap fix: 5 README screenshots re-shot on the clean stack | `web/docs/screenshots/claims-{calendar,queue,record-claimant,record-officer,sheet}.png`, README xUnit count (badge + row) only if stale | :8080 read-only |
