# 09 - Fugue Console: install, run against the API, and the six-minute demo

Goal: run the Angular admin console (`web/`) against a seeded API, sign in as each dev user, and walk the demo path: a 412 on a customer, its audit diff by correlation id, a module switched off, and a signed webhook arriving at the dev sink. Needs runbooks 03 (database), 05 (signing key, dev users) and 08 (the OpenAPI contract). Architecture: `web/README.md`.

## 1. Install (once)

Node 24 LTS or newer (`node -v`).
```bash
cd web
npm ci
npx playwright install chromium   # only for the e2e run in section 6
```

## 2. Start the API on 5081

The dev server proxies `/api` to `http://localhost:5081` (`web/proxy.conf.json`), so the browser talks to one origin and no CORS is involved. Start the API in Development with the seed and local webhook destinations allowed (the sink in section 5 lives on the API itself):
```bash
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1
set -a; source .env; set +a
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5081 \
  Webhooks__AllowInsecureLocal=true Webhooks__MaxAttempts=3 Webhooks__BaseDelaySeconds=2 \
  dotnet run --project src/Fugue.Api -- --seed
```
Expect `Now listening on: http://localhost:5081`. Another port works too: change `target` in `web/proxy.conf.json` and `apiOrigin` in `web/src/environments/environment.development.ts` (the address the API posts sink events to).

## 3. Run the console

```bash
cd web
npm start            # ng serve on http://localhost:4200
```
Open `http://localhost:4200`. The sign-in page lists the seeded accounts (Development build only; a production build ships none). Choose one to sign in.

| Account | Sees |
|---|---|
| `admin@fugue.test` | everything: Customers, Audit log, Users and access, Modules, Webhooks (with the Dev sink tab) |
| `staff@fugue.test` | Customers (all rows), My profile |
| `support@fugue.test` | the same as staff, through the `SUPPORT` group |
| `self@fugue.test` | Customers (only their own row, email locked), My profile |

The navigation is built from the permission CODES in `GET /api/v1/me`, never role names. `/me` is refetched after sign-in, after every token refresh and when the window regains focus, so a switched-off module disappears from the menu on the next of those.

## 4. Regenerate the API client after a contract change

```bash
cd web
npm run api:gen
```
Expect orval to write `src/app/api/generated/` from `docs/api/openapi.v1.json` and the post-step to print `marked 1 password type declaration(s)` (the secrets scan reads the generated `password: string;` as a value otherwise). Then `npx ng build` must stay clean. The console imports the generated MODELS through `src/app/api/models.ts`; its own wrappers in `src/app/api/*.api.ts` own paging headers, ETags and query keys.

## 5. The six-minute demo path

Sign in as `admin@fugue.test`.

1. **Customers, 412 (2 min).** Open `Sample Customer 07`. The drawer header shows `version N`: the ETag the form will save against. In a terminal, change the same customer with the current ETag (runbook 04, section 3), or open it in a second browser and save there. Back in the first drawer, change the name and press **Save changes**. Expect the amber banner **Changed since you opened it**, `version N -> M`, the fields that changed (`Name is now ...`) and a `ref` chip. **Reload their version** fills the form with the server's values (and their ETag); **Overwrite with mine** fetches the current ETag and resubmits yours. Save, and a toast says **Changes saved** with its own `ref`.
2. **Audit diff by correlation id (1 min).** Click the toast's `ref` chip (it copies), open **Audit log**, paste it into **Correlation id**, **Apply filters**. The detail panel shows `name` old -> new; masked values (`a•••@example.test`) stay masked. Also find the refused 412 save as a `failure` row with `customer.version_mismatch`. **View all for this correlation id** and **History of this customer** re-filter in one click.
3. **Module toggle (1 min).** **Modules**: each card shows configuration AND switch = effective. Switch **Customers** off (confirm). The Customers item leaves the navigation at once; any open Customers page moves to a calm "This area was just switched off" page. Switch it back on. `SYSTEM` is locked.
4. **Webhook dev sink (2 min).** **Webhooks -> Dev sink**. **Connect the sink** creates a `customer.*` subscription and hands its secret to the sink (no secret is ever shown here; on the Subscriptions tab, **New subscription** and **Rotate secret** show a secret exactly once). **Create a test customer**: within a few seconds `customer.created.v1` arrives with **Signature verified**, its `correlation` chip equal to the `ref` in the toast you just saw, and the customer id as `saga`. **Deliveries** shows it `Delivered`, 1 attempt, `200`.

Optional: a dead letter. Create a subscription to `http://127.0.0.1:9/hook` (allowed in Development only), change a customer, wait about 15 s; the delivery is `Dead letter` after 3 attempts; **Redeliver** queues it again.

## 6. Checks

```bash
cd web
npm test               # Vitest: ETag/If-Match, problem mapping, single-flight refresh, hasPermission (15 tests)
npm run lint           # angular-eslint
npx ng build           # production build; initial bundle about 395 kB raw / 105 kB over the wire
npx playwright test    # needs sections 2 and 3: the 412 smoke, then screenshots into web/docs/screenshots/
```
The e2e signs in several times a minute and the API allows 5 sign-ins per IP and email per minute (runbook 05). For e2e runs only, start the API with `RateLimiting__Auth__PerEmailPermits=60` as well.

## If it does not work

| Symptom | Cause | Fix |
|---|---|---|
| Sign-in says "The API cannot be reached" | The API is not on 5081 | Section 2, or change `web/proxy.conf.json` |
| "Too many attempts. Try again in N seconds." | The login limiter (runbook 05) | Wait, or raise the limit for local e2e only |
| Dev sink stays "Waiting for the first event" | Sink not connected, or the API lacks `Webhooks__AllowInsecureLocal=true` | Connect again; restart the API with the flag |
| Delivery pending with `401` | The sink trusts an older secret (a rotate elsewhere) | **Reconnect** on the Dev sink tab |
| Every page is "Not available" | `/me` failed or the module is off | Check the API log; **Modules** as admin |
| Signed out after a reload | The refresh token is tab-scoped (sessionStorage) and was revoked or expired | Sign in again; see `web/README.md`, token strategy |
