# Database Foundation

PostgreSQL infrastructure is present in Compose, but no EF Core model,
migrations or application persistence code is checked in yet.

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
- seed only deliberate development/reference data
- do not store passwords or tokens in domain tables
- do not store hidden Agentic AI reasoning
