# Runbooks

Step-by-step guides for someone who has never seen this repo. Every step is copy-pasteable and says what you should see.

| Runbook | Use it to |
|---|---|
| [01-local-setup.md](01-local-setup.md) | Install tools, build, test |
| [02-tracing-a-request.md](02-tracing-a-request.md) | Send a request, find it in logs, read a ProblemDetails |
| [03-local-database.md](03-local-database.md) | Start Postgres, migrate, seed, connect a GUI, Docker env vars |
| [04-customers-api.md](04-customers-api.md) | curl the Customers API (ETag, If-Match, 409/412/428), run Bruno |

Conventions: commands run from the repo root unless stated; "Expect" lines show the output that means the step worked.
