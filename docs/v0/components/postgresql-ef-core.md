# PostgreSQL and EF Core foundation

## Responsibility and source

PostgreSQL 16 is the relational store. The implemented Auth persistence
model is `services/auth/Data/AuthDbContext.cs` with checked-in migrations
under `services/auth/Data/Migrations/`. Auth uses Npgsql/EF Core and
applies relational migrations at startup before bootstrap administrator
seeding. The public API does not carry a database credential in the local
Compose topology; clients never connect to PostgreSQL.

## Implemented model

| Entity/table | Purpose |
|---|---|
| Users | Account identity, password hash, active state and token version |
| Roles, Permissions | Named role and permission definitions |
| UserRoles, RolePermissions | Many-to-many assignments |
| DeviceInstallations | Server-issued installation identifier and hashed device proof |
| ActiveSessions | Current account/installation sessions |
| UserSessionLogs | Ended session history; excluded from authentication |
| RefreshTokens | Hashed rotating tokens tied to an active or archived session |

The model has unique email, role and permission-code indexes; composite
assignment keys; a unique active `(UserId, DeviceId)` key; session and
refresh-token lookup indexes; and a check constraint requiring a refresh
token to reference exactly one active session or archived log. Migrations,
constraints and indexes must evolve with the owning behavior, including
an upgrade path for existing rows.

## Access and verification

Auth alone accesses its data through the service/EF Core layer. Keep
transaction boundaries around multi-step session and permission changes;
persist hashes and minimum necessary audit fields, never raw credentials
or hidden model reasoning.

Compose binds PostgreSQL to `127.0.0.1:5432` for local host tools while
Auth uses the private database network. A production database must remain
privately managed. The [database reference](../../database/README.md) and
[schema guide](../../database/schema.md) provide the operational details.

Deterministic provider-independent tests can use an isolated substitute.
SQL translation, migrations, constraints, indexes, locking and concurrency
require real PostgreSQL evidence through the explicit
[Auth provider test path](../../../services/auth/tests/README.md).
