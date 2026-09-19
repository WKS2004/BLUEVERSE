# API Foundation

The public API service is checked in at `services/api`. It is the single
backend entry point for React, Flutter and future service clients. The Auth
service is still a separate internal dependency at `services/auth`; routes
that require Auth cannot be used until that service is added.

## Gateway routes

| Route | Destination |
|---|---|
| `/api/*` | ASP.NET Core public API (`services/api`) |
| `/api/auth/*` | API reverse proxy to the internal Auth service |
| `/` | React frontend |

## API conventions

- RESTful HTTP methods
- DTO request/response models
- asynchronous operations
- server-side validation
- consistent error responses
- authorization policies/permissions
- Swagger/OpenAPI

The current API foundation provides the gateway health controller, YARP
reverse-proxy configuration, CORS policy, forwarded-header handling and a
standardized gateway error response. Domain DTOs, validation, persistence and
permission policies remain later implementation work.

## Health

The frontend health route is served by the frontend Nginx container and exposed
by the gateway at:

```text
GET /health
```

The public API health route is implemented by `services/api`:

```text
GET /api/health
```

It returns a small JSON payload such as:

```json
{"service":"api","status":"healthy"}
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
GET /api/swagger/v1.json
```

The Swagger UI is configured only in the API service and provides both the public API and Auth API documents in one interface. The Auth OpenAPI document is available through the API boundary at `/api/auth/swagger/v1/swagger.json`; Auth's container is internal-only.

Use the `Authorize` button in the unified UI to enter a JWT as `Bearer {token}`. The bearer security definition is registered by the API service for the secured operations that will be added as authentication and permission policies are implemented.

## Run the API directly

The HTTP launch profile listens on `http://localhost:5169`:

```bash
dotnet run --project services/api --launch-profile http
```

The direct-service smoke checks are:

```text
GET http://localhost:5169/api/health
GET http://localhost:5169/api/swagger
GET http://localhost:5169/api/swagger/v1.json
```

For the complete local stack, use the edge gateway at `http://localhost` as
described in the local deployment guide. The gateway is the supported client
entry point; clients must not call the internal Auth service directly.
