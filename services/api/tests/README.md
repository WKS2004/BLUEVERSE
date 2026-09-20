# API test suite

This suite is the authoritative test location for `services/api`. It uses
xUnit and `WebApplicationFactory` for deterministic HTTP-boundary tests. The
reverse-proxy cases use a local synthetic Kestrel destination; they do not
require Auth, PostgreSQL, Docker or network credentials.

Run the suite from the repository root with:

```powershell
dotnet test services/api/tests/Blueverse.Api.Tests/Blueverse.Api.Tests.csproj --configuration Release
```

Current coverage is intentionally limited to the API foundation that exists on
this branch:

- liveness and HTTP method behavior;
- the non-versioned public route contract;
- built-in OpenAPI, Swashbuckle and Swagger UI publication;
- allowed, rejected and preflight CORS behavior;
- forwarded headers and safe RFC 7807-style gateway errors;
- YARP Auth forwarding and unavailable-destination behavior.

JWT validation, named permission policies, DTO validation, persistence,
migrations, audit records and domain workflows are not implemented in the API
yet, so they require additional source contracts before meaningful tests can
be added.
