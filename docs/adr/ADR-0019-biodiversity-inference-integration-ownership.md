# ADR-0019: Biodiversity Inference Adapter Ownership and Boundary

**Status:** Accepted for v1 ownership and service boundaries; the IT3091
wire schema and public route/DTO remain implementation decisions.

**Date:** 2026-09-26

## Context

BLUEVERSE v1 must integrate the separate IT3091 biodiversity model and
inference service. The source workstream supplies the trained model/service;
BLUEVERSE is responsible for a genuine, server-mediated integration that
preserves useful prediction metadata and reports unavailable behavior without
fabricating results. The model is not Agentic AI and biodiversity is
informative context, not an inherent safety or operational authority.

The original ownership placed the BLUEVERSE ML consumer adapter with Member
1 because that component owns destinations, activities and the user-facing
biodiversity experience. The team reassessed the split. The adapter's main
system responsibility is a validated backend integration whose result can be
used by planning as optional contextual data, while Member 1 remains the
natural owner of how such context is explained in destination/activity
screens. Member 4 owns operational restrictions, assessments and approval;
model probability must not become that component's safety evidence or
decision authority.

The repository boundary requires React and Flutter to call only the public
ASP.NET Core API. Clients must not call an internal inference host. The
existing v1 work model assigns each member one complete `features/**` branch
and requires deterministic member components, including their ordinary
provider integrations, to be integrated and accepted before executable
`agentic-ai/**` implementation begins.

## Decision

1. **Member 3 owns BLUEVERSE's IT3091 integration.** Its
   `features/coastal-planner` branch implements the backend-mediated private
   adapter, deterministic validation of request/response data and the
   validated public prediction-result capability. Member 3 does not train,
   host or claim ownership of the separate IT3091 model or inference service.
2. **Member 1 owns the experience-facing use.** Its
   `features/coastal-experience-biodiversity` branch consumes Member 3's
   agreed public contract to request or display relevant biodiversity
   context in destination/activity experiences. Member 1 does not implement
   a second inference adapter and does not address the private IT3091 host.
3. **Member 4 is not the adapter owner.** Member 4 may consider validated
   biodiversity context only if a separately accepted business rule makes it
   relevant, but a prediction is not evidence of safety, observed presence,
   an operational restriction, an approval, or permission to change managed
   state. No such safety rule is introduced by this ADR.
4. **Keep the boundaries server-side.** React, Flutter and Agentic AI model
   prompts cannot call IT3091 directly or receive its credentials/private
   host. The Member 3 backend validates the request, calls the private
   inference service, validates its response and exposes only a public
   contract appropriate to authorized BLUEVERSE workflows.
5. **Treat predictions as optional, sourced ML context.** Preserve returned
   focal species, query location/area, model/version, prediction time,
   probability or habitat-suitability interpretation, uncertainty and
   limitations. Probability is not guaranteed species presence. Missing,
   stale, malformed, out-of-range or unavailable results must remain explicit
   unavailable/invalid states; they must never be replaced with guessed or
   zero values.
6. **Implement this integration before G07 as member-feature work.** It is
   ordinary ASP.NET Core ML/API integration and is required for v1. It is not
   an AI agent, RAG pipeline, LLM call, prompt, tool runtime or AI-owned
   workflow. After all four member components pass G07, the Member 1
   biodiversity specialist may use a separately allowlisted tool whose
   backend handler calls the approved Member 3 contract. The agent still does
   not call IT3091 directly.
7. **Keep implementation details evidence-based.** Agree IT3091
   authentication, transport, input/output schema, supported value ranges,
   timeouts, retry policy, freshness, caching/retention, health semantics and
   location precision with the supplying workstream before freezing source
   contracts. Do not add target endpoints to the live endpoint catalog before
   they exist.

## Rationale

- Member 3's planner assembles optional contextual information alongside
  existing source contracts and has the backend/API ownership to validate and
  expose the result. This keeps the inference adapter in the service layer
  that already integrates the planning workflow.
- Member 1 still owns the user need: discovery and clear presentation of
  sourced biodiversity information alongside coastal destinations and
  activities. It can implement against the agreed Member 3 contract and
  controlled fixtures while both branches are in progress, then verify the
  live provider/consumer integration after PRs merge to `dev`.
- Member 4's scope involves managed operational state and human approval.
  Assigning the biodiversity adapter to Member 4 risks coupling optional
  ecological predictions to safety or operational authority without a
  validated rule.
- A single adapter owner prevents duplicated external-service clients,
  inconsistent validation and competing unavailable/error semantics.

## Consequences

- Update the Member 1 and Member 3 component contracts, their work-area
  plans, the relationship map, the Agentic tool contract, the shared AI
  blueprint, readiness index and frozen requirement ownership consistently.
- Member 3 records evidence for genuine model-backed calls and deterministic
  validation/failure cases. Member 1 records evidence for rendering sourced
  results and unavailable/invalid states through the public contract.
- Shared route/DTO decisions are agreed at G00 and cross-checked after the
  feature PRs merge. Member 1 can use contract fixtures while Member 3's
  adapter is under implementation; fixture success alone is not integration
  acceptance.
- The Member 2 Open-Meteo adapter and Member 1 map adapter remain owned by
  their original components. These are separate provider contracts.
- The user's clarification also removes Member 2's separately assigned
  device feature. A requested condition period remains an ordinary Member 2
  query field; it is distinct from Member 3's itinerary date/time controls.
  Current device assignments are Member 1 GPS/location discovery, Member 3
  itinerary date/time and Member 4 optional assessment image evidence.
- No model vendor, ML framework, database/cache, vector store, public route,
  wire schema, hosting topology or prediction ranking weight is selected by
  this ADR.

## Alternatives considered

### Keep the adapter with Member 1

This would colocate provider calls with the user-facing biodiversity
experience. It was not selected because it blurs the service that assembles
planning context with the screen that presents it, while Member 3 already
owns backend planning integration. The contracts remain explicit so Member 1
can consume Member 3's API without losing ownership of UX.

### Assign the adapter to Member 4

This would colocate it with an operational review path. It was not selected
because biodiversity prediction is optional ecological context, not an
operational/safety decision signal, and Member 4 must not obtain implicit
authority from its ownership of approvals and operational restrictions.

### Let each component or agent call IT3091 independently

Rejected because it would duplicate credentials, privacy controls, schema
validation, retries, provenance and failure behavior, and would expose an
internal dependency beyond the approved backend boundary.

## Evidence required for implementation acceptance

Member 3's feature PR must show a genuine call to the separately supplied
service when configured, validated request/response association and
provenance, bounded timeout/retry, safe handling for missing model and
connection/schema/range/staleness failures, no secret leakage, and a public
contract suitable for Member 1. Member 1's feature PR must show available and
unavailable/invalid presentation with caveats and demonstrate that its
clients call only the public API. The merged `dev` integration must be
verified beyond fixtures before G07. The post-G07 agent tool must be
allowlisted, typed, read-only and evaluated separately from this ML service
adapter.
