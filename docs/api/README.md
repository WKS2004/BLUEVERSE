# API Foundation

The routes in this page are the intended public contract. The API and Auth
source projects are not present in the current checkout, so these endpoints
cannot be served locally until `services/api` and `services/auth` are added.

## Gateway routes

| Route | Destination |
|---|---|
| `/api/*` | ASP.NET Core API (expected at `services/api`) |
| `/` | React frontend |

## API conventions

- RESTful HTTP methods
- DTO request/response models
- asynchronous operations
- server-side validation
- consistent error responses
- authorization policies/permissions
- Swagger/OpenAPI

## Client integration contract

React and Flutter endpoint usage is registered in
[`../contracts/ui-integration.json`](../contracts/ui-integration.json). A
client may use only a public `/api/...` path listed there and referenced by
its shared workflow. The registry is validated in CI; it does not authorize
direct calls to Auth, Agentic AI, PostgreSQL or other internal services.

## Health

The frontend health route is served by the frontend Nginx container and exposed
by the gateway at:

```text
GET /health
```

The intended public API health route is:

```text
GET /api/health
```

Auth health route through the public API boundary:

```text
GET /api/auth/health
```

Additional ASP.NET services expose health routes through the same gateway convention:

```text
GET /api/<service-name>/health
```

Swagger/OpenAPI is exposed through the API gateway at:

```text
GET /api/swagger
GET /api/swagger/v1/swagger.json
```

The Swagger UI is configured only in the API service and provides both the public API and Auth API documents in one interface. The Auth OpenAPI document is available through the API boundary at `/api/auth/swagger/v1/swagger.json`; Auth's container is internal-only.

Use the `Authorize` button in the unified UI to enter a JWT as `Bearer {token}`. The bearer security definition is registered by the API service and is applied to operations that require authentication or permission policies.
