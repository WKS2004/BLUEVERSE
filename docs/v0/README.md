# BLUEVERSE v0 foundation

v0 establishes the shared application and engineering platform described in
[PROJECT_REQUIREMENTS.md](../../PROJECT_REQUIREMENTS.md), section 3. This guide
maps that foundation to its owning source, public contracts and verification.
It is also the implementation reference for extending those components.
Confirm current behavior against source and the
[endpoint catalog](../api/endpoint-catalog.md) before changing a contract.

## Foundation components

| Component | Responsibility |
|---|---|
| [React Web client](components/react-web-client.md) | Browser application, public API adapter and Auth session experience |
| [Flutter client](components/flutter-client.md) | Mobile application, gateway configuration, secure credentials and Auth session experience |
| [Public API and gateway](components/public-api-gateway.md) | Sole client-facing ASP.NET Core boundary, Auth forwarding, health, security and OpenAPI |
| [Auth and access control](components/auth-identity-access.md) | Identity, accounts, permissions, JWTs and device sessions |
| [PostgreSQL and EF Core](components/postgresql-ef-core.md) | Auth persistence, migrations, relational integrity and provider-specific evidence |
| [Docker and local network](components/docker-local-stack.md) | DHI images, Compose topology, edge routing and local health |
| [Contracts and CI](components/contracts-ci-validation.md) | Shared workflow registry, route catalog, test discovery and validation gates |
| [Repository and agent resources](components/repository-agent-resources.md) | Structure, ADRs, task routing, skills and contribution evidence |

These are technical foundation responsibilities, not assignments to the four
v1 member-owned business components. The [v1 guide](../v1/README.md) defines
those later domain components and agents.

## Shared application path

```text
React Web ─┐
           ├─► public gateway /api/... ─► ASP.NET Core API ─► Auth
Flutter ───┘                                         │          │
                                                   │          ▼
                                              OpenAPI/health  PostgreSQL
```

The local [edge-nginx](components/docker-local-stack.md) instance routes
browser and mobile traffic to the public API. Auth and PostgreSQL stay private.
The clients share workflow intent, permissions and server-owned state. Both
client technologies serve every authorized role and business capability;
layout and device input may differ. See the
[cross-client contract](../development/ui-integration.md).

## Foundation workflow

The [sign-in and session path](integration-and-acceptance.md) connects the
clients, public API, Auth and PostgreSQL. Browser transport uses protected
cookies; native Flutter transport uses platform secure storage. The server
owns permission decisions and session lifecycle. The same public boundary is
used for subsequent domain workflows.

v0 supplies the technical foundation. Coastal tourism, marine-condition,
planning, operations, Agentic AI and biodiversity business workflows are
specified separately for v1. This distinction describes scope, not a
restriction on either frontend.
