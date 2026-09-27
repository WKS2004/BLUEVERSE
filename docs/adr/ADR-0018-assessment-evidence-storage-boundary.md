# ADR-0018: Operational Assessment Image-Evidence Boundary

**Status:** Accepted for the Member 4 branch implementation; shared G00
acceptance remains pending. The selected format, limits, storage configuration
and retention policy below are branch-local decisions until that review.

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
3. The service stores attachment metadata in PostgreSQL and image bytes in a
   private filesystem storage adapter. Compose mounts the named
   `blueverse_coastal_operations_evidence` volume at
   `/var/lib/coastal-operations/evidence`; the volume is not published or
   served as static content. Production storage may be replaced behind the
   adapter after deployment needs are reviewed.
4. Accept only static, non-interlaced, 8-bit truecolor PNG (RGB or RGBA), at
   most 5 MiB, at most 4096 pixels in either dimension and at most 12 million
   pixels. The service validates PNG chunk checksums and structure, inflates
   the expected scanline data, rejects unsupported animation/critical chunks,
   and strips ancillary metadata before storage. MIME type alone is not
   trusted. The API and edge gateway enforce request limits above the file
   limit to allow multipart framing.
5. An assessment accepts at most five images. Upload is allowed only to its
   initiating operator while the assessment is `SUBMITTED` or
   `REVISION_REQUESTED`; each attachment is immutable and increments the
   assessment version. The assessment owner and a caller with the queue/evidence
   review grants may retrieve content through the API. No storage URL is
   returned.
6. The service retains images for 365 days, then a background worker deletes
   the bytes and marks metadata `EXPIRED` with an audit entry. Content length
   and SHA-256 must match persisted metadata on retrieval. A missing/corrupt
   file or storage outage returns a safe service error, not a false success.
   Local volume deletion is destructive to evidence as well as database data.
7. The existing API remains a thin authenticated/authorized routing
   integration. Raw images and storage references are not inputs to the
   post-G07 Agentic AI roles. A future model-vision/OCR capability requires a
   separate reviewed contract and explicit safety/evaluation work.
8. This decision excludes video, arbitrary documents, profile images, general
   file sharing, pollution/environmental incident submissions, emergency
   dispatch and government closure requests.

## Consequences

- Wanshaja Sooriyabandara's single `features/coastal-operations` branch includes its new
  internal service under `services/`, cross-platform capture/selection,
  upload and reviewer experience, private storage adapter, persistence,
  public API integration, failure behavior, tests and documentation.
- The endpoint catalog records the implemented upload and retrieval
  operations. The current service accepts a deliberately limited PNG subset;
  malware scanning and arbitrary image formats are not part of this contract.
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
