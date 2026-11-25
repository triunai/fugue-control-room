# ADR-005: Trace, correlation and saga ids

- Status: Accepted
- Date: 2026-11-25

## Context

Debugging a request across logs, responses and (later) outbound webhooks needs identifiers at three different granularities. An external review suggested dropping a saga id; the owner kept it because "which business process is this part of" is a different question from "which request chain" and "which hop".

## Decision

Three layers, each answering one question:

| Layer | Carrier | Question | Lifetime |
|---|---|---|---|
| Trace | W3C `traceparent`, via `System.Diagnostics.Activity` | Which hop, which span? | one operation (per hop) |
| Correlation id | `X-Correlation-Id` | Which request chain? | one client request and everything it triggers |
| Saga id | `X-Saga-Id`, `ICorrelationContext.SagaId` | Which business process? | a long-running workflow, possibly many requests |

Rules:

- `CorrelationIdMiddleware` is the first middleware. An inbound `X-Correlation-Id` is accepted only if it is 1 to 64 characters of `[A-Za-z0-9-_]`; otherwise a new id is generated (no echo of untrusted input, which also blocks header and log injection). The id is echoed on the response (set at response start so it survives the exception handler).
- `X-Saga-Id` follows the same validation, is optional, and is echoed only when set. A handler can set it with `ICorrelationContext.SetSaga(id)` (scoped service defined in `Fugue.Application/Abstractions`, so Application stays free of ASP.NET Core); an invalid value throws.
- Both ids are pushed to the Serilog `LogContext` and set as `Activity` tags and baggage (`correlation.id`, `saga.id`). If hosting created no `Activity`, the middleware starts one so a trace id always exists.
- Problem responses carry `traceId` and `correlationId`; the request log line carries CorrelationId, SagaId, TraceId and UserId.
- Propagation beyond the process (S4): `CorrelationId`, `SagaId` and the trace context are stored on the outbox row and sent as headers on signed outbound webhooks (`X-Correlation-Id`, `X-Saga-Id`, `traceparent`). No message broker is in scope.

## Consequences

- One scoped service and one middleware to maintain; handlers never touch headers.
- A saga id supplied by a client is only a label: it must never be used for authorization.
- `SetSaga` after the response started cannot change the response header; set it before returning.
