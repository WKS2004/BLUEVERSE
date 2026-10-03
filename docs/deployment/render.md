# Render Deployment

`render.yaml` is the initial Render Blueprint. It is deployment configuration,
not deployment evidence. It includes the public API and Auth services. The
marine-safety service is present in local Compose but is not defined in this
Render Blueprint; do not treat its local implementation as a hosted deployment.

It defines:

- Dockerized API
- Dockerized Auth service
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
7. Configure the same `JWT_SIGNING_KEY` value for the API and Auth services.
8. Keep `Jwt__AccessTokenMinutes` at `15` unless the security review approves
   another short-lived value. Keep `AuthSession__DefaultLifetimeDays` at `1`
   and `AuthSession__RememberMeLifetimeDays` at `30` to preserve the public
   session contract.
