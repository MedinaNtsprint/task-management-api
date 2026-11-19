# Task Management API

A .NET 10 API backed by PostgreSQL, intended to run locally with Docker Compose.

## Run locally

With Docker and the Compose plugin installed and running, start the stack from the repository root:

```sh
docker compose up --build
```

The first run pulls the public base images and restores the locked NuGet dependencies. Compose generates the database password, waits for PostgreSQL, then invokes the application with `--migrate` as its only argument. The API becomes available at `http://localhost:8080` only after that job succeeds. No `.env` file or manual database setup is required.

Use `docker compose down` to stop the stack while keeping the database and its password for the next run.

## Database recovery

- **PostgreSQL stopped after startup:** Run `docker compose start db`. The API may still be listening while PostgreSQL is down, but database-backed requests cannot succeed until it is ready again.
- **Migration cannot authenticate after a restore:** Restore `db-data` and its matching `db-secret` volume together. Starting with existing database data and a new secret creates a password mismatch: the readiness probe can pass, but the migration cannot authenticate. Restore the original matching secret or have a database administrator reset the role password to match the replacement credential. Then rerun the normal startup command without deleting the data volume.
- **Renaming database settings:** Keep `POSTGRES_DB`, `POSTGRES_USER`, and `POSTGRES_PASSWORD_FILE` in `compose.yaml` in sync with `src/TaskManagement.Api/Data/DatabaseConnection.cs`. Update the health check for a database or user change, the initializer and volume mounts for a password path change, and the application host if the `db` service is renamed. Changing bootstrap settings in Compose does not change an already-initialized database.

## Local-only security assumptions

Compose publishes the API on the host's loopback interface (`127.0.0.1:8080`). That mapping does not restrict the listener inside the container or apply when the image is run outside this Compose setup. PostgreSQL has no published host port. Its password is stored in a dedicated Docker volume; only the initializer has write access. It is not in the build, repository, or Compose environment settings. The volume is not encrypted, and administrators of the Docker engine can read it. Keep private material outside the build context.

For this single-user demo, the API and migration job share the bootstrap database role. Before a shared or production deployment, use a restricted runtime role, separate migration credentials, a managed secret store with per-service access, verified TLS, and reviewed image digests.
