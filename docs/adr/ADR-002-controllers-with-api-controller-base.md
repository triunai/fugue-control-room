# ADR-002: MVC controllers with an abstract `ApiControllerBase`

- Status: Accepted
- Date: 2026-11-11

## Context

The project exists to polish .NET API/CRUD skills. The HTTP surface needs one consistent way to map results to responses, concurrency, pagination and idempotency, without each endpoint re-deriving it.

## Decision

- ASP.NET Core **MVC controllers**. Not FastEndpoints, not minimal APIs.
- Every controller inherits an abstract `ApiControllerBase` that owns the plumbing: `Result<T>` to `ProblemDetails`, `CreatedAtRoute`, ETag / `If-Match` concurrency, pagination headers, idempotency-key handling, `CurrentUser` / `Tenant` access, and dispatch through a mediator.
- Controller actions are one-liners that dispatch a command or query. Business logic lives in handlers.
- Mediator: the source-generated **`Mediator`** package (martinothamar, MIT), not MediatR (commercial license since v13).

## Consequences

- Uniform responses and error shapes; actions stay trivially reviewable.
- Handlers are testable without HTTP; architecture tests can forbid logic and data access in controllers.
- Source generation avoids runtime reflection cost and licensing risk.
- `ApiControllerBase` becomes a critical seam: changes to it need review.
