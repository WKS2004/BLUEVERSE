# Experience & Biodiversity service

The service owns the coastal destination, activity, offering, schedule and
favourite tables in PostgreSQL. Normal service startup applies its migrations
and the existing canonical reference catalogue. Synthetic demo records use a
separate, explicit command and are never added by normal startup.

## Seed local synthetic demo data

Prerequisites: the repository's local PostgreSQL database must be running and
the connection must point to the local development database. Set the password
in your shell from your local development secret; do not put it in source or
share command output containing it.

From the repository root in PowerShell, set the Development environment and
connection string, then run:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=5432;Database=blueverse;Username=blueverse;Password=YOUR_LOCAL_POSTGRES_PASSWORD"
dotnet run --project services/experience-biodiversity --configuration Release -- --seed-synthetic-demo-data
```

The command applies only the Experience & Biodiversity migrations and adds up
to 24 deterministic synthetic records: four destinations, four activities,
eight offerings and eight schedules. It preserves existing rows and can be
rerun safely. Schedule windows begin the next local calendar day at 08:00 in
`Asia/Colombo` and span 180 days, including current-day availability. Names and descriptions identify these rows as
synthetic demo data. The command refuses to run outside
`ASPNETCORE_ENVIRONMENT=Development`, prints counts only, and does not start an
HTTP listener.

Clear the shell variables after use:

```powershell
Remove-Item Env:ConnectionStrings__DefaultConnection
Remove-Item Env:ASPNETCORE_ENVIRONMENT
```

The deterministic seed tests run with:

```powershell
dotnet test services/experience-biodiversity/tests/Blueverse.ExperienceBiodiversity.Tests/Blueverse.ExperienceBiodiversity.Tests.csproj --configuration Release --filter "FullyQualifiedName~SyntheticDemoDataSeederTests"
```
