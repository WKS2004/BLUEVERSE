# BLUEVERSE — Project Requirements

> **Status:** FINAL — v0 Foundation and v1 Requirements Baseline
> **Project:** BLUEVERSE
> **Domain:** Coastal Tourism & Marine Resilience
> **Group:** 2026-AI-45
> **Submission implementation scope:** v0 + v1
> **Future roadmap:** v2 and v3 remain outside the current implementation deadline
> **SE3090 submission deadline:** 30 September 2026, 11:50 PM

---

# 1. Purpose

BLUEVERSE is an intelligent cross-platform ecosystem for **Coastal Tourism & Marine Resilience**.

Its purpose is to bring together coastal tourism information, marine conditions, safety intelligence, biodiversity intelligence and coastal operational decision support so that tourists and coastal operators can make better-informed decisions.

BLUEVERSE must **not** become a generic tourism application.

Every major business capability must maintain a meaningful relationship with one or more of:

* coastal destinations;
* coastal activities;
* marine conditions;
* marine safety;
* marine biodiversity;
* coastal operations;
* coastal sustainability; or
* coastal decision support.

---

# 2. Final Delivery Scope

The implementation target for the SE3090 assignment is:

```text
v0 — Foundation
        ↓
v1 — Coastal Tourism & Operations
        ↓
SUBMISSION / EVALUATION
```

The team will fully implement, integrate, test, document and deploy **v0 and v1**.

The following remain planned future major phases:

```text
v2 — Environmental Resilience
v3 — Fisheries & Coastal Livelihoods
```

Features whose primary purpose belongs to v2 or v3 must not be added to the current implementation unless they are essential to an approved v1 workflow.

This scope freeze exists to protect the completeness, stability and demonstrability of v1.

---

# 3. v0 Foundation Baseline

v0 supplies the shared application, identity, persistence and engineering
foundation for every later business component. Its eight technical components
are specified below and in the [v0 foundation guide](docs/v0/README.md).
The guide maps each contract to its owning source, operational instructions and
verification evidence. The same foundation supports every authorized role in
both React and Flutter; release phase does not assign a platform to a role.

## 3.1 Foundation Scope

v0 includes:

* repository conventions, Git/GitHub collaboration and contribution records;
* the selected Docker Hardened Images, Docker Compose, private networks and
  the local edge Nginx gateway;
* React Web and Flutter Mobile application foundations with a shared Auth and
  session-management workflow;
* the ASP.NET Core public API and separate internal Auth/Identity service;
* PostgreSQL, EF Core models and checked-in migrations;
* registration, login, JWT validation, refresh, logout, device sessions,
  permission-based authorization and account administration;
* public health and Swagger/OpenAPI routes, plus API/Postman verification;
* the cross-client workflow registry, endpoint catalog, automated tests and
  GitHub Actions CI;
* architecture, security, database, setup, testing and ADR documentation; and
* repository agent instructions, rules, skills and resource validation.

## 3.2 Phase Boundary

The four coastal tourism and operations business components, their four
Agentic AI contributions, Member 1's map API integration, Member 2's Open-Meteo
marine integration and BLUEVERSE's biodiversity ML integration belong to v1.
v0 establishes their shared client, API,
authorization, database, gateway, test and documentation contracts. This phase
boundary does not reduce the role or workflow coverage required of either
client when a business capability is introduced.

## 3.3 React Web Client

The [React component contract](docs/v0/components/react-web-client.md) covers
the browser application in `apps/web`. Its `/`, `/signin`, `/signup`,
`/profile`, `/dashboard`, `/admin`, `/admin/permissions`, `/admin/roles`,
`/admin/users`, `/404` and `/500` routes provide the foundation entry,
registration, account and permission-aware Auth administration experiences.
The Auth adapter calls relative public `/api/auth/...` paths with browser
credentials. Protected HttpOnly cookies carry the browser session; token
secrets are not exposed in browser response JSON.

The profile supports editable account details, password changes, session
review and revocation, and account deletion. The dashboard presents current
account information and labels unavailable coastal services as future work.
The administration routes expose permission-checked user, role and permission
operations. Added role-permitted workflows must use server-owned permissions,
registered public API references and an equivalent Flutter route and business
outcome. Browser layout, accessibility, validation, loading, empty, success
and error behavior remain part of the component contract.

## 3.4 Flutter Mobile Client

The [Flutter component contract](docs/v0/components/flutter-client.md) covers
`apps/mobile`. Its `/`, `/signin`, `/signup`, `/profile`, `/dashboard`,
`/admin`, `/admin/permissions`, `/admin/roles`, `/admin/users`, `/404` and
`/500` routes provide onboarding, registration, account, dashboard and
permission-aware Auth administration experiences. The client calls the public
gateway through `/api/auth/...` paths, uses bearer access tokens for native
requests, and stores the installation ID, device proof, access token and
rotating refresh token behind platform secure storage. At launch it restores
an existing account to Dashboard or presents the signed-out onboarding
carousel. Profile and administration actions use the same server permissions
and outcomes as React.

The UI, logic and data layers preserve one server-owned authorization and
business contract with React. A mobile-specific input or network configuration
must not remove a role-permitted action. Local emulator, physical-device and
hosted configurations target the approved public gateway, never an internal
Docker service.

## 3.5 Public API and Gateway

The [public API component contract](docs/v0/components/public-api-gateway.md)
covers `services/api` and its edge entry. ASP.NET Core is the sole public
application boundary for both clients. It exposes `/api/health`, validates
access JWTs and browser access cookies, applies CORS and forwarded-header
configuration, publishes Swagger/OpenAPI, handles unexpected errors in a
structured form, and forwards `/api/auth/...` through YARP to internal Auth.
The public gateway also exposes the documented health and API/Auth OpenAPI
routes.

New domain operations must enter through public `/api/...` routes with
validated DTOs, named permissions, application services, structured errors,
OpenAPI and tests. Public API signature validation and Auth's active-session
check are separate guarantees: immediate revocation for a future non-Auth
domain endpoint requires an explicit server-side design.

## 3.6 Auth, Identity and Access Control

The [Auth component contract](docs/v0/components/auth-identity-access.md)
covers `services/auth`. Auth owns registration, sign-in, refresh, profile and
password changes, logout, session listing and revocation, and
permission-gated user, role and permission administration. It keeps the
role-to-permission model authoritative on the server. A new role begins
without business permissions. System-role management requires its dedicated
permission.

The v0 Auth permission codes are `auth.user.read`, `auth.user.manage`,
`auth.user.create`, `auth.user.update`, `auth.user.delete`, `auth.role.read`,
`auth.role.manage`, `auth.role.create`, `auth.role.update`, `auth.role.delete`,
`auth.permission.read` and `auth.role.system.manage`. Management requests
require the relevant read permission together with the action permission.
Passwords are hashed; credentials and signing keys
come from secure configuration rather than source or logs. All client-facing
Auth operations remain behind the public API.

Auth issues a server-owned installation ID and device proof. One installation
can hold at most five active account sessions, and one account at most five
active sessions across installations; a sixth account session evicts the
oldest. Repeated sign-in to the same account on one installation reuses its
session row. Default access JWT lifetime is 15 minutes. An absolute session
expires after one day, or 30 days with Remember Me. One-time hashed refresh
tokens rotate without extending that absolute expiry; replay revokes the
session. Logout and security changes revoke relevant sessions and retain
ended-session audit history.

## 3.7 PostgreSQL and EF Core

The [data component contract](docs/v0/components/postgresql-ef-core.md)
covers PostgreSQL 16 and Auth's EF Core/Npgsql model and checked-in migrations.
Auth owns the identity and session tables: users, roles, permissions,
assignments, device installations, active sessions, ended-session logs and
refresh tokens. Unique keys, relationship constraints, lookup indexes and
transactions protect account, permission and session behavior. Active
sessions authorize access; ended-session logs record history and never
authenticate.

Only owning backend services access PostgreSQL. The local Compose stack keeps
the database on a private service network and binds host access to
`127.0.0.1:5432` for local tooling. Provider-specific migration, constraint,
query and concurrency behavior requires evidence against real PostgreSQL;
provider-independent tests alone cannot establish it.

## 3.8 Docker and Local Network

The [Docker component contract](docs/v0/components/docker-local-stack.md)
covers `compose.yaml` and `infrastructure/docker/`. The selected hardened
images are Node 24 Debian 13 development, Nginx 1.30 Alpine, .NET 10 SDK
Alpine, ASP.NET Core 10 Alpine and PostgreSQL 16 Alpine. The local path is:

```text
host → edge-nginx ─┬─→ React
                   └─→ public API → internal Auth → PostgreSQL
```

The edge, internal and database networks keep Auth and PostgreSQL off direct
client routes. The gateway publishes host port 80 by default; API and Auth
expose only their network-facing service ports. Secrets enter through
environment configuration. The stack must provide health checks and the
documented public health and OpenAPI paths. Runtime commands must respect the
minimal ASP.NET image rather than assuming a shell is present.

## 3.9 Shared Contracts, Tests and CI

The [contract and CI component](docs/v0/components/contracts-ci-validation.md)
connects both clients to one public workflow registry and a
source-checked route inventory. The v0 registry includes the shared home
entry and `auth-session-management` workflow. The endpoint catalog records
public API/Auth, gateway, frontend and other declared routes; its JSON source
generates the readable Markdown view. Actual implementation source remains
authoritative for behavior.

Changing a UI route or client request requires synchronized registry and
catalog entries. Changing a public, internal, gateway, health or OpenAPI route
requires a catalog update. Validation rejects unregistered client routes,
internal-service targets, unapproved hosts, unverifiable dynamic targets and
versioned `/api/v1`-style paths. CI and owning package tests provide
independent evidence for UI contracts, backend behavior, Auth, PostgreSQL,
repository resources and Docker integration.

## 3.10 Repository and Agent Resources

The [repository component contract](docs/v0/components/repository-agent-resources.md)
covers the root `AGENTS.md`, `.agents/` routing, focused rules, workflow
skills and validators, alongside ADRs and contribution evidence. These
resources state universal architecture and working rules. Requirements and
the owning component documents hold phase-specific behavior; agents select
the applicable material from the task and verify implementation claims
against source and tests.

