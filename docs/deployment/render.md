# Render Deployment

`render.yaml` is the initial Render Blueprint. It is deployment configuration,
not deployment evidence. It includes the public API and Auth services. The
marine-safety service is present in local Compose but is not defined in this
Render Blueprint; do not treat its local implementation as a hosted deployment.

It defines:

- Dockerized API
- Dockerized Auth service
- Private Dockerized Coastal Planner service, reached only through API forwarding
- managed PostgreSQL

The Member 2 marine-safety backend still needs a private hosted-service
definition and deployment configuration before the marine workflow is
available from a hosted stack.

The React Web frontend and Flutter Mobile client are intentionally outside this
backend Blueprint. React is planned for Vercel, while Flutter is distributed
through the selected mobile target channels; both consume the deployed public
API.

Before deploying:

1. Ensure the repository is connected to Render.
2. Ensure DHI registry credentials are configured if required.
3. Provide secret environment variables requested by `sync: false`.
4. Confirm the generated services expose the expected health paths.
5. Configure CORS and trusted origins for the actual deployed frontend.
6. Confirm the Auth service can reach the managed database; Auth applies its
   checked-in EF Core migrations during startup before becoming ready.
7. Provide `JWT_SIGNING_KEY` on Auth. The Blueprint references that same value
   from API and Coastal Planner with `fromService`.
8. Keep `Jwt__AccessTokenMinutes` at `15` unless the security review approves
   another short-lived value. Keep `AuthSession__DefaultLifetimeDays` at `1`
   and `AuthSession__RememberMeLifetimeDays` at `30` to preserve the public
   session contract.

The two private services use the billable `0.5c-512mb` compute plan; `free`
is not a private-service plan. Review the cost in Render before deploying.
This file does not create or deploy resources by itself. See the
[official Blueprint specification](https://render.com/docs/blueprint-spec).

Supply `ConnectionStrings__DefaultConnection` as a secret **Npgsql key/value
connection string** for each backend. For example, use the managed database's
private host, port, database, user and password in
`Host=<private-host>;Port=5432;Database=<database>;Username=<user>;Password=<secret>;SSL Mode=Require`.
Do not paste Render's `postgresql://...` URL into this setting: these services
call `UseNpgsql` with an Npgsql connection string. Keep credentials out of Git.

Auth and Coastal Planner apply their own migrations and expose readiness at
`/api/auth/health` and `/api/planner/health`; API liveness is `/api/health`.
The API forwards the planner routes to `http://blueverse-coastal-planner:10000`.
`AuthSession__SelfServicePlannerAccess=true` gives newly registered travellers
the seeded planner grants, matching local Compose; set it to `false` to require
administrator assignment. Internal forwarding stays HTTP.

Peer catalogue, marine and operations URLs must be set to the actual accepted
owner services before live planning is accepted. Biodiversity is disabled in
this production Blueprint until its endpoint, service credential, model version
and provider contract are confirmed. No peer services or executable AI are
defined here. Flutter's current HTTP configuration is retained for local work;
hosted mobile transport remains a separate release gate.

When adding the private Marine Safety deployment, configure the same independent
`MARINE_ACTOR_SIGNING_KEY` on API and Marine, and the private
`EXPERIENCE_CATALOGUE_URL` on Marine. The existing API/Auth foundation can run
without an actor key; protected Marine requests then return 503, while Auth
and health routes remain available. Do not expose the component service to
clients. See the root README and [ADR-0021](../adr/ADR-0021-marine-catalogue-and-gateway-actor-context.md)
for the complete integration setup.
