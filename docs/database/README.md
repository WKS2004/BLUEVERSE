# Database Foundation

PostgreSQL infrastructure is present in Compose. The Auth EF Core model,
migrations and application persistence code are checked in under
`services/auth`.

PostgreSQL is the authoritative relational database.

## Local service

```text
postgres:5432
```

Local Compose explicitly publishes PostgreSQL on host port `5432` so pgAdmin4 can connect to it. The DHI volume is mounted at `/var/lib/postgresql`, allowing the image to manage its versioned `16/data` directory. This is intended for local development only; production databases must remain privately managed.

Use these pgAdmin4 connection settings:

```text
Host: 127.0.0.1
Port: 5432
Database: blueverse
Username: blueverse
Password: the POSTGRES_PASSWORD value from .env
```

## Rules

- use EF Core migrations
- normalize relational data appropriately
- define primary/foreign keys
- use constraints and indexes
- maintain `CreatedAt` / `UpdatedAt` where appropriate
- use transactions for multi-step operations that require atomicity
- Auth device installations use server-issued opaque IDs and hashed device
  keys; legacy client IDs are marked explicitly while they migrate
- Auth sessions use the `ActiveSessions` table with a unique `(UserId, DeviceId)`
  key and indexed active-device lookups; ended sessions move to the indexed
  `UserSessionLogs` archive with an end timestamp and reason
- Auth refresh-token rows store only hashes and support rotation/reuse audit;
  each token references exactly one active session or archived session log; no
  bearer token or device key is stored in plaintext
- seed only deliberate development/reference data
- Auth applies `services/auth/Data/Migrations` at startup before bootstrap
  administrator seeding
- Use the repository-pinned `dotnet-ef` tool manifest when creating or
  inspecting migrations
- do not store passwords or tokens in domain tables
- do not store hidden Agentic AI reasoning
