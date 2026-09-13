# Architecture Overview

## Logical architecture

```text
                     Internet / Local Network
                              |
                         edge-nginx
                              |
             +----------------+----------------+
             |                                 |
         React Web                       ASP.NET Core API
         / Vercel                       /        |       \
                                           Auth  Domain  Agentic AI
                                               \       |       /
                                                PostgreSQL
```

Flutter is a client application and communicates with the ASP.NET Core API. It is not a backend microservice.

## Local Docker architecture

```text
Client
  |
  v
edge-nginx :8080
  |
  +--> frontend :80
  |
  +--> api :8080 ------+
  |                    |
  +--> auth :8080  |
                       v
                   postgres :5432
```

Docker networks:

- `blueverse_edge`
- `blueverse_internal`

The internal network is not published directly to the host, except for the explicitly configured local PostgreSQL port `5432` used by pgAdmin4.

## Public application boundary

ASP.NET Core is authoritative for:

- authentication/authorization integration
- request validation
- business rules
- persistence
- Agentic AI workflow initiation
- approval enforcement
- audit/execution history

## AI boundary

If an internal Python Agentic AI service is introduced:

```text
React / Flutter
       |
       v
ASP.NET Core API
       |
       v
Internal Agentic AI service
       |
       +--> tools / data sources
```

The clients never call the AI service directly.
