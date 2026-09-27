# Coastal Planner service

The Coastal Planner owns recommendation workflows, saved itineraries, their
re-evaluation snapshots, and its validated biodiversity prediction cache. Its
public routes use the short `/api/planner/...` prefix and are exposed to
clients through the API gateway. The service does not depend on any peer
service being present to start or stay healthy.

## Peer service calls

The configured URLs identify service roots. The adapter currently uses these
proposed endpoint shapes while the other components and their team contracts
are being implemented:

| Peer | Request path | Use |
|---|---|---|
| Experience Catalogue | `GET /api/experiences/catalogue?destinationId={guid}&activityIds={comma-separated-guids}` | Published and available offering facts |
| Marine Conditions | `GET /api/marine/suitability?destinationId={guid}&activityId={guid}&start={utc}&end={utc}` | Sourced, time-bound suitability |
| Coastal Operations | `GET /api/operations/status?destinationId={guid}` | Current operating restrictions |
| Biodiversity ML | `GET /predict?destinationId={guid}&activityId={guid}` | Optional contextual species prediction |

Set the roots through `PeerServices:ExperienceCatalogueUrl`,
`PeerServices:MarineConditionsUrl`, `PeerServices:CoastalOperationsUrl`, and
`PeerServices:BiodiversityMlUrl`. The defaults point to their expected
internal service names. These hostnames are never used by clients.

`PeerServices:TimeoutSeconds` defaults to 3 seconds per attempt and accepts
1–30 seconds. `PeerServices:RetryCount` defaults to 1 retry after the initial
request and accepts 0–5 retries. Timeouts, non-success responses, malformed
JSON, and unreachable hosts produce a dependency-specific uncertainty note.
Request cancellation still cancels the planner request.

Recommendations complete with an empty candidate list when required catalogue,
marine, or operating evidence cannot be verified. The planner never invents
an offering, availability, safety profile, suitability, or operating status.
Biodiversity is optional: an outage returns `UNAVAILABLE`, no species and no
model metadata, and is not cached. Peer health is excluded from service startup
and readiness; `/api/planner/health` reports only this service's database and
migration readiness.

The adapter validates peer response identity and required fields. Confirm the
proposed paths and payloads with the owning teams when their components are
available; do not make missing peer services a Compose startup dependency.
Marine `ConditionTimestamp` is the condition's forecast/observation time, not
necessarily its retrieval time, so a UTC forecast timestamp may be in the
future. The Marine Conditions service owns profile-specific freshness
classification; the planner does not invent a freshness window.

## Authorization and ownership

Protected routes require the exact permission claim declared by the endpoint
catalog. Itinerary, recommendation, and workflow reads are filtered by the
authenticated actor ID. A caller cannot read another actor's saved results by
guessing a GUID.

## PostgreSQL migrations

Coastal Planner migrations are owned by this service in `Data/Migrations` and
use the repository-pinned `dotnet-ef` tool. Set
`ConnectionStrings__DefaultConnection` in the shell before running EF tools;
the design-time factory intentionally does not embed a connection string.

```powershell
dotnet tool restore
$env:ConnectionStrings__DefaultConnection = $env:COASTAL_PLANNER_CONNECTION_STRING
dotnet ef migrations add CoastalPlannerSchemaChange --project services/coastal-planner/Blueverse.CoastalPlanner.csproj --startup-project services/coastal-planner/Blueverse.CoastalPlanner.csproj --output-dir Data/Migrations
dotnet ef migrations script --project services/coastal-planner/Blueverse.CoastalPlanner.csproj --startup-project services/coastal-planner/Blueverse.CoastalPlanner.csproj
```

The application applies its migrations at startup when PostgreSQL is
available. It logs initialization failures and stays running; the health route
remains unready until its own database and migrations are ready.

## Tests

Run the package-local suite with:

```powershell
dotnet test services/coastal-planner/tests/Blueverse.CoastalPlanner.Tests/Blueverse.CoastalPlanner.Tests.csproj
```

The suite uses deterministic in-memory persistence for provider-independent
application behavior and an HTTP test server for JWT permissions, owner
scoping, and peer outage behavior. It does not substitute for PostgreSQL
migration, constraint, or concurrency evidence.
