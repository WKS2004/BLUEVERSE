# BLUEVERSE — Project Requirements

## 1. Purpose

BLUEVERSE is an intelligent ecosystem platform for **Coastal Tourism & Marine Resilience**.

It is not intended to become a generic tourism application. Major capabilities must have a meaningful relationship to the coastal/marine ecosystem.

## 2. SE3090 mandatory integration

The final system must integrate:

- ASP.NET Core Web API
- Entity Framework Core with PostgreSQL provider
- PostgreSQL
- React
- Flutter/Dart
- Agentic AI
- Git/GitHub
- automated testing and CI/CD
- deployment and technical documentation

React and Flutter use the same API, database, auth, permissions and business rules.

If Agentic AI is implemented as an internal Python service, clients must never call it directly. ASP.NET Core is the public application boundary and invokes internal AI services.

## 3. v0 Foundation

v0 is infrastructure and security foundation only.

### v0 includes

- repository conventions
- Docker infrastructure
- Docker Hardened Images
- edge Nginx gateway
- React foundation
- Flutter foundation
- ASP.NET Core API foundation
- separate Auth service foundation
- PostgreSQL
- authentication
- authorization
- role/permission model
- health endpoints
- Swagger/OpenAPI
- Postman/API verification
- CI foundation
- documentation and ADR foundation

### v0 excludes

- major tourism business workflows
- major environmental workflows
- fisheries workflows
- final Agentic AI business workflow
- final biodiversity ML integration

## 4. Authorization model

Authorization is permission-oriented:

```text
User → Role(s) → Permission(s)
Request → Required Permission
```

New roles begin with zero permissions.

Business components must check permissions rather than hard-code stakeholder role names such as:

```csharp
if (role == "Admin")
```

## 5. Planned major phases

### v1 — Coastal Tourism & Operations

Primary focus:

- tourists
- coastal operations and management
- destinations
- activities
- recommendations
- marine conditions
- safety
- alerts
- tourism operations
- tourism-related AI workflows

### v2 — Environmental Resilience

Primary focus:

- environmental authorities/organizations
- environmental incidents
- pollution reporting
- environmental intelligence
- restrictions
- AI-assisted environmental decisions
- human approval workflows

### v3 — Fisheries & Coastal Livelihoods

Primary focus:

- fisheries stakeholders
- coastal communities
- marine-resource information
- observations
- fisheries/coastal workflows

## 6. Cross-platform responsibility

### React

Primarily supports:

- administration
- staff/operations
- dashboards
- reporting
- data management
- AI monitoring
- approval workflows

### Flutter

Primarily supports:

- coastal/tourist experiences
- operational field workflows
- mobile reporting
- GPS/location
- camera/evidence capture
- notifications where appropriate

The two clients must have genuinely different responsibilities.

## 7. Agentic AI principles

The final Agentic AI workflow must be meaningful and multi-step.

The architecture must support:

1. planning
2. delegation
3. tool selection/use
4. structured outputs
5. persisted workflow state where required
6. deterministic validation
7. business-rule enforcement
8. human approval for appropriate high-impact actions
9. execution history
10. failure recovery
11. safe failure
12. prompt-injection resistance

AI recommendations are not the ultimate authority for high-impact actions.

Preferred safety pattern:

```text
AI Recommendation
       ↓
Deterministic Safety Rules
       ↓
Human Approval
       ↓
Execution
```

Do not persist hidden model reasoning. Persist only workflow state and execution information required by the design.

## 8. Marine Biodiversity Intelligence

The separate IT3091 ML workstream is planned to become a genuine BLUEVERSE capability.

Initial concept:

```text
OBIS + Bio-ORACLE
        ↓
ML model
        ↓
Species occurrence probability
        ↓
Habitat suitability
        ↓
Spatial biodiversity intelligence
        ↓
BLUEVERSE decision support
```

The exact focal species, preprocessing, pseudo-absence method, feature set, model lineup and evaluation remain data-dependent and are not fixed by this foundation.

## 9. Quality requirements

The final project must demonstrate:

- secure API design
- JWT authentication
- protected endpoints
- permission-based authorization
- password hashing
- secure configuration
- server-side validation
- global error handling
- structured logging
- CORS
- Swagger/OpenAPI
- EF Core migrations
- relational constraints/indexes
- appropriate transaction handling
- audit fields
- backend tests
- React tests
- Flutter analysis/tests
- integration tests where appropriate
- Agentic AI evaluation
- CI evidence
- deployment evidence

## 10. Repository engineering requirements

- Git/GitHub from the beginning
- small, traceable commits
- pull requests for team integration
- no secrets in Git
- ADRs for important architecture decisions
- documentation updated with architectural changes
- individual contribution must remain visible in Git history

## 11. Versioning

Use Semantic-Versioning-shaped `x.y.z` numbering with BLUEVERSE-specific phase semantics.

Example:

```text
2.10.6
```

means major phase 2, integration release 10, bug-fix release 6.

## 12. Definition of foundation completion

v0 is considered structurally ready when:

- repository conventions are committed
- Docker gateway/network definitions are present
- DHI Dockerfiles are present
- generated client/service projects are placed in their expected locations
- local Compose can build and start the services
- health endpoints work through the gateway
- Swagger is reachable through the gateway
- PostgreSQL migrations can be introduced
- Flutter can reach the gateway from a real device/emulator configuration
- CI builds and tests generated backend projects
- documentation explains setup and architecture
