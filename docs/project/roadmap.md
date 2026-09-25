# BLUEVERSE Roadmap

## v0 — Foundation

Authentication, authorization, permissions, API/Auth, PostgreSQL, React/Flutter
foundations, the shared UI route/API integration contract, Docker, gateway, CI
and documentation. The public API/Auth foundation and shared Auth workflow are
checked in. The [v0 guide](../v0/README.md) maps their component contracts;
domain endpoints are introduced with their owning later workflows.

Every authorized business workflow belongs in both React Web and Flutter
Mobile. Presentation and device input may differ without changing role,
action or outcome coverage.
User-facing work follows the principles in
[`ui-experience-principles.md`](ui-experience-principles.md).

## v1 — Coastal Tourism & Operations

The SE3090 submission target includes v0 and a fully integrated v1. V1 has
four member-owned business components: Coastal Experience & Biodiversity
Discovery; Marine Conditions & Safety Intelligence; Smart Coastal Planner &
Itinerary Management; and Coastal Operations, Advisories & Alerts. Each has
a distinct Agentic AI contribution. Open-Meteo conditions, internal
biodiversity-ML integration, a deterministic safety layer, authorized human
approval and the complete operational-assessment workflow are in scope.

React and Flutter provide the same permitted business workflows to tourists,
operators, reviewers and administrators. Neither platform has a stakeholder
or workflow priority. See the [v1 guide](../v1/README.md) and the
[requirements baseline](../../PROJECT_REQUIREMENTS.md).

Marine Biodiversity Intelligence is part of v1 integration. Its trained model
comes from the separate IT3091 workstream; BLUEVERSE must expose genuine
inference when the model is available and an explicit unavailable state when
it is not. The current repository does not yet contain that integration.

## v2 — Environmental Resilience

Environmental authority, incident, pollution and approval workflows across both
client surfaces, with permission-aware experiences for each participating role.

## v3 — Fisheries & Coastal Livelihoods

Fisheries and coastal-resource workflows across both client surfaces.
