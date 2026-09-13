# API Foundation

## Gateway routes

| Route | Destination |
|---|---|
| `/api/*` | ASP.NET Core API |
| `/` | React frontend |

## API conventions

- RESTful HTTP methods
- DTO request/response models
- asynchronous operations
- server-side validation
- consistent error responses
- authorization policies/permissions
- Swagger/OpenAPI

## Health

The initial public API health route is:

```text
GET /api/health
```

API health route:

```text
GET /api/health
```

Auth health route through the public API boundary:

```text
GET /api/auth/health
```

Swagger/OpenAPI is exposed through the API gateway at:

```text
GET /api/swagger
GET /api/swagger/v1/swagger.json
```

The Swagger UI is configured only in the API service and provides both the public API and Auth API documents in one interface. The Auth OpenAPI document is available through the API boundary at `/api/auth/swagger/v1/swagger.json`; Auth's container is internal-only.

Use the `Authorize` button in the unified UI to enter a JWT as `Bearer {token}`. The bearer security definition is registered by the API service and is applied to operations that require authentication or permission policies.
