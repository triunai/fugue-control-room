# ADR-009 — Module management: configuration kill switch plus runtime registry

- Status: accepted
- Date: 2026-11-25
- Decision entry: D-023 in `docs/decisions.md`

## Context
The owner wants feature areas that can be switched off without removing code: an operator-controlled hard switch and a runtime toggle with an admin API.

## Decision
* The `modules` table is the runtime registry (`is_active`, soft delete, ETag from `xmin`). Configuration `Modules:<CODE>:Enabled` (appsettings or `Modules__CODE__Enabled`, default true, read live) is the hard kill switch the database cannot override. **Effective enabled = configuration AND `is_active` AND not deleted.**
* A feature controller carries exactly one `[Module("CODE")]`. `ModuleGateMiddleware` runs after authentication and BEFORE authorization (a disabled module also removes its permissions, so a later gate would answer 403 and confirm the feature exists): a disabled module answers `404` problem+json `module.disabled`, logs at Information and writes an `access_audit` row. Anonymous callers keep the 401. System controllers (auth, diagnostics, modules) are exempt through a shrink-only allowlist baseline.
* `GET /api/v1/modules` shows configuration, database and effective state with the ETag; `PATCH /api/v1/modules/{code}` flips `is_active` (`MODULES_MANAGE`, mandatory `If-Match`, 204 + ETag). The `SYSTEM` module (which holds module management) cannot be disabled.
* A disabled module's permissions are excluded from effective permissions (config-off modules too).

## Alternatives considered
* Feature flags only in configuration: no runtime toggle, no admin view.
* Database only: an operator cannot force a module off if the database says on.
* An action filter for the gate: runs after authorization, so a disabled module would answer 403.
* Removing disabled controllers from routing at startup: cannot toggle at runtime.

## Consequences
* One extra indexed query per request to a gated controller.
* Disabling a module leaves its data intact; permissions return when it is re-enabled.
* Fitness functions: every feature controller has exactly one `[Module]`, naming a seeded module.

## Supersedes
None.
