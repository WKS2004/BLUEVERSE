# Docker Infrastructure

Custom Dockerfiles remain here so application codebases can stay close to their normal IDE-generated structures.

## Services

- `api/` — ASP.NET Core public API
- `auth/` — ASP.NET Core Auth service
- `frontend/` — React build + static Nginx runtime
- `edge-nginx/` — local reverse proxy configuration

PostgreSQL is consumed directly from its selected DHI image in `compose.yaml`; a custom PostgreSQL Dockerfile is intentionally not created.

Auth is an internal backend service. It is not attached to the edge network and must be reached through the API's `/api/auth/...` reverse-proxy route.

The frontend Nginx file is a `server`-context configuration because the Dockerfile installs it under `/etc/nginx/conf.d/default.conf`. Global directives such as `worker_processes`, `events` and `http` belong only in the edge gateway's `/etc/nginx/nginx.conf`.

## Important

The application directories referenced by the Dockerfiles are intentionally absent from this foundation package. They are generated separately:

```text
apps/web
apps/mobile
services/api
services/auth
```
