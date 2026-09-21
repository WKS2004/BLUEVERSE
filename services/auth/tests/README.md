# Auth test suite

This suite is the authoritative test location for `services/auth`. It uses
xUnit and `WebApplicationFactory` with an isolated EF Core in-memory database
for deterministic endpoint and authorization tests.

Run it from the repository root:

```powershell
dotnet test services/auth/tests/Blueverse.Auth.Tests/Blueverse.Auth.Tests.csproj --configuration Release
```

The default suite currently passes 23 cases. It covers registration/login with
server-issued device sessions, hashed refresh-token rotation and replay
revocation, protected cookie transport, the five-account device boundary,
five-session account eviction, account/device/everywhere logout scopes,
profile-only updates, explicit password changes, active-session archival from
`ActiveSessions` into `UserSessionLogs` with refresh-token relinking,
protected endpoints, individual role and permission reads,
strict role and permission validation, system-role escalation prevention,
immediate token revocation after deactivation, password hashing and required
JWT signing-key configuration. The default suite uses InMemory for deterministic
tests. The PostgreSQL-backed capacity/refresh smoke test is opt-in so it can
run against a disposable or explicitly selected database without making CI
depend on a developer database:

```powershell
$env:BLUEVERSE_AUTH_POSTGRES_TEST_CONNECTION = "Host=localhost;Port=5432;Database=blueverse;Username=blueverse;Password=<local-secret>"
dotnet test services/auth/tests/Blueverse.Auth.Tests/Blueverse.Auth.Tests.csproj --configuration Release -p:EnablePostgresAuthTests=true --filter "TestId=AUTH-POSTGRES-SESSION-001"
```

Do not commit the connection string. PostgreSQL migrations are applied by the
Auth startup path and are also verified by the Docker smoke checks.
