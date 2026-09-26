# ADR-0018: Operational Assessment Image-Evidence Boundary

**Status:** Accepted for ownership and security boundary; image formats,
limits, storage provider and retention policy must be decided before Wanshaja Sooriyabandara (Member 4)
implements uploads.

**Date:** 2026-09-26

## Context

Wanshaja Sooriyabandara's v1 operational assessment already presents evidence to an
authorized human reviewer. The team wants a scoped camera/image-picker and
image-file upload capability so an operator can provide visual evidence for
a BLUEVERSE-managed operation. The current foundation has no assessment
workflow or media storage. The feature must remain compatible with the
React/Flutter public-API-only boundary and must not turn v1 into general file
sharing or environmental incident reporting.

## Decision

1. Wanshaja Sooriyabandara (Member 4) owns optional image evidence attached to its operational
   assessment lifecycle. Flutter may capture or select an image; React
   supports selecting an image file and may offer direct camera capture where
   the browser supports it. Both clients provide the same business action,
   permission behavior and reviewer-visible result.
2. Image upload, association, authorization and retrieval pass through the
   public API, which routes the operation to Wanshaja Sooriyabandara's private .NET service.
   Clients never receive storage credentials, access a storage host directly
   or use public static file URLs.
3. Wanshaja Sooriyabandara's service owns PostgreSQL attachment metadata, ownership,
   assessment/version association, status and audit references. Image bytes
   live in private backend-controlled storage behind that service's storage
   interface; the concrete storage provider and development/deployment
   configuration are not selected by this ADR.
4. The Wanshaja Sooriyabandara (Member 4) service validates actual file content, not just client name
   or MIME; it enforces agreed type/count/size limits, rejects unsafe or
   malformed images, and makes evidence available to reviewers only after
   accepted inspection/sanitization succeeds. The existing API remains a
   thin authenticated/authorized routing integration.
5. Evidence associated with a submitted assessment version cannot be
   silently replaced or removed. Supplemental evidence is separately
   authorized, versioned and auditable. Retention and deletion follow the
   final documented assessment schedule.
6. Raw images and storage references are not inputs to the post-G07 Agentic
   AI roles. A future model-vision/OCR capability requires a separate reviewed
   contract and explicit safety/evaluation work.
7. This decision excludes video, arbitrary documents, profile images, general
   file sharing, pollution/environmental incident submissions, emergency
   dispatch and government closure requests.

## Consequences

- Wanshaja Sooriyabandara's single `features/coastal-operations` branch includes its new
  internal service under `services/`, cross-platform capture/selection,
  upload and reviewer experience, private storage adapter, persistence,
  public API integration, failure behavior, tests and documentation.
- Before implementation, Wanshaja Sooriyabandara (Member 4) records the accepted image formats,
  maximum file count/bytes, scanning/sanitization approach, provider/config,
  retention/deletion behavior, assessment-version lifecycle, permissions and
  exact API contracts. Those concrete choices must be reflected in the
  endpoint catalog and security/operational docs when implemented.
- Storage outage or failed validation cannot create a false attachment
  record or claim success. Optional evidence remains optional unless a
  separately documented assessment rule requires it.
- The feature supports Wanshaja Sooriyabandara's human evidence-review task; it does not give
  the Agentic AI agent file or mutation tools.

## Related records

- [v1 device capabilities and evidence media](../v1/device-capabilities.md)
- [Wanshaja Sooriyabandara (Member 4) component contract](../v1/components/member-4-coastal-operations-advisories-alerts.md)
- [Wanshaja Sooriyabandara (Member 4) Safety & Operations Agent contract](../v1/agents/member-4-safety-operations-agent.md)
- [Client and API architecture](../architecture/service-boundaries.md)
