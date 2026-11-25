# ADR-004: FluentResults, with our own `ErrorType` kept

- Status: Accepted
- Date: 2026-11-25

## Context

Handlers return a result object instead of throwing for expected failures. The scaffold first shipped a small hand-rolled `Result` / `Result<T>` / `Error` in `Fugue.Domain`. An external reviewer (Fable) recommended **keeping** the hand-rolled type: it is about 80 lines, has no dependency, and its failure model is exactly what the API layer needs. The owner overrode that recommendation: **FluentResults** is the ecosystem-familiar choice (widely recognised, what other .NET teams and interviewers expect to see), and the owner values that familiarity over the minimalism.

## Decision

- Use **FluentResults** (`Result`, `Result<T>`, `IError`) for all handler return values.
- Keep our own transport-agnostic `ErrorType` enum (Failure, Validation, NotFound, Conflict, Unauthorized, Forbidden, PreconditionFailed, PreconditionRequired). FluentResults has no notion of error category, and the HTTP layer needs one to choose a status.
- Add `FugueError : FluentResults.Error` carrying `Code`, `ErrorType Type` and optional per-field `Fields`; `Code` and `Type` are also written to `Metadata`. `FugueErrors` holds one factory per `ErrorType`.
- `Fugue.Domain` may depend on FluentResults and nothing else third-party; an architecture test enforces it.
- `ApiControllerBase.Problem(IError)` uses a `FugueError`'s type, code and fields; any other error is treated as `ErrorType.Failure` with code `unexpected` and its message is not exposed.
- `MapStatus(ErrorType)` stays the owner's hand-written decision.

## Consequences

- One extra dependency in the domain layer and a slightly looser contract: nothing stops a handler adding a plain `Error`. The API fallback (500 `unexpected`) and review keep that honest.
- Multiple errors can sit on one result; `ApiControllerBase` reports the first.
- The reviewer's concern (dependency weight and API surface of a third-party type) is accepted as a known trade-off.
