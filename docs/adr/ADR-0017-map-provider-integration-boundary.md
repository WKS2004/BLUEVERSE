# ADR-0017: Map Provider Integration Ownership and Boundary

**Status:** Accepted for component ownership and network boundary; provider
and feature-scope choices remain open.

**Date:** 2026-09-26

## Context

BLUEVERSE v1 includes location-aware discovery of coastal destinations and
activities. The user has confirmed that the project will use a map API and
proposed Member 1 as its owner because Member 1 owns destination and leisure
activity discovery. The formal SE3090 assignment permits maps as a meaningful
third-party integration and asks that external-service access use the ASP.NET
Core backend where appropriate. Repository architecture is stricter for
client traffic: React and Flutter use only the public API and do not call
external providers directly.

The current repository does not select or implement a map vendor. It also does
not settle whether v1 needs a rendered map, map tiles, place search,
geocoding, directions, or a smaller subset. Provider terms, credentials,
attribution, quotas, privacy and availability behavior depend on those choices.

## Decision

1. Member 1 owns BLUEVERSE's map-provider integration contract and
   server-side consumer adapter inside its own internal .NET component
   service under `services/`.
2. `services/api` mediates all client-requested map-provider access by
   routing/forwarding public `/api/...` operations to Member 1's service.
   React and Flutter call only the public API; provider credentials and
   outbound requests remain in Member 1's service. A provider that cannot
   support this boundary is not eligible under the current architecture unless
   a later reviewed ADR explicitly changes the boundary.
3. Member 1's persisted destination catalogue remains authoritative for
   canonical destination identity, title, coordinates, publication and
   business availability. Provider results are untrusted discovery or display
   context; they must not silently create, publish or overwrite a destination.
4. Map lookup/display is not a default Agentic AI tool or model data source.
   The Member 1 agent consumes validated location and destination context from
   Member 1's tools. Any later agent need for a map operation requires a
   separate, least-privilege tool contract and evaluation.
5. Keep the provider, exact map features, rendering approach, response schema,
   credential/key restrictions, license/attribution obligations, quotas,
   rate handling, cache/retention, timeout/retry policy and precise location
   minimization open until the owning implementation records evidence and
   updates its contract. Preserve a manual/list discovery path when the map
   provider is unavailable.

## Consequences

- Member 1 can develop its component on the existing single
  `features/coastal-experience-biodiversity` branch; map functionality does
  not create a separate member branch or change the parallel four-member
  workflow.
- Member 1's service owns map requests, provider credentials, response
  validation and provider failure behavior behind the public API integration.
- The eventual provider choice must be compatible with server-mediated access
  and useful in both React and Flutter. A direct client SDK/API call is not an
  allowed shortcut under this decision.
- This ADR does not claim a vendor, map SDK, geocoder, route service, API
  endpoint, production credential, or working map integration has been
  selected or implemented.
- Member 2 owns Open-Meteo weather/marine acquisition. Member 3 owns the
  separate BLUEVERSE consumer adapter for the IT3091 biodiversity inference
  API; Member 1 consumes its validated public result for experience-facing
  context. See [ADR-0019](ADR-0019-biodiversity-inference-integration-ownership.md).

## Evidence required to complete implementation

Before the map integration is accepted, the Member 1 PR and its follow-up
contract must identify the selected provider and exact features, document
terms/attribution and server-side credential configuration, define typed
request/result/error behavior, demonstrate no provider call from either
client, show privacy-minimal location handling, and cover timeout, rate-limit,
invalid response, no-result and unavailable behavior with a working manual
or list fallback. Add only real public endpoints to the endpoint catalog and
register any resulting screens through the shared UI integration contract.
