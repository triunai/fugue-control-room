# 08 - OpenAPI and the console contract: view Swagger, refresh the snapshot, generate a client, call /me

Goal: see the API's contract, understand the gate that keeps it honest, and hand it to the console. Needs runbooks 03 (database) and 05 (dev users). The decision is `docs/adr/ADR-012-openapi-contract-and-console-boundary.md`.

The contract is the file `docs/api/openapi.v1.json` (OpenAPI 3.0). The backend owns it: the code generates it, a test compares it with the committed file, and the console generates its TypeScript client from it.

## 1. View it in Swagger UI (Development only)

```bash
set -a; source .env; set +a
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5080 dotnet run --project src/Fugue.Api -- --seed
```
Open `http://localhost:5080/swagger`. Expect a list of tags (Customers, Webhooks, Me, ...) and an Authorize button. The raw document is at `http://localhost:5080/swagger/v1/swagger.json`. In any other environment both routes do not exist (the request is answered like any unknown path).

To try a call: `POST /api/v1/auth/token` with the dev admin (`admin@fugue.test`, password in runbook 05), copy `accessToken`, press Authorize and paste it (no `Bearer ` prefix).

## 2. What the document says

- Operation ids are `Controller_Action` (`Customers_List`, `Webhooks_Redeliver`, `Me_Get`); a generated client uses them as method names, so renaming an action renames a client method.
- Ids are `string` with `format: uuid`; enums are strings; a property is `required` when its C# type is non-nullable.
- Every error is `application/problem+json` with a stable `code`. `400` carries the `errors` map. The statuses an action can return are listed per operation; the shared ones (`400 401 403 412 428 429 503`, the module-gate `404`) are components under `#/components/responses`.
- Headers: `If-Match` is a documented request parameter wherever a version is needed; responses document `ETag`, `Location`, `Link`, `X-Next-Cursor`, `X-Correlation-Id` and `Retry-After` (components under `#/components/headers`).
- Operations marked `x-development-only` (the webhook sink) exist only when the API runs in Development.

## 3. The gate: change the API, refresh the snapshot

Any change to a route, DTO, status or header changes the generated document, and one test fails until the committed file matches:
```bash
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet test tests/Fugue.Tests --filter "FullyQualifiedName~OpenApiContractTests"
```
Expect, after a contract change, a failure whose message says `docs/api/openapi.v1.json is out of date` and names the command below. Refresh it (it runs the same test in write mode; it needs no database or Docker):
```bash
npm run api:openapi
git diff docs/api/openapi.v1.json
```
Read the diff: it IS the review of the contract change. Commit it together with the code. The output is deterministic (keys sorted, LF line ends), so a re-run on an unchanged API produces no diff.

A new action must carry a summary (`/// <summary>`), `[ProducesResponseType(typeof(Dto), 200)]` (or 201/204), `[ProblemResponse(404, "code")]` for the missing-resource and conflict cases it decides itself, and `[IfMatch]` when it reads `If-Match`. The fitness tests fail otherwise.

## 4. How the console regenerates its client

The console reads the committed file, not a running API, so a client build is reproducible from a checkout:
```bash
cd web
npx openapi-typescript ../docs/api/openapi.v1.json -o src/app/api/schema.d.ts   # or the generator the console chose
```
The generator and its output folder are the console's choice (see `web/README.md`); the only input is `docs/api/openapi.v1.json`. After a backend change: pull, regenerate, fix the compile errors. Those errors are the contract change reaching the client at build time.

## 5. Call /me

```bash
PW='Fugue-Dev-Passw0rd!'  # secrets-scan:ignore
TOKEN=$(curl -s -X POST localhost:5080/api/v1/auth/token -H 'Content-Type: application/json' \
  -d "{\"email\":\"staff@fugue.test\",\"password\":\"$PW\"}" | python3 -c 'import sys,json;print(json.load(sys.stdin)["accessToken"])')
curl -s localhost:5080/api/v1/me -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
```
Expect `roles: ["STAFF"]`, `permissions` that are all `CUSTOMERS_*`, and `modules` listing every module that is switched on. The permission list is what the API enforces right now (deny beats allow beats role; expired overrides ignored; a disabled module's permissions removed), so the console can use it for `*hasPermission` and for the navigation, and treat it as a hint only: the API still answers 403. The response is `Cache-Control: no-store`.

## 6. CORS from a browser

The console's dev server (`http://localhost:4200`) is allowed in Development. Other origins come from configuration, each an exact origin:
```bash
Cors__Origins__0=https://console.example.test dotnet run --project src/Fugue.Api
```
A wildcard, a path or a trailing slash stops the host at start with a message naming `Cors:Origins`. Check a preflight by hand (no token is needed, it is answered before authentication):
```bash
curl -si -X OPTIONS localhost:5080/api/v1/customers -H 'Origin: http://localhost:4200' \
  -H 'Access-Control-Request-Method: PUT' -H 'Access-Control-Request-Headers: authorization,if-match,content-type'
```
Expect `204`, `Access-Control-Allow-Origin: http://localhost:4200`, the allowed headers, and no `Access-Control-Allow-Credentials` (the console sends a bearer token, never a cookie). A real response lists `Access-Control-Expose-Headers: ETag, Link, X-Next-Cursor, X-Correlation-Id, X-Saga-Id, Retry-After, Location`, which is what lets the browser code read them.
