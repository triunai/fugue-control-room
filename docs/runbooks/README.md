# Runbooks

Step-by-step guides for someone who has never seen this repo. Every step is copy-pasteable and says what you should see.

| Runbook | Use it to |
|---|---|
| [00-for-reviewers.md](00-for-reviewers.md) | Start here if you are reviewing: run the stack, sign in, run the tests and the API suite |
| [01-local-setup.md](01-local-setup.md) | Install tools, build, test |
| [02-tracing-a-request.md](02-tracing-a-request.md) | Send a request, find it in logs, read a ProblemDetails |
| [03-local-database.md](03-local-database.md) | Start Postgres, migrate, seed, connect a GUI, Docker env vars |
| [04-customers-api.md](04-customers-api.md) | curl the Customers API (ETag, If-Match, 409/412/428), run Bruno |
| [05-auth.md](05-auth.md) | Signing key, sign in, call as each role, ownership, modules |
| [06-audit.md](06-audit.md) | Query the audit log (curl and psql), read a diff, see a failure row, try to tamper |
| [07-webhooks.md](07-webhooks.md) | Subscribe the dev sink, see a signed event, verify the signature, force a dead letter, redeliver |
| [08-openapi-and-console-contract.md](08-openapi-and-console-contract.md) | View Swagger, refresh the contract snapshot, how the console regenerates its client, call `/me`, check CORS |
| [09-console.md](09-console.md) | Install and run the Angular console, regenerate the API client, demo accounts, the 6-minute demo path, run the e2e smoke |
| [10-docker-compose.md](10-docker-compose.md) | One command: Postgres, migrations, a seeded Development API and the console in containers; ports, accounts, stop, throwaway reset |
| [11-claims.md](11-claims.md) | The claims platform: accounts, the state machine, a curl walk-through of the happy path, the 404/409/412/428 demos, workload and exposure |
| [12-kafka.md](12-kafka.md) | Run the optional Kafka broker and watch claim events on the topic |
| [agent-tooling.md](agent-tooling.md) | Demo: read-only Postgres MCP, psql, Bruno CLI, headless Playwright |

Conventions: commands run from the repo root unless stated; "Expect" lines show the output that means the step worked.
