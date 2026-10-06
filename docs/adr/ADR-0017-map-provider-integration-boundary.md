# ADR-0017: Map Provider Integration Ownership and Boundary

**Status:** Accepted for component ownership and server-mediated lookup
boundary. The current public map-tile implementation is recorded below;
shared G00 agreement remains pending.

**Date:** 2026-09-26

## Context

BLUEVERSE v1 includes location-aware discovery of coastal destinations and
activities. The user has confirmed that the project will use a map API and
proposed Ushan Srinuka (Member 1) as its owner because Ushan Srinuka (Member 1) owns destination and leisure
activity discovery. The formal SE3090 assignment permits maps as a meaningful
third-party integration and asks that external-service access use the ASP.NET
Core backend where appropriate. Repository architecture is stricter for
client traffic: React and Flutter use only the public API and do not call
external providers directly.

The experience service already returns MapLibre style configuration for
OpenFreeMap, and place search is handled by the service through Photon with a
local catalogue fallback. The Flutter client still needs to render that
configured style as a real, interactive map. Provider terms, attribution and
availability behavior must remain explicit; map tiles do not need user
coordinates or BLUEVERSE credentials.

## Decision

1. Ushan Srinuka (Member 1) owns BLUEVERSE's map-provider integration contract and
   server-side consumer adapter inside its own internal .NET component
   service under `services/`.
2. `services/api` mediates map configuration, place search/geocoding and any
   provider operation that uses credentials or user-specific data. Flutter's
   MapLibre Native renderer and React's MapLibre GL JS renderer may fetch public
   style and vector-tile resources only from the HTTPS OpenFreeMap URL returned
   by `GET /api/experiences/map/config`. This bounded exception is for
   rendering the base map: clients must not construct a provider URL or send
   user identity, device location or BLUEVERSE credentials to that provider.
   Current-location coordinates are held in client memory and sent only to the
   public nearby endpoint after an explicit user action; the location marker is
   drawn locally. Other client map-provider calls remain behind the public API
   and Ushan's service.
3. Ushan Srinuka's persisted destination catalogue remains authoritative for
   canonical destination identity, title, coordinates, publication and
   business availability. Provider results are untrusted discovery or display
   context; they must not silently create, publish or overwrite a destination.
4. Map lookup/display is not a default Agentic AI tool or model data source.
   The Ushan Srinuka (Member 1) agent consumes validated location and destination context from
   Ushan Srinuka's tools. Any later agent need for a map operation requires a
   separate, least-privilege tool contract and evaluation.
5. Both clients use OpenFreeMap's public MapLibre styles: MapLibre Native in
   Flutter and MapLibre GL JS in React. OpenFreeMap requires attribution and
   advertises its public instance as keyless, with no SLA; both clients display
   MapLibre's attribution control and the attribution returned by the
   configuration API. The maps support pan, zoom and destination selection,
   not turn-by-turn directions. Preserve manual place search and catalogue-list
   discovery if configuration or tile loading fails.

## Consequences

- Ushan Srinuka (Member 1) can develop its component on the existing single
  `features/experience-biodiversity` branch; map functionality does
  not create a separate member branch or change the parallel four-member
  workflow.
- Ushan Srinuka's service owns style configuration, place search, response
  validation and provider failure behavior behind the public API. The two
  MapLibre renderers request public tile/style resources only from the
  configured OpenFreeMap host to render map imagery.
- Neither client may bypass configuration to select another tile host or use
  the renderer to send location, identity or credentials. Map search and
  geocoding remain service-owned; nearby coordinates go to the public API only
  after a user requests location-aware discovery.
- The current code implements OpenFreeMap style configuration and the
  server-mediated Photon place-search adapter. Flutter MapLibre Native and
  React MapLibre GL JS consume the config endpoint; shared-owner G00 review
  remains pending.
- Sanuda Abeysinghe (Member 2) owns Open-Meteo weather/marine acquisition. Adithya Gunawardana (Member 3) owns the
  separate BLUEVERSE consumer adapter for the IT3091 biodiversity inference
  API; Ushan Srinuka (Member 1) consumes its validated public result for experience-facing
  context. See [ADR-0019](ADR-0019-biodiversity-inference-integration-ownership.md).

## Evidence required to complete implementation

Before the map integration is accepted at G00, all owners must review the
OpenFreeMap/MapLibre tile-rendering exception and the public API boundary. The
component's verification must cover configuration failure, tile-load timeout,
invalid coordinates and an empty destination set while preserving manual
search and catalogue-list fallback. No client request may send user or device
location to the tile provider. Add only real public endpoints to the endpoint
catalog and register screens through the shared UI integration contract.

## Implementation references

- [OpenFreeMap Quick Start](https://openfreemap.org/quick_start/) documents its
  MapLibre style URLs and mobile MapLibre Native integration.
- [OpenFreeMap](https://openfreemap.org/) documents its public-instance
  attribution, no-key setup and service availability terms.