Each AI-assisted contribution records the confirmed acting member, task,
tools/model and verification in that member's usage log. Architecture and
contract changes must keep their source, tests, documentation and relevant
ADR synchronized.

## 3.11 v0 Integration and Acceptance

The [v0 acceptance guide](docs/v0/integration-and-acceptance.md) describes
the shared sign-in path: either client submits credentials through
`POST /api/auth/login`; Auth creates or reuses the PostgreSQL-backed
installation session; the client reads `GET /api/auth/me` and
`GET /api/auth/sessions`, and refreshes through `POST /api/auth/refresh` when
authorized. `POST /api/auth/logout` ends the authenticated account's session
on the current device; `POST /api/auth/logout-account` supports signing out a
selected saved account while preserving other accounts on that device; and
`POST /api/auth/logout-all-devices` ends all sessions for the authenticated
account after current-password verification. Browser cookies and native secure
storage differ in transport; permission, identity and session results are
server-owned and shared.

Acceptance evidence must cover both clients' route/request contracts, API and
Auth responses and authorization, migration and relational behavior, public
gateway health/OpenAPI reachability, repository validators and CI discovery.
A successful build or matching HTTP status alone is insufficient: response
content, persisted state, revocation and side effects must match the
documented contract.

---

# 4. Mandatory Technology Integration

The assessed system must integrate:

* ASP.NET Core Web API;
* Entity Framework Core with PostgreSQL provider;
* PostgreSQL;
* React;
* Flutter/Dart;
* Agentic AI;
* Git and GitHub;
* automated testing;
* GitHub Actions CI;
* deployment;
* technical documentation; and
* Architecture Decision Records.

React and Flutter must use the same:

* ASP.NET Core application boundary;
* PostgreSQL-backed business data;
* identity;
* authorization model;
* business rules; and
* Agentic AI workflows where applicable.

Disconnected prototypes do not satisfy BLUEVERSE requirements.

The [v0 foundation](docs/v0/README.md) establishes the shared boundary and
identity path; the [v1 component and agent guide](docs/v1/README.md) specifies
the assessed coastal business workflows built on it.

---

# 5. Public Application Boundary

ASP.NET Core is the authoritative public application layer.

For the local Docker stack, the high-level communication pattern is:

```text
React Web ──┐
            ├──► edge-nginx ──► public /api/... ──► ASP.NET Core API
Flutter ────┘                                      │
                       ┌──────────────────────────┼──────────────────────┐
                       ▼                          ▼                      ▼
               Auth / domain services     private Agentic AI     private ML inference
                       │
                       ▼
                   PostgreSQL

ASP.NET Core-managed integration ──► approved external services
```

In hosted deployments, both clients still use the approved public API
boundary; the local `edge-nginx` container is not a required production
hosting topology.

Client-facing authentication is exposed through approved public
`/api/auth/...` routes. The public API forwards those routes to internal
Auth; the clients never address Auth directly.

React and Flutter must never directly access:

* PostgreSQL;
* internal Auth/Identity hostnames;
* the internal Agentic AI service;
* the internal ML service;
* private microservice hostnames; or
* external services that should be mediated by the backend.

Where Agentic AI or ML is implemented in Python, it remains an internal service invoked through the ASP.NET Core application boundary.

---

# 6. Cross-Platform Integration Contract

Every new or modified user-facing workflow must be implemented in both clients and represented in:

```text
docs/contracts/ui-integration.json
```

The current registry entry identifies:

* a stable shared workflow ID;
* the React route and owning source file;
* the Flutter route and owning source file; and
* public `/api/...` endpoint references, with method, owner and operation
  metadata in the registry's endpoint definitions.

The owning API operation and component contract define required permissions.
Both clients must expose each permitted role and action through their
registered workflow, even when several roles share one route. The registry
schema does not require a separate route or permission field for every role.
See the [UI integration contract](docs/development/ui-integration.md).

React and Flutter should use literal relative public paths such as:

```text
/api/destinations
/api/activities
/api/marine-conditions
/api/itineraries
/api/operations/assessments
/api/agent/workflows
```

Absolute API hosts may only be used where explicitly allowed by the contract.

Dynamic request targets that cannot be verified by CI are invalid unless
explicitly supported by the integration contract. The
[endpoint catalog](docs/api/endpoint-catalog.md) is the complete current route
inventory; illustrative paths in this requirements document are proposed
contracts until their implementation and catalog entries exist.

The shared UI integration validator must pass before a workflow is considered complete.

---

# 7. Authorization Model

BLUEVERSE uses **Role-Based Access Control with permission-based enforcement**.

Conceptually:

```text
User
  ↓
Role(s)
  ↓
Permission(s)
  ↓
Requested Operation
  ↓
Required Permission
```

Roles organize permissions.

Business components authorize operations using permissions rather than hard-coded stakeholder names.

Avoid:

```csharp
if (user.Role == "Admin")
```

Prefer:

```text
Does the current user possess the permission
required by this operation?
```

A newly created role begins with zero business permissions until permissions are explicitly assigned.

This retains RBAC while allowing BLUEVERSE to introduce future stakeholder roles without rewriting business components.

