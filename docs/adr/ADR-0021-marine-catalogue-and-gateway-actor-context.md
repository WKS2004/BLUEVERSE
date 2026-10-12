# ADR-0021: Marine activity catalogue and gateway actor context

- Date: 2026-10-08
- Status: Accepted for the Member 2 implementation requested in this chat;
  cross-member deployment compatibility remains an integration gate.
- Owners: Sanuda Abeysinghe (marine consumer), Member 1 (activity producer),
  public API/Auth owners (identity boundary).

## Context

Member 1 implements Activity CRUD in `experience-biodiversity`. Marine's
temporary activity writes and fixed client IDs created a second authority.
Marine also queried Auth tables directly, conflicting with ADR-0020.

## Decision

1. Member 1 owns activity creation, editing, publication and deletion. Marine
   consumes `GET /api/experiences/activities` through a private typed backend
   client. Only validated `PUBLISHED` records are usable. Marine exposes read
   projections at `/api/marine/activities`; its activity POST/PUT/DELETE routes
   are removed. Both clients fetch the projection through the public API.
2. `MarineActivities` retains local FK references and historic labels. It is a
   projection, never an authoritative catalogue or availability fallback.
   Existing placeholder IDs are preserved for history and never mapped by name.
   Managers create and independently approve new profiles for canonical IDs.
   Distinct IDs may share a name; the projection's name index is nonunique.
3. The public API selects the caller's bearer/session cookie and invokes the
   existing Auth `/api/auth/me` contract. Auth validates its current account and
   session and resolves current role permissions. Auth flows stay unchanged.
4. The API authorizes the marine operation and replaces any incoming actor
   header with an HS256 context under a separate `MARINE_ACTOR_SIGNING_KEY`.
   It binds actor/permissions to method, path/query and SHA256 body hash, with
   issuer `Blueverse.Api`, audience `Blueverse.MarineSafety` and 30-second
   lifetime. Caller credentials are stripped before forwarding. Marine accepts
   this context only and enforces its existing permission policies. It never
   reads Auth tables or authenticates client JWTs directly.
5. Unavailable Auth or catalogue returns 503 with `Retry-After: 5`, without
   granting authority or inventing activities. Condition-only lookups and
   historical/profile reads do not depend on the catalogue. Profile creation,
   editing/review and evaluation validate current published availability.

## Consequences and evidence

API and Marine deployments require the same independent actor secret. Native
development routes requests through the gateway. The Member 1 service is not
copied into this branch; configure `EXPERIENCE_CATALOGUE_URL` when deploying it.
Private services remain on the internal network. Context secrets confer
gateway authority and must be protected accordingly; deployment credentials
and database grants require environment-specific provisioning.

Tests exercise the production gateway middleware, Auth transport doubles,
request-bound context validation, catalogue transport/schema failures and both
clients. These do not establish a live cross-member or PostgreSQL integration.
No agent execution is introduced before G07.
