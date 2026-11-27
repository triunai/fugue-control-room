# Lane nth-api2 status (review follow-ups: visible events, honest exposure, drill-down)

Refs W-024. Base a9e1610. Branch `nth-api2`.

## Commits
- `feat(domain): a reserve change is an event` (`claim.reserve.set.v1`)
- `feat(application): exposure counts unreserved claims; workload shows idle officers`
- `feat(api): queue drill-down filters` (also the OpenAPI snapshot and the regenerated console client)
- `feat(infra): demo seed subscribes the dev sink to claim events`
- `test(api): Bruno requests for the drill-down filters and the new report fields`

## Validation (copied from output)
- `dotnet build`: 0 Warning(s), 0 Error(s)
- `dotnet test`: ArchTests 37 passed; Tests 661 passed, 0 failed (base: 646)
- `dotnet format --verify-no-changes`: clean
- `npm run api:openapi` and `cd web && npm run api:gen`: re-run after the last commit, no diff
- `cd web && npm test`: 62 passed (11 files); `npm run build`: complete
- Bruno `claims` against a live API (Postgres :5456, API :5106, seeded): 38 requests passed, 42/42 tests
- Live sink check: after the Bruno walk the dev sink listed all seven claim event types, including `claim.reserve.set.v1`

## Choices and deviations
- Reserve event payload: thin (id, number, status, market), NO amount. ADR-011 and `ClaimEvents.cs` keep amounts out of events; consumers read the claim over REST.
- Contract names for nth-claimui, as asked: `unreservedOpenCount` (rows and totals), `oldestOpenClaimId` (nullable, per officer), query `officerId`, `minAgeDays`, `maxAgeDays` on `GET /claims` and `GET /claims/counts`.
- Age filters: whole elapsed days since submission, UTC instants, inclusive. `minAgeDays=3` = submitted at least 72h ago; `maxAgeDays=7` = younger than 8 x 24h. 400 when negative, over 3650, max below min, or `officerId` is the empty GUID; a malformed GUID or number is a binding 400.
- `officerId` is staff only: for a claimant it matches nothing (200, empty), so it cannot be used to learn who handles a claim.
- Workload "officers who can handle claims": the SQL computes effective `CLAIMS_HANDLE` from direct roles, group roles and allow/deny overrides, active users only. It does NOT apply module switches or tenant checks (the full resolver in `user-access.sql` does). Anyone still assigned a claim is listed regardless. Idle officers in the seeded dev DB: staff and support (and any other holder, e.g. admin, if the role grants it).
- Seed: the sink subscription is created through the store, not the create command, so the SSRF guard does not run for the local URL. The sink secret lives in memory and is lost on restart, so the seeder reads the stored secret back and hands it to the sink on every start. Delivery to the http sink still needs `Webhooks__AllowInsecureLocal=true` (documented in runbooks 07 and 11); the URL comes from `Seed:DevSinkUrl`, else the first listen URL, else `http://localhost:5080`.
- Bruno 15 and 17 no longer ask for or mention photos. No migration added. `docs/CLAIMS.md` still says events carry no amounts and lists claim events without `claim.reserve.set.v1` (not in this lane's allowed files): the lead may add it.
- Process note: the first commit of the reports slice was refused by the format hook because the shell lost `PATH`; re-committed with the env set. Slice 2's test file was committed without the filter tests (they land in slice 3 with the code they test).

## Could not verify
- The webhooks screen's sink tab in a browser (only the sink API was checked). Open the console against the seeded API with `Webhooks__AllowInsecureLocal=true` and act on a claim.
- Merge with nth-quality's new `Claim` tests: `SetReserve` now raises an event, so a test that counts events per move will need `+1` for the reserve move.
