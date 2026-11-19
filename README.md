# Task Management API

A .NET 10 API backed by PostgreSQL, intended to run locally with Docker Compose.

## Run locally

With Docker and the Compose plugin installed and running, start the stack from the repository root:

```sh
docker compose up --build
```

The first run pulls the public base images and restores the locked NuGet dependencies. Compose generates the database password, waits for PostgreSQL, then runs a one-shot `--migrate` job. The API starts at `http://localhost:8080` only after the migration job succeeds. No `.env` file or manual database setup is required.

Use `docker compose down` to stop the stack while keeping the database and its password for the next run.

## Local-only security assumptions

PostgreSQL has no published host port and the API binds to loopback. The password is stored in a dedicated Docker volume; only the initializer has write access. It is not in the build, repository, or Compose environment settings. The volume is not encrypted, and administrators of the Docker engine can read it. Keep private material outside the build context.

For this single-user demo, the API and migration job share the bootstrap database role. Before a shared or production deployment, use a restricted runtime role, separate migration credentials, a managed secret store with per-service access, verified TLS, and reviewed image digests.
