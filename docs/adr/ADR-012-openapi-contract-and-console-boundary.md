# ADR-012 — OpenAPI contract, console CORS and GET /me

- Status: accepted
- Date: 2026-11-26
- Decision entry: D-042, D-043, D-044 in `docs/decisions.md`

## Context
The Fugue Console (Angular, W-017) is a second consumer of the API. It must generate its client from a machine-readable contract, call the API from a browser origin, and learn what the signed-in user may see without guessing from 403s. Left informal, each of those drifts: a renamed field breaks the console silently, a CORS typo fails only in a browser, and a nav built from role names duplicates the server's rules.

## Decision
* **The backend owns the contract.** Swashbuckle (6.9, net8) generates one OpenAPI 3.0 document, `v1`, from the controllers, the XML doc comments and the attributes. Operation ids are `Controller_Action`. Typed ids are `string` with `format: uuid`, enums are strings, a property is `required` when its CLR type is non-nullable. Every error is `application/problem+json` (shared component responses `BadRequest`, `Unauthorized`, `Forbidden`, `NotFound`, `PreconditionFailed`, `PreconditionRequired`, `TooManyRequests`, `ServiceUnavailable`); an action lists only what it decides itself (`[ProblemResponse(404, "customer.not_found")]`, 409) and `[IfMatch]`; the rest (400, 401, 403, the module-gate 404, 412, 428, 429, 503, the response headers ETag, Location, Link, X-Next-Cursor, X-Correlation-Id, Retry-After) follows from the pipeline attributes in `FugueOperationFilter`.
* **The snapshot is a gate.** `docs/api/openapi.v1.json` is the document rendered deterministically (keys sorted, LF). A test builds the document in memory and fails when it differs from the committed file; `npm run api:openapi` refreshes it, and the change shows up as a reviewable diff next to the code that caused it. The console regenerates its TypeScript client from this file, so a breaking change is visible in review and not at runtime.
* **Swagger UI and the live JSON exist only in Development** (`/swagger`, `/swagger/v1/swagger.json`); other environments do not map them. The snapshot includes the Development-only webhook sink, marked `x-development-only`, because the console's dev screen needs it.
* **CORS.** One named policy, `Console`: the origins in `Cors:Origins` (each an exact origin, validated at start; a wildcard stops the host) plus `http://localhost:4200` in Development only. No credentials (the console sends a bearer token in a header, never a cookie). Methods GET, POST, PUT, PATCH, DELETE; request headers `Authorization`, `Content-Type`, `If-Match`, `Idempotency-Key`, `X-Correlation-Id`, `X-Saga-Id`; exposed response headers `ETag`, `Link`, `X-Next-Cursor`, `X-Correlation-Id`, `X-Saga-Id`, `Retry-After`, `Location`. The middleware runs before authentication so a token-less preflight is answered, and its headers survive the exception handler.
* **`GET /api/v1/me`.** Any authenticated user, no permission, module SYSTEM (which cannot be disabled). It returns the caller's own id, email, display name, tenant, active roles (direct and through active groups), active groups, effective permission codes (the same `IUserAccessReader` query the authorizer uses: deny beats allow beats role, expired overrides ignored, disabled modules removed) and the effectively enabled modules (code and name). Never cached (`Cache-Control: no-store`). It reads nothing from a header or a body: the subject is the verified token's user.
* **Fitness.** Every `[HasPermission]` literal must exist in the permission catalog the migration seeds (read from the EF model's seed data), the constants and the seed are the same set, every action declares its 2xx, every declared error is problem+json, and the set of "signed-in only" actions is closed (`MeController.Get`).

## Alternatives considered
* **Hand-written client types in the console**: no gate; drift shows up as runtime errors.
* **Generate the document at build time into the repo (MSBuild target / CLI tool)**: needs a second host entry point and a tool restore; the in-process test builds the same document with the same wiring and also gates it.
* **Allow any origin in Development**: hides a CORS mistake until the first real deployment; the console has one fixed dev origin.
* **Allow credentials**: nothing in the design is cookie-based, and credentialed CORS widens the blast radius of any origin mistake.
* **Return roles only from `/me` and let the console map roles to screens**: duplicates the server's rules and breaks on overrides, groups and disabled modules.

## Consequences
The snapshot test fails on any contract change until the file is refreshed and committed: that is the point. A new endpoint needs summaries and `[ProducesResponseType]` or the fitness tests fail. A new feature module needs no change here. The document is a contract of shape, not of behaviour: examples and the runbook carry behaviour.

## Supersedes
none.
