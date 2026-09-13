# Render Deployment

`render.yaml` is the initial Render Blueprint.

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
6. Run EF Core migrations using the approved deployment procedure.
