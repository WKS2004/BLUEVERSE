# Docker rules

- Preserve the selected DHI images: `dhi.io/node:24-debian13-dev`,
  `dhi.io/nginx:1.30-alpine`, `dhi.io/dotnet:10-sdk-alpine`,
  `dhi.io/aspnetcore:10-alpine` and `dhi.io/postgres:16-alpine`.
- Keep Dockerfiles under `infrastructure/docker/<service>/` and build from the
  repository root so project paths resolve consistently.
- Treat the ASP.NET runtime as minimal: use exec-form `ENTRYPOINT`/`CMD` and do
  not assume `/bin/sh` or debugging utilities exist.
- Preserve intended Compose networks, health/readiness behavior, gateway
  routing and internal-service isolation. Do not publish internal ports merely
  to simplify local testing.
- Keep secrets in environment variables or approved secret storage; never put
  credentials in Compose, images, logs or `.agents`.

For changes, inspect `compose.yaml`, the affected Dockerfile/configuration,
the Docker docs and the matching CI workflow. Run the routed structural and
Docker validation before claiming success.