Auth foundation permissions and session behavior are specified in
[section 3.6](#36-auth-identity-and-access-control); v1 domain permissions
are enforced by the owning server operation.

---

# 8. Initial v1 Roles

The following seed roles support v1.

They are configuration defaults rather than hard-coded authorization rules.

## 8.1 Tourist

Primary responsibilities:

* discover destinations;
* discover activities;
* view marine conditions;
* view safety information;
* request recommendations;
* create/manage personal itineraries;
* view relevant alerts;
* view biodiversity intelligence.

Uses both React and Flutter for every permitted tourist workflow.

---

## 8.2 Coastal Operator

Primary responsibilities:

* manage or monitor permitted coastal activity operations;
* manage operational schedules where permitted;
* inspect marine conditions;
* initiate operational assessments;
* monitor assessment results;
* view operational alerts.

Uses both React and Flutter for every permitted operator workflow.

---

## 8.3 Operations Reviewer

Primary responsibilities:

* monitor operational assessments;
* inspect AI recommendations;
* inspect deterministic validation;
* approve recommendations;
* reject recommendations;
* request revision;
* manage advisories/alerts where authorized.

Uses both React and Flutter for every permitted review workflow.

---

## 8.4 Platform Administrator

Primary responsibilities:

* users;
* roles;
* permissions;
* system configuration;
* authorized audit visibility.

Platform Administrator does not automatically bypass business permissions.

Platform administration workflows are available in both React and Flutter
subject to the same permissions.

---

# 9. Equal React and Flutter Product Coverage

React Web and Flutter Mobile are equally complete product surfaces. The v0
Auth and session workflow is shared, and every v1
role—Tourist, Coastal Operator, Operations Reviewer and Platform
Administrator—can complete every permitted business workflow in either client.
Neither platform owns, prioritizes or restricts a stakeholder group, component,
business action or approval. The same server-owned permissions, business data,
workflow outcomes and public API apply in both clients.

This equal-coverage rule also applies to new stakeholder roles and workflows in
future phases. Responsive layout, input methods and device capabilities may
differ without changing who can perform a business action.
React must provide a usable browser application, and Flutter must provide a
genuine mobile application with its required device-location feature. Both
deliver the same permitted business outcomes in their respective contexts.

## 9.1 Capabilities Required in Both Clients

For the roles authorized to use them, both React and Flutter must provide:

* coastal discovery and destination/activity search;
* destination, activity, offering and schedule management;
* recommendations, favourites and itinerary management;
* marine conditions, freshness, safety information and safety-profile management;
* biodiversity information;
* operational assessment initiation, progress and results;
* Agentic AI workflow review and authorized human approval;
* advisories, alerts and operational history;
* reporting and analytics;
* authorized user, role, permission, configuration and audit management.

The interface in each client must make these capabilities usable for its
intended screen and input method; displaying a link or raw API response alone
does not establish a complete workflow.

## 9.2 Flutter Device Capability

Flutter must provide at least one meaningful device capability.

The required v1 device capability is:

```text
GPS / Device Location
```

Location is used for coastal discovery and relevant location-aware workflows.
Manual alternatives must remain available where practical when location
permission is denied. The same location-aware business workflow must remain
usable in React through an appropriate location input; GPS is a mobile input
method, not ownership of coastal discovery.

Additional in-scope device interactions are Member 3's date/time selection
for coastal planning and Member 4's optional image evidence attachments for
BLUEVERSE-managed operational assessments. Member 2 accepts a requested period
as an ordinary marine-query business input; it has no separately assigned
device capability. The specific cross-platform behavior and security
boundary are in the
[v1 device-capability contract](docs/v1/device-capabilities.md). They do not
replace the required Flutter GPS capability.

---

# 10. Final Four v1 Business Components

BLUEVERSE v1 contains exactly four primary student-owned business components.

| Component | Final Name                                   | Primary Domain                                                  |
| --------- | -------------------------------------------- | --------------------------------------------------------------- |
| A         | Coastal Experience & Biodiversity Discovery  | Destinations, activities, availability and biodiversity context |
| B         | Marine Conditions & Safety Intelligence      | Weather/marine intelligence and deterministic suitability       |
| C         | Smart Coastal Planner & Itinerary Management | Recommendations, Agentic planning and itineraries               |
| D         | Coastal Operations, Advisories & Alerts      | Operational decisions, approval, state and alerts               |

The separate [member component](docs/v1/README.md#component-and-agent-contract-map)
and Agentic AI contracts in `docs/v1/` expand the requirements below into
inputs, outputs, authorization, cross-client behavior, failure cases and
acceptance evidence. Member numbers identify responsibility areas; they do
not, by themselves, identify a person or GitHub account.

Each component owner must contribute identifiable work across:

* ASP.NET Core;
* PostgreSQL/EF Core;
* React;
* Flutter;
* testing;
* documentation;
* Git/GitHub; and
* one distinct Agentic AI contribution.

No member may own only:

* project management;
* testing;
* documentation;
* frontend;
* backend.

---

# 11. Component Ownership Boundaries

Ownership must remain clear enough to defend through Git history and viva.

## Component A owns

* destinations;
* coastal activities;
* activity offerings;
* tourism availability;
* schedules associated with those offerings;
* favourites/saved experiences;
* the selected map API integration for location-aware discovery; and
* the user-facing biodiversity context surface, consuming Member 3's validated
  public prediction contract. Member 1 does not own the BLUEVERSE inference
  adapter or the separate IT3091 model/inference service.

## Component B owns

* external marine/weather acquisition;
* marine-condition snapshots;
* condition freshness;
* activity safety profiles;
* deterministic environmental suitability assessment.

## Component C owns

* recommendation requests;
* personalization constraints;
* Agentic AI workflow planning/delegation;
* recommendation assembly;
* itineraries;
* itinerary re-evaluation; and
* BLUEVERSE's backend-mediated consumer adapter and validated public result
  contract for the separate IT3091 biodiversity inference service. Member 3
  does not own or implement the IT3091 model or inference service itself.

## Component D owns

* operational assessment;
* operational recommendations;
* operational state;
* human approval;
* operational advisories;
* alerts;
* operational execution history.

Cross-component communication must occur through defined application/service contracts.

Shared workflows do not erase primary component ownership.

---

# 12. Component A — Coastal Experience & Biodiversity Discovery

Detailed member contract:
[Coastal Experience & Biodiversity Discovery](docs/v1/components/member-1-coastal-experience-biodiversity-discovery.md).

## 12.1 Purpose

Component A represents the coastal experiences BLUEVERSE exposes to tourists and operators.

It connects tourism discovery with genuine coastal and marine context rather than functioning as a generic tourism catalogue.

---

## 12.2 Main Business Data

The component must represent at minimum:

* coastal destinations;
* coastal activities;
* activity offerings or availability;
* schedules where required;
* destination/activity status;
* tourist favourites or saved experiences;
* biodiversity information associated with relevant locations.

Typical activities may include:

* surfing;
* snorkeling;
* diving;
* whale/dolphin watching;
* coastal boat activities;
* other approved marine/coastal experiences.

---

## 12.3 React and Flutter Requirements

Both clients must support the complete authorized Component A workflow:

* create, update, publish/unpublish and archive destinations where appropriate;
* create/manage activities, offerings, schedules and availability;
* browse, search, filter, sort and paginate destinations and activities;
* view destination/activity details and current availability;
* discover nearby coastal experiences;
* use the selected map API capability for location-aware discovery where
  included in the approved feature scope;
* save/remove favourites and inspect biodiversity context where available.

## 12.4 Location and Interaction

Flutter must support device-location discovery as the required v1 device
capability. React must support the same location-aware discovery through a
suitable location input. Manual alternatives should be available where
practical when location permission is denied. Layout and controls may vary
without reducing any permitted business action on either client.

Use a one-time, user-initiated location reading for nearby discovery; do not
add background tracking. React may offer browser geolocation as a convenience,
but must retain manual destination/region entry. The Member 1 work-area
contract defines permission, timeout, privacy and fallback behavior. For a
direct Member 2 marine-condition query, both clients must let the user select
a supported forecast/observation time or interval and submit its agreed
semantics to the API; planner-originated assessments preserve Member 3's
validated itinerary period. Member 3 captures its already-scoped itinerary
date/time inputs with appropriate web and native picker controls. Member 4
may accept optional image evidence on a managed operational assessment; this
does not include generic uploads or environmental-incident reporting. See the
[v1 device-capability
contract](docs/v1/device-capabilities.md).

Member 1 owns BLUEVERSE's consumer integration for the selected map API as
part of destination/activity discovery. The provider and the exact features
required (such as map display, place lookup, geocoding or directions) must be
selected and documented before implementation; this requirement does not
silently require every example feature. React and Flutter must access provider
capabilities through ASP.NET Core under the repository's public-API-only
client boundary. Provider credentials remain server-side. Returned map/place
data is untrusted discovery/display context and must not automatically create
or overwrite canonical BLUEVERSE destination records. Respect provider terms,
attribution, quotas, rate limits and location-data minimization. If the
provider is unavailable, manual/list discovery remains usable. The map does
not decide publication, schedule/availability, environmental suitability or
operational restrictions.

---

## 12.5 Business-Specific Workflow

Experience publication/availability must involve business logic beyond CRUD.

A possible lifecycle is:

```text
DRAFT
  ↓
PUBLISHED
  ↓
ARCHIVED
```

The final state model may be refined during technical design.

Experiences that are not valid or operationally available must not be presented as currently recommendable.

---

## 12.6 API Requirement

Component A must provide at least four meaningful public API endpoints and at least one operation beyond ordinary CRUD.

Representative resources include:

```text
/api/destinations
/api/activities
/api/activity-offerings
/api/favourites
/api/biodiversity/...
```

Publication, availability evaluation or another equivalent domain operation satisfies the required non-CRUD business behaviour.

Where the selected map features require backend operations (for example
provider-backed place lookup or geocoding), expose them only through the
public ASP.NET Core API. Define their exact routes and DTOs during
implementation; document only implemented routes in the endpoint catalog.

Exact routes and DTOs belong to implementation contracts.

---

# 13. Component A Agentic AI Contribution

Detailed agent contract:
[Coastal Experience & Biodiversity Agent](docs/v1/agents/member-1-coastal-experience-biodiversity-agent.md).

## Coastal Experience & Biodiversity Agent

### Responsibility

Provide structured coastal-experience context required by recommendation and operational workflows, enriched by biodiversity intelligence where relevant.

### Typical Inputs

* destination;
* activity;
* requested date/time;
* available offerings;
* tourist/operator constraints;
* biodiversity query where relevant.

### Controlled Tools

Examples:

```text
destination_lookup
activity_lookup
offering_lookup
schedule_lookup
biodiversity_prediction_lookup
```

For this post-G07 tool, the Member 1 agent requests biodiversity context
through the approved Member 3 public/typed backend contract. Member 3 owns
the private IT3091 adapter as ordinary v1 component integration; neither the
agent nor its tool calls the IT3091 host directly. The prediction result must
remain sourced optional context with explicit provenance, uncertainty and
unavailable/invalid states, not safety authority. See
[ADR-0019](docs/adr/ADR-0019-biodiversity-inference-integration-ownership.md).

### Structured Output

The agent produces an **Experience & Biodiversity Context Report** containing relevant fields such as:

* destination availability;
* activity availability;
* schedule context;
* experience constraints;
* biodiversity context where relevant;
* prediction availability;
* prediction metadata/uncertainty;
* missing-data indicators.

Biodiversity is contextual intelligence and must not be treated as a safety-critical signal unless a deterministic business rule explicitly establishes otherwise.

The agent must not directly publish, suspend or modify protected business records.

---

# 14. Component B — Marine Conditions & Safety Intelligence

Detailed member contract:
[Marine Conditions & Safety Intelligence](docs/v1/components/member-2-marine-conditions-safety-intelligence.md).

## 14.1 Purpose

Component B retrieves and transforms relevant weather and marine information into structured coastal decision-support information.

The application must clearly separate:

```text
Environmental Forecast / Observation
              ↓
Deterministic Suitability Assessment
              ↓
AI Interpretation / Recommendation
```

The LLM must not invent physical safety thresholds.

---

## 14.2 Third-Party Integration

BLUEVERSE v1 uses a weather/marine data provider as its primary required third-party integration.

The selected v1 provider is:

**Open-Meteo Weather and Marine APIs**

External access occurs through backend-controlled services.

Only variables relevant to actual BLUEVERSE workflows should be consumed.

Relevant information may include:

* weather condition;
* wind;
* rainfall;
* wave height;
* wave direction;
* wave period;
* swell information;
* sea-surface temperature;
* ocean-current information where appropriate.

---

## 14.3 Data Quality Requirements

Environmental data displayed or consumed by BLUEVERSE must retain enough information to communicate:

* source;
* requested location;
* forecast/observation time;
* retrieval time;
* freshness where relevant;
* unavailable fields.

Stale or unavailable information must not silently appear as current information.

Marine/weather data is decision-support information and must not be presented as a replacement for professional maritime-navigation information.

---

## 14.4 Safety Profiles

Different coastal activities may use different configurable deterministic rules.

Examples:

```text
Surfing Safety Profile
Snorkeling Safety Profile
Diving Safety Profile
Boat Activity Safety Profile
```

A safety profile may contain domain-relevant parameters.

Thresholds must originate from configured application rules or defensible criteria.

The LLM must not invent thresholds during runtime.

---

## 14.5 React and Flutter Requirements

Both clients must support the complete authorized Component B workflow:

* look up current and forecast marine conditions by destination and activity;
* accept the requested supported forecast or observation time/interval as an
  ordinary query input; when evaluating a planner candidate, preserve the
  validated itinerary period supplied by Member 3;
* show source, timestamps, freshness, unavailable fields and relevant warnings;
* show activity-specific suitability, caution and condition summaries;
* navigate between destination/activity details and marine information;
* inspect condition-assessment history;
* search, filter, sort and paginate where appropriate;
* manage safety profiles where permitted.

## 14.6 Presentation

Each client may present condition information to fit its screen and input
method. Both must preserve the same evidence, permission-gated actions and
server-calculated suitability result. The requested period is a normal
business input in the condition query; Member 2 has no separately assigned
device feature. The API defines the supported period horizon, granularity,
time-zone interpretation and provider coverage. Clients must report
unsupported periods and must not silently clamp, shift or replace a requested
period.

---

## 14.7 Deterministic Suitability

The backend must be able to derive an activity-specific suitability result using deterministic rules.

Representative states:

```text
SUITABLE
CAUTION
UNSUITABLE
UNKNOWN
```

Final names may be refined during implementation.

The essential requirement is:

**The suitability classification cannot depend solely on an LLM.**

AI may explain the result but cannot override it.

---

## 14.8 Business-Specific Operation

Component B's primary non-CRUD operation is:

```text
Activity / Marine Condition Suitability Assessment
```

It evaluates a specified activity, location and relevant time using configured deterministic rules.

---

# 15. Component B Agentic AI Contribution

Detailed agent contract:
[Marine Conditions Intelligence Agent](docs/v1/agents/member-2-marine-conditions-intelligence-agent.md).

## Marine Conditions Intelligence Agent

### Responsibility

Collect and interpret relevant marine/weather context required by a workflow.

### Controlled Tools

Examples:

```text
weather_forecast_lookup
marine_forecast_lookup
condition_snapshot_lookup
```

### Structured Output

The agent produces a **Marine Conditions Report** containing information such as:

* location;
* requested period;
* relevant weather factors;
* relevant marine factors;
* source timestamps;
* freshness;
* unavailable information;
* factors relevant to the supplied objective.

The agent does not independently authorize whether an operation may proceed.

---

# 16. Component C — Smart Coastal Planner & Itinerary Management

Detailed member contract:
[Smart Coastal Planner & Itinerary Management](docs/v1/components/member-3-smart-coastal-planner-itinerary-management.md).

## 16.1 Purpose

Component C creates personalized coastal recommendations and itineraries.

It is explicitly a **coastal planner**, not a generic travel planner.

---

## 16.2 Recommendation Inputs

Relevant constraints may include:

* destination or coastal region;
* date/time;
* available duration;
* preferred coastal activities;
* interests;
* experience level where relevant;
* optional planning constraints.

Only data useful to the planning objective should be collected.

---

## 16.3 Recommendation Constraints

Recommendations may use only:

* valid destinations;
* currently usable/published activities;
* available offerings;
* relevant schedules;
* permissible operational states.

If required marine-condition information is unavailable, BLUEVERSE must expose uncertainty rather than invent conditions.

If deterministic rules classify an activity as unsuitable for the requested circumstances, the final accepted recommendation must not reintroduce that activity for that period.

---

## 16.4 React and Flutter Requirements

Both clients must support the complete authorized Component C workflow:

* enter preferences and create recommendation requests;
* request and inspect smart coastal recommendations and relevant marine/safety
  information;
* create, add/remove/reorder/update, save and revisit itinerary items;
* view itinerary history and re-evaluate an itinerary where required;
* inspect and search/filter recommendation requests, results, related Agentic
  AI workflows and failures where permitted;
* view useful aggregate planning information where permitted.

Date/time preferences and itinerary schedule edits must use accessible,
platform-appropriate date/time selection controls. Both clients submit the
same semantic date, local time, duration and time-zone context for server
validation; the [device-capability contract](docs/v1/device-capabilities.md)
defines the interaction and unresolved time-zone decisions.

## 16.5 Presentation and Inspection

Both clients may adapt the itinerary editor and monitoring views to their
screen and input method. Authorized tourist and operational actions remain
equally complete on React and Flutter.

---

## 16.6 Business-Specific Operation

The component must support:

```text
Itinerary Re-evaluation
```

A stored itinerary can be re-evaluated when relevant:

* marine conditions;
* activity availability; or
* operational states

have changed.

---

# 17. Component C Agentic AI Contribution

Detailed agent contract:
[Planning & Coordination Agent](docs/v1/agents/member-3-planning-coordination-agent.md).

## Planning & Coordination Agent

### Responsibility

This is the primary Agentic AI workflow coordinator.

It receives a domain objective and creates a structured multi-step plan.

### Core Responsibilities

The agent must:

1. interpret the supplied objective;
2. identify required information;
3. construct a structured plan;
4. delegate work to appropriate specialized agents;
5. respect dependencies;
6. track expected outputs;
7. assemble validated information into the workflow result.

### Structured Output

The plan must identify:

* workflow objective;
* planned steps;
* assigned agent for each relevant step;
* dependencies;
* required tools;
* expected structured outputs.

The Planning Agent:

* cannot grant itself tools;
* cannot bypass authorization;
* cannot bypass deterministic validation;
* cannot bypass required human approval;
* cannot directly perform protected operational state changes.

---

# 18. Component D — Coastal Operations, Advisories & Alerts

Detailed member contract:
[Coastal Operations, Advisories & Alerts](docs/v1/components/member-4-coastal-operations-advisories-alerts.md).

## 18.1 Purpose

Component D manages coastal operational assessments and provides the primary v1 Human-in-the-Loop workflow.

It connects:

* field/operator requests;
* marine intelligence;
* experience context;
* Agentic AI;
* deterministic validation;
* authorized human approval;
* operational state;
* advisories/alerts.

---

## 18.2 Core Business Concepts

The component must represent:

* operational assessment;
* optional image evidence submitted by an authorized operator and attached
  to a specific assessment/version;
* AI operational recommendation/proposal;
* operational status;
* operational history;
* approval decision;
* alert/advisory.

Possible operational states include:

```text
OPEN
CAUTION
TEMPORARILY_SUSPENDED
CANCELLED
COMPLETED
```

The final allowed state-transition model must be documented and validated.

---

## 18.3 React and Flutter Requirements

For authorized operators and reviewers, both clients must support:

* select an activity/offering, inspect conditions and submit an assessment
  objective with relevant context, optionally attaching image evidence;
* inspect the assessment queue, details, workflow plan, progress, agent and
  tool-call summaries, recommendation, validation result and proposed action;
* approve, reject or request revision through permission-gated controls;
* inspect approval status, resulting operational state and active alerts;
* manage permitted advisories/alerts and inspect operational history;
* search, filter and paginate assessment information where appropriate.

Flutter may capture a photo or select an image; React supports image-file
selection/upload and may offer direct camera capture when available. Both
clients upload and display the same authorized evidence through Member 4's
public API. The [device-capability contract](docs/v1/device-capabilities.md)
and [ADR-0018](docs/adr/ADR-0018-assessment-evidence-storage-boundary.md)
define the private-storage, integrity, reviewer-access, versioning and
Agentic-AI boundaries. Uploads do not make Member 4 an owner of pollution or
environmental-incident workflows.

## 18.4 Presentation and Interaction

Both clients may adapt queue, evidence and approval layouts to their screen
and input method. Neither client has priority for initiating, reviewing or
tracking operational assessments.

---

## 18.5 High-Impact Actions

A recommendation is considered high impact when it proposes an operational action such as:

* temporarily suspending a BLUEVERSE-managed coastal activity/offering;
* cancelling a BLUEVERSE-managed activity session;
* changing an offering into another restrictive operational state;
* publishing a high-severity BLUEVERSE operational alert.

Agentic AI may **recommend** these actions.

Agentic AI may **not execute them autonomously**.

Required pattern:

```text
AI Recommendation
        ↓
Deterministic Validation
        ↓
PENDING HUMAN APPROVAL
        ↓
Approve / Reject / Request Revision
        ├──► Reject / Request Revision ──► Decision + Audit
        │
        └──► Eligible Approval
                    ↓
            ASP.NET Core Revalidation
                    ↓
            Transactional State Change
                    ↓
                 Audit History
```

BLUEVERSE v1 does not claim governmental or legal authority to:

* close public beaches;
* issue official emergency orders;
* control public maritime navigation.

Its operational actions apply only to operations represented and managed within BLUEVERSE.

---

## 18.6 Business-Specific Operation

Component D's key non-CRUD operations include:

```text
APPROVE
REJECT
REQUEST REVISION
```

of an Agentic AI operational proposal, followed by controlled execution where applicable.

---

# 19. Component D Agentic AI Contribution

Detailed agent contract:
[Safety & Operations Agent](docs/v1/agents/member-4-safety-operations-agent.md).

## Safety & Operations Agent

### Responsibility

Evaluate structured workflow context and produce a structured operational recommendation.

### Typical Inputs

* workflow objective;
* destination/activity context;
* marine-conditions report;
* experience/biodiversity context;
* current operational state;
* configured safety rules;
* active relevant alerts/restrictions.

### Controlled Tools

Examples:

```text
safety_profile_lookup
operational_status_lookup
active_alert_lookup
operational_constraint_lookup
```

### Structured Output

The agent produces a **Safety & Operations Recommendation** containing:

* assessed factors;
* recommendation;
* proposed operational action where relevant;
* affected object;
* proposed alert/advisory where relevant;
* unresolved uncertainty;
* whether human approval is required.

The agent must not execute a protected high-impact action itself.

---

# 20. Final Agentic AI Architecture

The [Agentic AI architecture guide](docs/agentic-ai/architecture.md) and
[v1 workflow guide](docs/v1/workflows.md) expand this model into controlled
tools, persisted state, validation, approval and recovery contracts.

BLUEVERSE v1 contains four distinct Agentic AI responsibilities:

```text
Planning & Coordination Agent
              │
              ├───────────────┐
              │               │
              ▼               ▼
Marine Conditions       Coastal Experience &
Intelligence Agent      Biodiversity Agent
              │               │
              └───────┬───────┘
                      ▼
             Safety & Operations
                   Agent
                      │
                      ▼
          Deterministic Validation
                      │
             ┌────────┴────────┐
             │                 │
             ▼                 ▼
       Normal Result      Approval Required
                               │
                               ▼
                       Authorized Human
```

A distinct agent must possess:

* identifiable responsibility;
* defined input contract;
* defined structured output contract;
* controlled tool permissions;
* visible participation in the workflow.

Renaming or cloning the same prompt does not constitute distinct agents.

---

# 21. Deterministic Validation Layer

Deterministic validation is **not an LLM agent**.

It is application/business logic.

It validates relevant concerns such as:

* output schema;
* required fields;
* safety-profile rules;
* business constraints;
* data freshness;
* operational state;
* allowed state transitions;
* authorization;
* approval requirements.

Conceptual validator outcomes may include:

```text
VALID
REQUIRES_APPROVAL
REQUIRES_REVISION
BLOCKED
SAFE_FAILURE
```

Exact implementation names may change.

An Agentic AI output cannot override a deterministic `BLOCKED` result.

---

# 22. Canonical Assessed Agentic Workflow

The primary assessed workflow is:

# Coastal Activity Operational Assessment

The assessed demonstration starts in Flutter and uses React for review. This
does not limit either role to one client: authorized operators can initiate and
track assessments in React or Flutter, and authorized reviewers can make the
same decisions in React or Flutter.

This workflow is deliberately designed to demonstrate:

* all four business components;
* all four Agentic AI contributions;
* Flutter;
* ASP.NET Core;
* PostgreSQL;
* third-party data;
* deterministic validation;
* React;
* human approval;
* status return.

---

## Step 1 — Assessed Flutter Initiation

A Coastal Operator selects:

* destination;
* activity/offering;
* relevant date/time.

The operator submits an objective such as:

```text
Assess the selected snorkeling operation for the requested
period and recommend whether the operation should continue,
continue with caution, or require a restrictive operational action.
```

---

## Step 2 — ASP.NET Core

ASP.NET Core:

* authenticates the user;
* checks the required permission;
* validates the request;
* creates the workflow record;
* stores the objective;
* assigns the workflow ID;
* initiates the internal Agentic AI workflow.

---

## Step 3 — Planning & Coordination Agent

The agent creates a structured plan.

Representative plan:

```text
1. Obtain destination/activity information.
2. Obtain relevant marine/weather conditions.
3. Obtain availability and biodiversity context.
4. Perform operational/safety analysis.
5. Create a structured recommendation.
6. Apply deterministic validation.
7. Request human approval if required.
```

The plan is persisted as workflow state.

---

## Step 4 — Marine Conditions Intelligence Agent

The agent retrieves relevant environmental information using allowlisted tools.

Tool inputs and outputs are validated.

External-service failures must be recorded.

---

## Step 5 — Coastal Experience & Biodiversity Agent

The agent retrieves relevant:

* destination;
* activity;
* offering;
* schedule/availability;
* operationally relevant experience information;
* biodiversity context where available.

Biodiversity intelligence acts as contextual enrichment and is not automatically interpreted as safety evidence.

If biodiversity prediction is unavailable, the workflow must record the unavailable state rather than fabricate a result.

---

## Step 6 — Safety & Operations Agent

The agent consumes the structured results and creates an operational recommendation.

Representative proposed outcomes include:

```text
CONTINUE
CONTINUE_WITH_CAUTION
TEMPORARILY_SUSPEND
CANCEL_SESSION
INSUFFICIENT_DATA
```

Final enumeration names may change during implementation.

---

## Step 7 — Deterministic Validation

Application code validates the recommendation against:

* schema;
* business rules;
* safety configuration;
* condition freshness;
* operational state;
* state-transition rules;
* approval requirements.

Unsupported or unsafe proposals must be:

* blocked; or
* returned for revision.

---

## Step 8 — Assessed React Human Approval

If the proposal contains a high-impact action:

```text
Workflow Status = PENDING_APPROVAL
```

The workflow pauses.

In the assessed demonstration, an authorized reviewer uses React to inspect:

* objective;
* structured workflow plan;
* agent execution summary;
* relevant condition information;
* proposed action;
* deterministic validation result.

The reviewer may choose:

```text
APPROVE
REJECT
REQUEST_REVISION
```

Unauthorized users must not be able to make the approval decision.

---

## Step 9 — Business Execution

If an eligible proposal is approved, ASP.NET Core executes the permitted business action.

Changes must:

* use server-side business rules;
* use transactions where necessary;
* create audit/history information.

The LLM never directly changes the protected operational state.

---

## Step 10 — Flutter Status Return

The initiating operator retrieves the updated workflow/result from the shared ASP.NET Core API. Both clients can display the same authoritative status.

The final assessed flow is:

```text
Flutter
  ↓
ASP.NET Core
  ↓
PostgreSQL
  ↓
Agentic AI
  ↓
Controlled Tools
  ↓
Deterministic Validation
  ↓
React Human Approval
  ↓
ASP.NET Core Execution
  ↓
PostgreSQL
  ↓
Flutter Updated Status
```

---

# 23. Tourist Recommendation Workflow

The second major Agentic AI use case is lower impact.

```text
React or Flutter Tourist
      ↓
Coastal Planning Objective
      ↓
Planning & Coordination Agent
      ↓
 ┌────┴─────────────────┐
 ▼                      ▼
Marine Conditions   Experience &
Agent               Biodiversity Agent
 └─────────┬────────────┘
           ↓
Deterministic Constraints
           ↓
Coastal Recommendation
           ↓
Itinerary
```

Normal tourist recommendations do not require staff approval.

Deterministic safety and business constraints still apply.

---

# 24. Agentic AI Workflow State

Persist only structured state required to operate and audit the workflow.

Required information includes where relevant:

* workflow ID;
* workflow type;
* initiator;
* objective;
* structured plan;
* workflow status;
* completed/current steps;
* structured agent outputs;
* tool results or auditable tool summaries;
* validation results;
* errors;
* retries;
* approval status;
* approval decision;
* final result;
* timestamps.

Do not persist:

* hidden model reasoning;
* chain-of-thought;
* API secrets;
* passwords;
* access tokens;
* unnecessary sensitive information.

---

# 25. Agent Tool Controls

Every Agentic AI tool must be explicitly allowlisted.

Each tool must define:

* purpose;
* authorized agent(s);
* typed/validated input;
* structured output;
* timeout behaviour;
* failure handling;
* auditable execution information.

Agents must not:

* create arbitrary tools at runtime;
* perform unrestricted network access;
* request unnecessary secrets;
* bypass server authorization;
* directly execute protected high-impact actions.

---

# 26. Agentic AI Security

The Agentic AI subsystem must address:

* objective/input validation;
* tool-input validation;
* structured-output validation;
* prompt-injection resistance;
* least-privilege tool permissions;
* secret protection;
* timeouts;
* bounded retry limits;
* authorization;
* deterministic validation;
* safe failure.

Untrusted user, tool or external-service content must not be treated as authorization to:

* change system instructions;
* grant additional tools;
* reveal secrets;
* bypass validation;
* bypass approval;
* perform unauthorized mutations.

---

# 27. Failure Recovery and Safe Failure

The workflow must safely handle relevant failures such as:

* AI model timeout;
* malformed structured output;
* tool timeout;
* external API timeout;
* external API invalid response;
* missing marine information;
* stale marine information;
* unavailable biodiversity ML service;
* invalid tool input;
* deterministic validation rejection;
* approval rejection;
* retry exhaustion.

Where bounded retry is appropriate:

```text
Failure
  ↓
Bounded Retry
  ↓
Revalidation
```

If recovery is not possible:

```text
SAFE_FAILURE
```

must be recorded.

No unsafe side effect may occur after a safe failure.

---

# 28. Marine Biodiversity Intelligence

Marine Biodiversity Intelligence is an official BLUEVERSE capability.

The separate IT3091 ML workstream provides the initial predictive model.

Conceptually:

```text
OBIS Species Occurrences
          +
Bio-ORACLE Environmental Data
          ↓
      ML Pipeline
          ↓
Species Occurrence Probability
          ↓
    Habitat Suitability
          ↓
Spatial Biodiversity Intelligence
          ↓
       BLUEVERSE
```

The ML capability and Agentic AI capability are distinct:

```text
Machine Learning
→ predictive biodiversity intelligence

Agentic AI
→ planning, orchestration and decision-support workflows
```

Neither replaces the other.

---

# 29. v1 Biodiversity Integration

The BLUEVERSE project is responsible for integrating the separately developed IT3091 model into the BLUEVERSE architecture.

The ML inference service remains internal.

Required communication pattern:

```text
React / Flutter
      ↓
ASP.NET Core
      ↓
Internal ML Inference
      ↓
Structured Prediction
      ↓
ASP.NET Core
```

Member 3 owns BLUEVERSE's ASP.NET Core consumer adapter and validated public
prediction-result contract. Member 1 owns the destination/activity-facing
experience and consumes Member 3's public contract; its clients never call
the private IT3091 service. The ML adapter is ordinary v1 backend integration
on `features/coastal-planner`, and must be implemented before G07. It does not
implement Agentic AI. The future Member 1 biodiversity agent may invoke the
result only through an approved allowlisted backend tool after G07. See
[ADR-0019](docs/adr/ADR-0019-biodiversity-inference-integration-ownership.md).

The model integration should expose useful information such as:

* focal species;
* queried location;
* occurrence probability;
* habitat-suitability interpretation;
* model version;
* prediction timestamp;
* relevant uncertainty or limitations.

An occurrence probability must never be presented as guaranteed species presence.

The v1 integration must be capable of returning a genuine model prediction when the ML service and trained model are available.

---

# 30. ML Safe Degradation

Biodiversity ML is informative rather than safety-critical.

Temporary inference/service failure must produce an explicit unavailable state such as:

```text
Biodiversity Intelligence = UNAVAILABLE
```

The application must not fabricate a prediction.

Other workflows may continue only where they can operate safely without biodiversity prediction.

Safe degradation does not replace the requirement to implement the actual v1 ML integration path.

---

# 31. Third-Party Weather/Marine Integration

BLUEVERSE v1 integrates Open-Meteo Weather/Marine APIs for relevant coastal context.

This weather/marine responsibility belongs to Member 2. Member 1's separate
map API integration for destination/activity discovery is specified in
§12.4; the two providers and their data contracts must not be conflated.

External-service access must be mediated through backend services.

Implementation must handle:

* timeout;
* unavailable service;
* invalid response;
* unavailable variables;
* service/rate limitations;
* source timestamp;
* freshness;
* safe fallback.

Only information serving an actual BLUEVERSE business requirement should be requested, processed or stored.

---

# 32. Database Requirements

PostgreSQL must use normalized relational design appropriate to the four components.
The v0 Auth schema and session lifecycle are the identity foundation for
these v1 domains; [the database reference](docs/database/README.md) describes
the implemented Auth model and the extension contract. Each new domain model
must use an owning backend service and EF Core migrations. Neither client nor
an agent tool directly reads or writes PostgreSQL.

The final schema must demonstrate:

* primary keys;
* foreign keys;
* appropriate relationships;
* constraints;
* indexes;
* appropriate PostgreSQL data types;
* EF Core migrations;
* suitable seed data;
* audit fields;
* transactions where required.

Major relational domains include:

```text
Identity & Authorization

Coastal Experiences

Marine Conditions & Safety

Recommendations & Itineraries

Coastal Operations & Alerts

Agentic AI Workflow State
```

Exact tables, keys and entity names belong to the database design rather than this requirements baseline.

---

# 33. API Requirements

Every v1 student-owned business component must provide:

* at least four meaningful API endpoints; and
* at least one business-specific operation beyond simple CRUD.

Across BLUEVERSE, API functionality must demonstrate where appropriate:

* CRUD;
* search;
* filtering;
* sorting;
* pagination;
* status workflows;
* history;
* reporting/analytics;
* business-specific actions.

Public APIs must use appropriate:

* HTTP routes;
* methods;
* status codes;
* request/response models;
* asynchronous operations.

The [endpoint catalog](docs/api/endpoint-catalog.md) is the current public,
gateway, frontend and internal route inventory. Proposed example paths in
this document are not live endpoints until implemented and added to the
catalog. New public operations must appear under `/api/...`, without a
path-version segment, and preserve the sole public ASP.NET Core boundary.

---

# 34. Backend Quality Requirements

The ASP.NET Core backend must demonstrate:

* controllers/endpoints;
* DTOs;
* service/application layer;
* suitable data-access abstractions;
* dependency injection;
* asynchronous operations;
* EF Core;
* server-side validation;
* global error handling;
* structured logging;
* CORS;
* Swagger/OpenAPI;
* secure configuration;
* auditing.

The public API and internal Auth foundation establish the service boundary.
New domain or AI services must be reachable by clients only through the
public API and must add owning tests, health/operational wiring and route
documentation with their implementation.

---

# 35. Authentication and Security

The final system must demonstrate:

* secure registration/login;
* password hashing;
* JWT authentication;
* token validation/expiry;
* protected APIs;
* RBAC with permission-based enforcement;
* server-side authorization;
* server-side validation;
* secure Flutter token storage;
* protected React routes/navigation;
* secrets outside Git;
* environment-based configuration;
* restricted internal-service access.

Security must not rely only on hiding UI controls.
The shared v0 session design uses protected browser cookies and native
Flutter secure storage with bearer access tokens. Auth owns installation
proofs, token rotation, absolute session expiry, revocation and ended-session
history as specified in [section 3.6](#36-auth-identity-and-access-control).
Both clients must honor the same server-authorized operations; all protected
API endpoints enforce their required permissions independently of navigation.

---

# 36. React Quality Requirements

React must use:

* functional components;
* React Hooks;
* React Router;
* justified state management;
* reusable components;
* protected routes/navigation;
* ASP.NET Core API integration.

The v0 browser Auth/session adapter is the base for v1 domain screens. All
authorized management, tourism, operator and review actions must remain
available through React, using the same server-owned outcomes as Flutter.

Relevant interfaces must support:

* validation;
* loading states;
* empty states;
* success feedback;
* error states;
* responsive layouts;
* accessibility considerations.

---

# 37. Flutter Quality Requirements

Flutter must use:

* reusable widgets;
* routing/navigation;
* justified state management;
* the shared ASP.NET Core APIs;
* secure token storage;
* protected screens;
* form validation;
* relevant status/history UI.

The v0 native Auth/session repository and secure credential boundary are the
base for v1 domain screens. All authorized management, tourism, operator and
review actions must remain available through Flutter, using the same
server-owned outcomes as React.

Relevant screens must support:

* loading states;
* empty states;
* error states;
* responsive layouts.

GPS/location is the primary meaningful mobile-device feature for v1.

---

# 38. Reporting and Analytics

BLUEVERSE must contain meaningful reporting or analytics rather than only CRUD screens.

Both React and Flutter must contain one or more real data-driven views relevant to project operations, for example:

* destination/activity counts;
* operational assessment status;
* recommendation activity;
* marine-condition assessment history;
* alert/advisory statistics;
* Agentic AI workflow status or outcome summaries.

Analytics must be based on actual application data.

---

# 39. Automated Testing

The [test strategy](docs/testing/strategy.md) and
[acceptance matrix](docs/testing/test-matrix.md) distinguish the checked-in
foundation evidence from v1 target cases. Requirement-based tests must verify
responses, authorization, persisted state and side effects, including
applicable invalid, boundary, malformed, denied, dependency-failure and
concurrency paths.

## 39.1 Backend

Required evidence includes:

* unit/service tests;
* validation tests;
* authentication tests;
* authorization/permission tests;
* controller/API tests;
* business-rule tests;
* state-transition tests.

## 39.2 PostgreSQL

Required evidence includes:

* PostgreSQL integration tests;
* constraints;
* migrations;
* relationships/data integrity;
* transaction behaviour where applicable.

## 39.3 React

Required evidence includes:

* component tests;
* form validation;
* protected routes;
* API integration;
* error states;
* approval workflow where relevant.

## 39.4 Flutter

Required evidence includes:

* unit tests;
* widget tests;
* form validation;
* navigation;
* API integration;
* workflow-status behaviour;
* device-permission behaviour where appropriate.

---

# 40. Agentic AI Evaluation

At least one complete golden-case workflow must verify:

* domain objective;
* structured planning;
* correct delegation;
* distinct agent participation;
* controlled tool selection;
* validated tool input;
* structured outputs;
* persisted state;
* deterministic validation;
* business-rule compliance;
* human-approval enforcement;
* auditable final outcome.

Additional evaluation must address:

* prompt injection;
* malformed structured output;
* unavailable tool/service;
* invalid tool input;
* failure recovery;
* approval rejection;
* request revision;
* retry exhaustion;
* safe failure.

LLM-as-a-judge may be used only as supporting evidence.

Evaluation must also contain deterministic:

* assertions;
* schema validation;
* business-rule validation; and
* human review where appropriate.

---

# 41. End-to-End Integration Requirement

At least one complete assessed workflow must demonstrate:

```text
Flutter
  ↓
ASP.NET Core
  ↓
PostgreSQL
  ↓
Agentic AI
  ↓
React Review / Approval
  ↓
ASP.NET Core
  ↓
PostgreSQL
  ↓
Flutter Updated Status
```

The **Coastal Activity Operational Assessment** workflow is the canonical workflow used to satisfy this requirement.
This is the assessed demonstration sequence. An authorized operator can
initiate and track the same workflow in either client, and an authorized
reviewer can inspect and decide it in either client. The clients share
workflow state through the public API and never call one another.

---

# 42. Performance Requirements

The project must collect actual evidence for relevant performance areas including:

* concurrent requests;
* API response time;
* success/failure rate;
* database response;
* external-service latency;
* Agentic AI latency.

Reported performance figures must come from tests actually executed by the team.

---

# 43. Git and Collaborative Development

The repository must use Git/GitHub throughout the development lifecycle.

Required practices include:

* meaningful commits;
* feature/task branches;
* issues;
* pull requests;
* reviews;
* a GitHub project board and task tracking;
* clear ownership;
* merge/conflict evidence where relevant.

Each team member must maintain traceable technical contribution.

Artificial commit splitting, fabricated contribution evidence and final-day bulk contribution are not acceptable.

---

# 44. CI Requirements

At least one GitHub Actions workflow must:

1. restore backend dependencies;
2. build backend projects;
3. run automated backend tests.

This workflow must run on:

```text
Push → main
Pull Request → main
```

The v1 engineering target should additionally validate where practical:

* React build/tests;
* Flutter analysis;
* Flutter tests;
* repository validation;
* UI integration contracts;
* Agentic AI deterministic tests.

Secrets required by CI must use secure repository/environment configuration.

The repository's separated workflows also check route/catalog consistency,
client/API integration, agent resources and Docker behavior. A green CI
workflow is evidence for the checks it ran; it does not replace the complete
cross-client and operational acceptance path.

---

# 45. Documentation and ADRs

Project documentation must include:

* project overview;
* business problem;
* v0 technical components and their integration/acceptance contract;
* finalized v1 scope;
* roles/permissions;
* separate documentation for each of the four member-owned business
  components and each of their four Agentic AI contributions;
* system architecture;
* Agentic AI architecture;
* database design;
* ER diagram;
* API documentation;
* React design;
* Flutter design;
* ML integration;
* third-party integration;
* security;
* testing;
* Agentic AI evaluation;
* performance;
* deployment;
* individual contributions;
* individual AI-use logs and a consolidated group AI-use declaration.

The [v0 guide](docs/v0/README.md) maps the technical foundation and its
source evidence. The [v1 guide](docs/v1/README.md) maps each member component,
agent, cross-client contract and assessed workflow. The two guides expand
this baseline; they do not change the scope without a requirements amendment.

The implemented BLUEVERSE design deliverable is documented in
[`DESIGN.md`](DESIGN.md), with matching color and
type tokens in the React entry stylesheet and Flutter native theme. Both
clients share the same palette, typography hierarchy, imagery, component
character and accessibility expectations while using platform-appropriate
layouts and controls.

Development AI use is permitted with full, accurate disclosure. Each member's
log records the task, tool/model, output retained or changed or rejected, and
verification. The group declaration confirms that every member can explain,
test and modify work submitted under their name. Individual assessed
reflections are written by the students themselves.

Important architectural decisions must use ADRs.

At minimum, ADRs must address:

* React state management;
* Flutter state management;
* Agentic AI framework/orchestration;
* Agent workflow-state persistence;
* cloud/deployment architecture.

Additional ADRs may document decisions such as:

* permission-oriented RBAC;
* microservice boundaries;
* ML integration;
* third-party marine/weather integration.

---

# 46. Repository Engineering Requirements

Repository-level AI/agent instructions, rules, skills and validation remain under:

```text
.agents/
```

Root and `.agents/` guidance states universal project architecture, safety,
task routing and validation rules. It must not make a release document the
default for every task. Select the applicable requirements and owning
component documentation from the work being performed.

Imported guidance must:

* remain source-pinned where required;
* remain supplementary to BLUEVERSE rules;
* never override the project requirements.

Architectural changes require corresponding documentation updates.

UI/API integration metadata must remain synchronized with:

* React;
* Flutter;
* gateway;
* backend.

`docs/contracts/ui-integration.json` owns the shared user-workflow mapping.
`docs/api/endpoint-catalog.json` owns the checked route inventory and
generates `docs/api/endpoint-catalog.md`. Implementation source remains
authoritative for executable behavior. Route changes update the relevant
contract files and pass both validators before acceptance.

---

# 47. Deployment Requirements

The final v1 submission must provide:

* deployed React application;
* deployed ASP.NET Core application/API;
* working API health URL;
* working Swagger/OpenAPI URL;
* securely deployed PostgreSQL;
* complete Flutter source;
* runnable Android APK or approved equivalent;
* Agentic AI setup/runtime instructions;
* model/framework requirements;
* startup order;
* ML integration/setup instructions.

## 47.1 Submission Package

The nominated group leader makes one submission through Course Web by
**30 September 2026 at 11:50 PM**. Written work is combined into one
organized PDF named according to the official `SE3090_GroupNumber`
convention, with a Group Report section and a clearly labelled Individual
Report section for every member. The Group Report contains the project scope,
requirements and roles; full-stack and Agentic AI architecture; database and
ER diagram; API, React and Flutter design; technical, testing, Agentic AI
evaluation, performance and deployment reports; ADRs; security, diagrams,
references and the consolidated group AI-use declaration.

Each Individual Report section contains the member's contribution and owned
component, technical and Git/PR/test evidence, challenges and learning,
individual AI-use log, approximately one-page reflection written by that
student, and signed declaration. The reflection must describe tools used,
useful and incorrect AI output, what the student changed or rejected, and
their own learning; it must not be AI-generated.

The submission also includes the repository and deployed React/API links,
health and Swagger URLs, PostgreSQL deployment evidence, Agentic AI setup or
access instructions, required environment-variable names and startup order,
the runnable Flutter APK or approved equivalent with installation guidance,
and a working ten-minute demonstration-video link viewable by anyone with
the link. Provide evaluator test-account instructions through an approved
secure channel rather than committing credentials.

## 47.2 Evaluator Access

Keep the repository, demonstration video and deployed services accessible
through at least **21 October 2026**. Before submission, the group leader
opens every submitted link in a private browser session and confirms that
evaluators can reach it without requesting access. The implementation must
remain reproducible using institution-provided or no-cost services.

Deployment preserves the v0 public API/Auth and database isolation: both
clients reach the approved public API, while Auth, Agentic AI, ML inference
and PostgreSQL remain private. The local Docker gateway is the development
entry point; hosted topology may use an equivalent public boundary.

The exact deployment provider is an architecture/deployment decision and may change according to service availability.

The implementation must not require paid services in order to satisfy the assignment.

---

# 48. Final Demonstration and Viva Readiness

The completed system must be demonstrable without relying on development-time AI assistants.
The final evaluation includes a ten-minute live demonstration followed by a
twenty-minute viva and technical-question session. Every member attends and
can be asked to explain, modify, test or debug their own work.

The team must be able to demonstrate:

* login using different roles;
* protected operations;
* CRUD;
* business-specific operations;
* PostgreSQL changes;
* Swagger/OpenAPI;
* React and Flutter using the same API;
* an authorized stakeholder completing the same permitted business action
  in either client;
* complete Agentic AI workflow;
* structured planning;
* delegation;
* four distinct agents;
* allowlisted tools;
* persisted state;
* deterministic validation;
* authorized human approval;
* execution history;
* error handling;
* safe failure;
* automated tests;
* passing CI;
* deployed applications;
* Git contribution history.

Every member must be able to explain, modify, test and debug their own contribution.

During the final demonstration and viva, external AI assistants, chatbots, IDE copilots and agentic coding tools must not be used to answer questions or modify the submitted system.

The BLUEVERSE Agentic AI subsystem itself must still be executed as part of the demonstration.

---

# 49. v1 Scope Exclusions

The following are **not required for v1**:

* hotel booking;
* airline booking;
* generic restaurant discovery;
* broad social networking;
* production payment processing;
* autonomous safety-critical decisions;
* professional marine navigation;
* official governmental beach closure;
* emergency-service dispatch;
* full environmental incident management;
* pollution-response coordination;
* environmental-authority workflows;
* fisheries management;
* fishing-industry workflows;
* coastal-livelihood management;
* maritime heritage management;
* future climate-scenario simulation.

These features must not displace required v1 functionality.

---

# 50. Future v2 — Environmental Resilience

Future scope includes:

* environmental authorities/organizations;
* pollution reporting;
* environmental incidents;
* environmental intelligence;
* restrictions;
* AI-assisted environmental decisions;
* human approval;
* response coordination.

v2 is not part of the current implementation target.

---

# 51. Future v3 — Fisheries & Coastal Livelihoods

Future scope includes:

* fisheries stakeholders;
* coastal communities;
* marine-resource information;
* stakeholder observations;
* fisheries/coastal workflows;
* coastal-livelihood intelligence.

v3 is not part of the current implementation target.

---

# 52. Versioning

BLUEVERSE uses:

```text
x.y.z
```

with project-specific phase semantics.

```text
x = major development phase
y = integration/feature release
z = bug-fix release
```

Major phases:

```text
0 → Foundation
1 → Coastal Tourism & Operations
2 → Environmental Resilience
3 → Fisheries & Coastal Livelihoods
```

The phase number determines which component and acceptance contract applies;
it does not assign stakeholder roles or business workflows to a frontend.
The requirements remain the scope baseline, while the v0 and v1 guides
provide phase-specific implementation detail.

Example:

```text
1.4.2
```

means:

* major phase v1;
* fourth integration release within v1;
* second bug-fix release for that integration state.

---

# 53. Final Team Ownership

Member numbers below identify v1 business-component and agent
responsibilities. They do not establish a person's GitHub identity. The
shared v0 technical foundation is a cross-team dependency, and contribution
evidence must use the confirmed team account mapping.

## Member 1

**Business Component**

Coastal Experience & Biodiversity Discovery

**Agentic AI Contribution**

Coastal Experience & Biodiversity Agent

**Primary ownership**

* destinations;
* coastal activities;
* activity offerings/availability;
* associated schedules;
* discovery;
* GPS-aware coastal discovery;
* the selected map API consumer integration for destination/activity
  discovery, through ASP.NET Core;
* favourites;
* the user-facing biodiversity context surface, consuming the validated
  Member 3 prediction contract. Member 1 does not own the BLUEVERSE adapter;
  the separate IT3091 workstream supplies the model and inference service.

---

## Member 2

**Business Component**

Marine Conditions & Safety Intelligence

**Agentic AI Contribution**

Marine Conditions Intelligence Agent

**Primary ownership**

* weather/marine integration;
* marine-condition information;
* condition snapshots;
* data freshness;
* configurable activity safety profiles;
* deterministic environmental suitability.

---

## Member 3

**Business Component**

Smart Coastal Planner & Itinerary Management

**Agentic AI Contribution**

Planning & Coordination Agent

**Primary ownership**

* recommendation requests;
* personalization constraints;
* Agentic AI planning/delegation;
* recommendation assembly;
* itineraries;
* itinerary re-evaluation; and
* BLUEVERSE's backend-mediated adapter and validated public result contract
  for the separate IT3091 biodiversity inference service. The IT3091
  workstream supplies the model and inference service.

---

## Member 4

**Business Component**

Coastal Operations, Advisories & Alerts

**Agentic AI Contribution**

Safety & Operations Agent

**Primary ownership**

* operational assessments;
* operational recommendations;
* operational state;
* human approval;
* advisories/alerts;
* execution history.

---

# 54. Final v1 Architecture Summary

```text
                         BLUEVERSE v1
                Coastal Tourism & Operations
                              │
          ┌───────────────────┼───────────────────┐
          │                   │                   │
          ▼                   ▼                   ▼
 Coastal Experience     Marine Conditions    Smart Coastal
 & Biodiversity         & Safety             Planning
          │                   │                   │
          └──────────┬────────┴────────┬──────────┘
                     │                 │
                     ▼                 ▼
                 Agentic AI       Biodiversity ML
                     │                 │
                     └────────┬────────┘
                              ▼
                      Decision Support
                              │
                              ▼
                  Deterministic Validation
                              │
                      ┌───────┴───────┐
                      │               │
                      ▼               ▼
                 Normal Flow     Human Approval
                                      │
                                      ▼
                             Coastal Operations
                                      │
                               Advisories/Alerts
                                      │
                            ┌─────────┴─────────┐
                            ▼                   ▼
                React               Flutter
              All roles             All roles
```

The external integration ownership is distinct: Member 1 consumes the
selected map API through ASP.NET Core; Member 2 owns Open-Meteo
weather/marine acquisition; and Member 3 consumes the private IT3091
biodiversity inference API through its backend adapter while that separate
workstream supplies the model and inference service. Member 1 owns the
user-facing biodiversity context and consumes Member 3's validated public
contract. These boundaries do not make map or ML services available in the
current foundation.

---

# 55. v1 Definition of Done

BLUEVERSE v1 is complete only when all applicable requirements below are satisfied.

The Foundation group covers v0; its detailed integration and verification
contract is in [section 3](#3-v0-foundation-baseline) and the
[v0 acceptance guide](docs/v0/integration-and-acceptance.md). The remaining
groups include the v1 member components, agents and full delivery evidence.

## Foundation

* [ ] repository conventions are complete;
* [ ] Docker infrastructure works;
* [ ] Docker Hardened Images are used where required;
* [ ] Compose networking works;
* [ ] edge Nginx works;
* [ ] React foundation works;
* [ ] Flutter foundation works;
* [ ] ASP.NET Core public API foundation works;
* [ ] Auth/Identity service foundation works;
* [ ] PostgreSQL works;
* [ ] EF Core migrations work;
* [ ] JWT authentication works;
* [ ] role-permission authorization works;
* [ ] browser cookies and native secure credentials follow the shared Auth
  session contract;
* [ ] refresh, expiry, revocation and active/ended session behavior are
  verified against their server-owned state;
* [ ] health endpoints work through the gateway;
* [ ] Swagger/OpenAPI works through the gateway;
* [ ] API/Postman verification works;
* [ ] CI foundation works;
* [ ] repository validators pass;
* [ ] route catalog and UI registry match the implemented client/API routes.

## Four Business Components

* [ ] all four components are implemented;
* [ ] every component contains meaningful relational data;
* [ ] every component has at least four meaningful API endpoints;
* [ ] every component contains a business operation beyond ordinary CRUD;
* [ ] every owner has identifiable backend contribution;
* [ ] every owner has identifiable database contribution;
* [ ] every owner has identifiable React contribution;
* [ ] every owner has identifiable Flutter contribution;
* [ ] every owner has tests;
* [ ] every owner has documentation;
* [ ] every owner has traceable Git/PR evidence.

## Agentic AI

* [ ] four distinct agents are implemented;
* [ ] each agent has an identifiable responsibility;
* [ ] each agent has defined input/output contracts;
* [ ] structured planning works;
* [ ] delegation works;
* [ ] allowlisted tools work;
* [ ] tool inputs are validated;
* [ ] structured outputs are validated;
* [ ] shared workflow state is persisted;
* [ ] deterministic validation works;
* [ ] required human approval works;
* [ ] approve works;
* [ ] reject works;
* [ ] request revision works;
* [ ] execution history is auditable;
* [ ] errors/retries are recorded;
* [ ] prompt-injection resistance is evaluated;
* [ ] safe failure works.

## Intelligence Integrations

* [ ] Open-Meteo weather/marine integration works;
* [ ] the selected Member 1 map API integration works through ASP.NET Core,
      with provider terms/attribution, server-side credentials, validated
      results, privacy-minimal location data and safe unavailable behavior;
* [ ] source/time/freshness information is handled correctly;
* [ ] ML inference integration path is implemented;
* [ ] a real biodiversity prediction can be consumed when the trained model service is available;
* [ ] temporary ML failure produces an explicit safe-unavailable state;
* [ ] Agentic AI and ML services remain private behind ASP.NET Core.

## Cross-Platform

* [ ] React and Flutter use the same application backend;
* [ ] React and Flutter use the same business data;
* [ ] React and Flutter use the same identity/authorization rules;
* [ ] React and Flutter each provide every permitted role and v1 workflow;
* [ ] platform-specific input or layout does not restrict a business capability;
* [ ] Flutter GPS/location functionality works;
* [ ] UI integration registry is synchronized;
* [ ] UI integration validation passes.

## Canonical E2E Workflow

The following must work reliably:

This is the assessed Flutter-to-React demonstration path. Equivalent
permission-gated initiation, review and status capabilities must also be
available in both clients.

```text
Flutter Coastal Operator
        ↓
Operational Assessment
        ↓
ASP.NET Core
        ↓
PostgreSQL
        ↓
Planning & Coordination Agent
        ↓
 ┌──────┴────────────────────┐
 ↓                           ↓
Marine Conditions       Coastal Experience &
Agent                   Biodiversity Agent
 └──────────┬────────────────┘
            ↓
Safety & Operations Agent
            ↓
Deterministic Validation
            ↓
React Operations Reviewer
            ↓
Approve / Reject / Request Revision
            ├──► Reject / Request Revision ──► Recorded Decision
            │
            └──► Eligible Approval
                        ↓
              ASP.NET Core Revalidation
                        ↓
              Permitted Business Execution
                        ↓
                 PostgreSQL + Audit
                        ↓
             Updated Status in Both Clients
```

## Quality

* [ ] backend tests pass;
* [ ] PostgreSQL integration tests pass;
* [ ] React tests pass;
* [ ] Flutter tests pass;
* [ ] Agentic AI golden case passes;
* [ ] failure/safe-failure tests pass;
* [ ] prompt-injection tests pass;
* [ ] CI passes;
* [ ] performance tests are executed;
* [ ] reporting/analytics are functional;
* [ ] no secrets are committed.

## Deployment

* [ ] React deployment works;
* [ ] ASP.NET Core deployment works;
* [ ] PostgreSQL deployment works;
* [ ] health URL works;
* [ ] Swagger URL works;
* [ ] Flutter APK runs;
* [ ] Agentic AI startup/runtime is documented;
* [ ] ML service integration/startup is documented;
* [ ] evaluator access instructions are complete.
* [ ] deployed services, repository and demonstration video remain accessible
  through at least 21 October 2026.

## Documentation

* [ ] README is complete;
* [ ] architecture documentation is complete;
* [ ] each v0 technical component and the shared integration path are documented;
* [ ] each v1 member component and agent has a separate, current contract;
* [ ] ER diagram is complete;
* [ ] API documentation is complete;
* [ ] ADRs are complete;
* [ ] testing evidence exists;
* [ ] Agentic AI evaluation evidence exists;
* [ ] performance evidence exists;
* [ ] deployment evidence exists;
* [ ] individual contributions are documented;
* [ ] individual AI usage is logged;
* [ ] group AI usage is declared;
* [ ] one consolidated PDF contains the Group Report and every labelled
  Individual Report section, with each member's own reflection and signed
  declaration;
* [ ] repository, live URLs, setup information and the accessible ten-minute
  demonstration-video link are included in the submission.

## Evaluation Readiness

* [ ] every member can explain their component;
* [ ] every member can explain their database contribution;
* [ ] every member can explain their React contribution;
* [ ] every member can explain their Flutter contribution;
* [ ] every member can explain their Agentic AI contribution;
* [ ] every member can explain relevant tests and CI;
* [ ] every member can trace the shared cross-platform workflow;
* [ ] the group leader verifies every submitted link in a private browser;
* [ ] the full Agentic AI workflow can run during the demonstration without development-time AI assistance.

When all requirements above are satisfied, the assessed implementation is considered:

```text
BLUEVERSE v1 — COMPLETE
```

---

# 56. Requirements Freeze

This document is the authoritative functional and architectural scope baseline
for BLUEVERSE v0 and v1.

From this point onward:

* component boundaries are considered finalized;
* the four Agentic AI responsibilities are considered finalized;
* the canonical assessed workflow is considered finalized;
* v1 stakeholder scope is considered finalized;
* v2 and v3 remain deferred;
* Marine Biodiversity Intelligence remains part of v1 integration;
* the team has amended the v1 scope to include a Member 1-owned map API
  integration for location-aware discovery, with provider and exact feature
  scope left for technical selection;
* implementation details may evolve through ADRs and technical contracts;
* implementation changes must not silently alter this requirements baseline.

Material changes to a frozen requirement should occur only when:

1. a requirement proves technically impossible;
2. the official assignment requirements change;
3. the lecturer provides contradictory guidance; or
4. the team formally agrees that a requirement must be replaced without reducing assignment compliance.

Editorial clarification, source mapping and cross-reference updates may
improve readability without silently changing the agreed scope. Record any
material amendment, the reason and its effects on the owning component and
acceptance documents.

## Recorded v1 scope amendment — 2026-09-26

The team added a map API integration to v1 at the user's direction. Member 1
owns the BLUEVERSE adapter and location-discovery contract because it owns
destinations and activity discovery. The formal assignment permits maps as a
meaningful third-party API, while repository rules require clients to use the
public ASP.NET Core API. The selected vendor and exact feature scope remain
open and are tracked in [ADR-0017](docs/adr/ADR-0017-map-provider-integration-boundary.md).

## Recorded device-capability and evidence amendment — 2026-09-26

The team confirmed Flutter GPS/device location as the required device
capability, with Member 1 owning nearby discovery and React retaining an
equivalent location-entry workflow. To place useful device interactions
inside the existing member scopes, Member 3 owns accessible date/time
selection for the planner's already-required time inputs; Member 4 owns
optional image capture/selection and private upload as evidence on
BLUEVERSE-managed operational assessments. These are implemented in the
owning members' single `features/**` branches, with no cross-member branch
sequence. Image evidence is not generic file sharing, is not sent to the
Agentic AI runtime, and does not add v2 environmental-incident or pollution
reporting. See [device capabilities](docs/v1/device-capabilities.md) and
[ADR-0018](docs/adr/ADR-0018-assessment-evidence-storage-boundary.md).

## Recorded Member 2 device-input allocation — 2026-09-26 (superseded)

This historical allocation was superseded by the later Member 2 capability
clarification below. It is retained to preserve the requirements amendment
history; it is not a current device-feature requirement.

The team assigned Member 2 an accessible date/time or interval selector for
direct marine-condition lookups and deterministic suitability assessments.
This is an input to Member 2's existing location/activity/period contract, not
a separate member branch or a new domain component. It is distinct from
Member 3's itinerary scheduling. When the planner supplies a candidate
period, Member 2 must use that validated period without prompting for or
substituting another one. React and Flutter must submit equivalent period
semantics; the Member 2 API contract must set provider-supported bounds,
interval granularity and time-zone/DST behavior. See the [Member 2 component
contract](docs/v1/components/member-2-marine-conditions-safety-intelligence.md),
[Member 2 work plan](docs/v1/phases/member-2-phase-plan.md) and [device
capability guide](docs/v1/device-capabilities.md).

## Recorded ML integration and Member 2 device-capability clarification — 2026-09-26

The team assigned BLUEVERSE's backend-mediated IT3091 biodiversity inference
consumer adapter and validated public prediction contract to Member 3 because
the prediction is optional context for planning and is not an operational
safety authority. The separate IT3091 workstream continues to supply the
model and inference service. Member 1 owns the destination/activity-facing
presentation and consumes Member 3's validated public contract; Member 4 does
not own this adapter. This ordinary ML integration is implemented as part of
the Member 3 feature before G07 and is distinct from Agentic AI, which remains
deferred to `agentic-ai/**` until all member components pass G07. The member
relationship and cross-layer contracts are in
[ADR-0019](docs/adr/ADR-0019-biodiversity-inference-integration-ownership.md).

The team also removed Member 2's separately assigned device interaction.
Member 2 continues to accept a requested period as a normal marine-query
business input and preserves Member 3's validated itinerary period for
planner-originated work. Member 2 has no additional sensor, device permission
or device-specific feature requirement. The current device assignments are
Member 1 GPS/location discovery, Member 3 planner date/time selection, and
Member 4 optional assessment image evidence; see the
[device-capability guide](docs/v1/device-capabilities.md).

Otherwise:

```text
PROJECT_REQUIREMENTS.md
        ↓
FROZEN FOR BLUEVERSE v0 + v1 IMPLEMENTATION
```
