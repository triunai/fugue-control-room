# 02 - Tracing a request

Goal: send a request with and without correlation headers, find it in the logs, and read a ProblemDetails. Prerequisite: [01-local-setup.md](01-local-setup.md) done (build passes). Background: `docs/adr/ADR-005-trace-correlation-saga-ids.md`.

## 1. Start the API in Development mode

Terminal A:
```bash
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5080 dotnet run --project src/Fugue.Api
```
Expect: `Now listening on: http://localhost:5080` and `Application started.` Leave it running. Development logs are readable lines; any other environment logs compact JSON, one event per line.

## 2. Request without any correlation header

Terminal B:
```bash
curl -si 'http://localhost:5080/api/v1/diagnostics/ping?count=3'
```
Expect: `HTTP/1.1 200 OK`, a generated `X-Correlation-Id: <32 hex characters>` header, no `X-Saga-Id`, and body `{"message":"pong","count":3}`.

## 3. Request with your own ids

```bash
curl -si -H 'X-Correlation-Id: demo-123' -H 'X-Saga-Id: order-42' 'http://localhost:5080/api/v1/diagnostics/ping?count=3'
```
Expect: both headers echoed back (`X-Correlation-Id: demo-123`, `X-Saga-Id: order-42`).

An id with other characters or over 64 characters is ignored (a new correlation id is generated; a bad saga id is dropped):
```bash
curl -si -H 'X-Correlation-Id: bad id;<x>' 'http://localhost:5080/api/v1/diagnostics/ping?count=1' | grep -i x-correlation-id
```
Expect: a fresh 32-character hex id, not `bad id;<x>`.

## 4. Find the request in the logs

In Terminal A, search for your id:
```
[20:38:30 INF] demo-123 order-42 Handled PingQuery in 3.8 ms: success
[20:38:30 INF] demo-123 order-42 HTTP GET /api/v1/diagnostics/ping responded 200 in 44.5 ms
```
Expect two lines carrying `demo-123 order-42`: the logging behavior (message name, elapsed ms, outcome; never the payload) and the request log. In non-Development environments the same data is JSON properties `CorrelationId`, `SagaId`, `TraceId`, `UserId` (when authenticated). To see JSON locally, restart with `ASPNETCORE_ENVIRONMENT=Production`, then filter with `grep demo-123`.

## 5. Read a ProblemDetails

A binding error (framework-generated 400):
```bash
curl -si 'http://localhost:5080/api/v1/diagnostics/ping?count=abc'
```
Expect: `400`, and a body with `errors.count`, `traceId` and `correlationId`. The `correlationId` equals the response `X-Correlation-Id` header.

A validation failure (`count` must be 0 to 100):
```bash
curl -si 'http://localhost:5080/api/v1/diagnostics/ping?count=500'
```
Expect (**until the owner implements `ApiControllerBase.MapStatus`**): `500` with `Content-Type: application/problem+json`, `type: https://fugue.dev/errors/unexpected`, `code: unexpected`, plus `traceId` and `correlationId`, because `MapStatus` throws `NotImplementedException` and the global exception handler reports it. In Development the body also has `detail` and `exception`; in Production it has neither. The log shows `Handled PingQuery ...: failure validation.failed` at WRN.
After `MapStatus` is implemented, expect `400` with `errors.count` instead.

How to read it: `type` and `code` say what happened; `traceId` is the per-hop trace (find it in logs as `TraceId`); `correlationId` is the request chain. Send the `correlationId` when reporting a bug.

## 6. Stop

Press Ctrl+C in Terminal A. Expect the process to exit with no error.
