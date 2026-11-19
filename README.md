# Task Management API

A .NET 10 REST API for creating, listing, updating, and deleting tasks, backed by PostgreSQL.

## Run locally

With Docker running and Docker Compose installed, start the stack from the repository root:

```sh
docker compose up --build
```

The first run pulls public images and restores locked NuGet dependencies inside the build container. No host .NET SDK, `.env` file, or manual database setup is needed. Compose generates a database password on first startup, waits for PostgreSQL, and runs the application with `--migrate` as its only argument. The API starts at `http://localhost:8080` after that job succeeds.

A fresh database starts with three demo tasks: **Draft the release notes** (ID 1, Alex, `pending`), **Review the onboarding guide** (ID 2, Morgan, `inProgress`), and **Agree on release criteria** (ID 3, Alex, `done`). Use `docker compose down` to stop the stack while keeping the database and its matching password for the next run.

## Try the API

Open the [interactive Scalar reference](http://localhost:8080/scalar/v1) to explore and try the task endpoints, or download the [OpenAPI JSON](http://localhost:8080/openapi/v1.json). Both are served by the same local API from the normal Compose startup, with no extra port or configuration. Requests from the reference use the same origin (`http://localhost:8080`).

Create a task, then list matching tasks with a page size of one:

```sh
curl -i -H 'Content-Type: application/json' \
  -d '{"title":"Write a smoke test","description":"Check the local task API.","status":"pending","assignee":"Alex","dueDate":"2030-02-01"}' \
  http://localhost:8080/api/tasks

curl 'http://localhost:8080/api/tasks?status=pending&assignee=Alex&pageSize=1'
```

| Method | Route | Success | Input and lookup errors |
| --- | --- | --- | --- |
| `POST` | `/api/tasks` | `201 Created` with the task and a `Location` header | `400` |
| `GET` | `/api/tasks` | `200 OK` with a page of tasks | `400` |
| `GET` | `/api/tasks/{id}` | `200 OK` with the task | `400`, `404` |
| `PUT` | `/api/tasks/{id}` | `200 OK` with the replacement | `400`, `404` |
| `DELETE` | `/api/tasks/{id}` | `204 No Content` | `400`, `404` |

`POST` and `PUT` require all five JSON fields; `PUT` replaces the whole task. `title`, `description`, and `assignee` must not be blank or contain NUL characters. Their maximum lengths are 120, 2,000, and 100 characters respectively, checked before trimming surrounding whitespace for storage. `status` must be the exact string `pending`, `inProgress`, or `done`. `dueDate` is a calendar date in `YYYY-MM-DD` format with no time or time zone; past dates are allowed. Invalid input returns `400`. Item IDs must be positive 32-bit integers; a valid ID that does not exist returns `404`.

### Filters and pagination

The optional `status` and `assignee` filters are combined with AND. Both match exactly and are case-sensitive; the assignee filter is not trimmed. `pageSize` defaults to 20 and accepts integers from 1 to 100. Supply each recognized query parameter at most once. Repeated values, NULs in filter or pagination values, an invalid status, or an empty or invalid numeric value return `400`.

The response contains `items` sorted by ascending `id` and `nextAfterId`. When `nextAfterId` is not `null`, send it as `afterId` with the same filters to fetch tasks with higher IDs. Omit `afterId` for the first page; if supplied, it must be a positive 32-bit integer. A `null` cursor means there are no more matching tasks, including when `items` is empty.

### HTTP errors and rate limiting

Application errors use JSON Problem Details (`application/problem+json`) when the request accepts JSON. Validation problems include field-level errors in `errors`. Unmatched paths return `404`; unexpected exceptions return a generic `500` without exception messages or stack traces. Errors rejected before the application pipeline, such as disallowed `Host` headers, need not have a Problem Details body.

Matched task routes share a fixed window of **120 requests per minute per API instance**. Excess requests return `429 Too Many Requests` immediately, without queuing. When the limiter supplies a retry time, `Retry-After` gives the wait in whole seconds. For local development, set the positive `RateLimiting:RequestsPerMinute` setting in application configuration (or `RateLimiting__RequestsPerMinute` in the API process environment) before startup. The limiter is shared across callers, not keyed by IP or forwarded headers or coordinated across replicas.

## Test locally

With the .NET 10 SDK and a running local Docker engine, run from the repository root:

```sh
dotnet restore TaskManagement.slnx --locked-mode
dotnet test TaskManagement.slnx --no-restore
```

The xUnit integration tests host the API in-process and provision, migrate, and dispose of temporary PostgreSQL containers using Testcontainers. They cover CRUD, validation, filters, pagination, Problem Details, rate limiting, and the OpenAPI document and Scalar page. They do not require the Compose stack or an already-running API.

## Design decisions

- **Controller → service → EF Core.** The controller handles HTTP, validates request DTOs, and maps expected service outcomes such as `Result<T>.NotFound` to HTTP responses. DI gives the `ITaskService` implementation and EF Core context a request scope. The service uses EF Core directly rather than adding a generic repository. Database I/O is asynchronous and receives the request cancellation token; validation and in-memory mapping stay synchronous.
- **PostgreSQL and Compose.** PostgreSQL provides relational constraints and indexes, and the integration tests exercise the same database engine as the application. Compose gives a reproducible local stack without a host SDK or database, and the separate migration job applies the schema and seed before the API serves requests.
- **Keyset pagination.** The list query filters on `id > afterId`, orders by `id`, and fetches one extra row to find out whether another page exists. It avoids offset scans and a total count, uses no tracking, and projects only the response fields. The primary key and composite `(status, id)` and `(assignee, id)` indexes support the unfiltered and filtered access paths.

## Local-only security and recovery

Compose binds the API to host loopback (`127.0.0.1:8080`) and publishes no PostgreSQL port. The loopback mapping does not restrict the listener inside the container or apply outside this Compose setup. The API serves plain HTTP and has no authentication or authorization; the default `Host` allowlist (`localhost` and `127.0.0.1`, with or without a port) is not access control. The rate limiter is a local safeguard, not a production traffic policy.

The generated database password is not committed, built into the image, or passed as a Compose environment value. PostgreSQL, the migration job, and the API read it from a dedicated volume mounted read-only; only the initializer has a writable mount. The volume is unencrypted and readable by Docker administrators. The API and migration job share the bootstrap database role in this demo. Keep private material outside the build context.

- **PostgreSQL stopped after startup:** Run `docker compose start db`. The API can still listen while PostgreSQL is down, but database-backed requests cannot succeed until it is ready again.
- **Migration cannot authenticate after a restore:** Restore `db-data` and its matching `db-secret` together. With existing data and a new secret, the readiness probe can pass even though migration authentication fails. Restore the matching secret or have a database administrator reset the role password to match the replacement credential, then rerun the normal startup command without deleting the data volume.
- **Renaming database settings:** Keep `POSTGRES_DB`, `POSTGRES_USER`, and `POSTGRES_PASSWORD_FILE` in `compose.yaml` in sync with `src/TaskManagement.Api/Data/DatabaseConnection.cs`. Update the health check for database or user changes, the initializer and mounts for password path changes, and the application host if the `db` service is renamed. Bootstrap setting changes do not reconfigure an existing database.

## With more time

- **Secure access:** Add authentication and task-level authorization. Separate the least-privilege runtime role from migration credentials, manage and rotate secrets with per-service access, require verified TLS, and pin reviewed image digests.
- **Prove recovery:** Define recovery targets, automate encrypted backups, and rehearse restoring data into a fresh environment with matching credentials.
- **Measure before tuning:** Inspect query plans and load-test representative filter combinations and data sizes before adding indexes. Track latency, errors, rate-limit rejections, and connection pool usage with metrics and traces.

## AI assistance

I used an AI coding assistant for planning, implementation, and review. I set the constraints and decided what stayed; the assistant proposed, wrote code, and checked it.

**What I asked for**

- An opinion on the brief, then a plan split into short tasks, each ending in its own commit.
- My requirements: .NET 10, PostgreSQL with seed data, DTOs, controller → service → `Result<T>`, middleware and rate limiting, async I/O, English code, and a stack that starts with one command and keeps its secret out of source and image layers.
- One task at a time: scaffold, schema and seed, Compose, CRUD, filters and pagination, errors and rate limiting, README, and finally OpenAPI with Scalar. Each task came with tests and a review.

**What I accepted**

- Validation in three layers: DTO annotations for format, the service for rules, and PostgreSQL constraints for integrity.
- Keyset pagination: filter on `id > afterId`, fetch one extra row instead of counting, and index `(status, id)` and `(assignee, id)`. A SQL trace confirmed one parameterized query with no `COUNT` or `OFFSET`.
- A generated database password in a dedicated volume, read from a file rather than an environment variable.
- Scalar over the .NET 10 OpenAPI document as the only viewer, with its assets bundled so the page makes no third-party requests.

**What I rejected or changed**

- The assistant first advised SQLite, three tests, and no Docker to finish safely. I chose PostgreSQL and Docker because the reviewers will run the project, and I kept the full test suite.

**What I corrected**

Failing tests and independent reviews caught these before delivery. The HTTP fixes each started from a test that failed first:

- Text containing a NUL character caused a PostgreSQL error and a `500`; it now returns `400`.
- A `Host` header outside the allowlist was served; it is now rejected.
- Repeated query parameters and numbers with a trailing NUL returned `200`; they now return `400`.
- Rate limiting, generic `500` bodies, and Problem Details for unmatched routes were missing at first and were added with tests.
- Scalar enabled telemetry by default; the security review flagged it, and I turned it off and asserted that in a test.
- The test database image is pinned by digest and bound to loopback only, after a security review note.

**How I verified it**

The tests are integration tests against real PostgreSQL, not mocks. Some independent verification runs could not start the test containers, so I reran the exact commands myself. I treated passing review as evidence, not proof, and nothing was pushed until the clean-clone run passed.
