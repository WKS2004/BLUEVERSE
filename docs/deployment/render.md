# Render Deployment

`render.yaml` is the initial Render Blueprint. It is deployment configuration,
not deployment evidence; the public API and Auth projects referenced by its
Dockerfiles are committed.

It defines:

- Dockerized API
- Dockerized Auth service
- managed PostgreSQL

The React frontend is intentionally outside this Blueprint because the planned frontend target is Vercel.

Before deploying:

1. Ensure the repository is connected to Render.
2. Ensure DHI registry credentials are configured if required.
3. Provide secret environment variables requested by `sync: false`.
4. Confirm the generated services expose the expected health paths.
5. Configure CORS and trusted origins for the actual deployed frontend.
6. Confirm the Auth service can reach the managed database; Auth applies its
   checked-in EF Core migrations during startup before becoming ready.
7. Configure the same `JWT_SIGNING_KEY` value for the API and Auth services.
8. Keep `Jwt__AccessTokenMinutes` at `15` unless the security review approves
   another short-lived value. Keep `AuthSession__DefaultLifetimeDays` at `1`
   and `AuthSession__RememberMeLifetimeDays` at `30` to preserve the public
   session contract.
