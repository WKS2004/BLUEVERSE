# IT3091 Biodiversity Inference Contract Proposal

**Status: unconfirmed proposal.** No IT3091 owner has approved this document.
All wire details below are assumptions for review, not facts about the supplied
model or service. BLUEVERSE owns only the consumer adapter and public result
contract; IT3091 owns its model and inference service.

## Proposed private endpoint (assumption)

- Method/path: `POST /v1/predictions` over HTTPS in deployed environments.
- Authentication: service-to-service bearer credential supplied through
  `BIODIVERSITY_ML_API_KEY`; never placed in source, client configuration, or
  committed Compose values. Authentication method and audience are unconfirmed.
- A development fake may accept an empty credential only on the isolated
  internal Docker network. It is synthetic and is not an IT3091 substitute.
- The request carries `X-Correlation-ID`; the value is for tracing only and is
  not a user or location identifier.

## Proposed request (assumption)

```json
{
  "contractVersion": 1,
  "destinationId": "canonical-guid",
  "activityId": "canonical-guid-or-null",
  "requestedAtUtc": "2026-10-11T00:00:00Z"
}
```

Only validated canonical destination/activity IDs and request time are sent.
No account identity, exact GPS coordinate, free-form personal context, or
tracking history is sent. The use of canonical IDs in place of a coarse
coordinate bounding box is an assumption; the G00 proposal allows either.
The real service must document its own canonical-ID lookup or mapping. Optional
season/month is mentioned in G00 but is intentionally omitted until its need
and semantics are confirmed.

## Proposed response (assumption)

```json
{
  "contractVersion": 1,
  "provider": "supplier-controlled provenance label",
  "destinationId": "same-canonical-guid",
  "activityId": "same-canonical-guid-or-null",
  "status": "available",
  "modelVersion": "supplier-version",
  "inferenceTimestampUtc": "2026-10-11T00:00:00Z",
  "species": [{
    "speciesId": "canonical-species-guid",
    "scientificName": "supplier scientific name",
    "commonName": "supplier common name",
    "habitatSuitability": 0.0,
    "confidenceLevel": "supplier-defined",
    "uncertaintyText": "supplier-provided caveat"
  }],
  "limitations": "supplier-provided limitations"
}
```

`habitatSuitability` in `[0,1]`, its interpretation, `confidenceLevel`, and
`speciesId` mapping are unconfirmed; do not label the number a calibrated
probability until the owners verify that. Species names and IDs must retain
their supplier provenance. The response must associate with the requested
destination/activity and include model version, inference time, limitations,
and uncertainty. `provider` is required provenance identifying the responding
implementation; the local fake returns `synthetic-it3091-fake`, never a claim
that IT3091 answered. The fake's values are synthetic test fixtures only.

## Proposed freshness, resilience and errors (assumption)

- Reject missing/mismatched identity, unsupported schema/version, non-finite or
  out-of-range values, missing provenance, and future timestamps beyond a small
  clock-skew allowance.
- G00 proposes a 3-second timeout and at most one retry. Whether retries apply
  to each error class remains for IT3091/service-owner review. The adapter's
  circuit breaker is local protection and does not imply IT3091 availability.
- A six-hour cache lifetime already exists in the planner. It is **not approved
  by IT3091**. The cache is only usable when a configured expected model
  version matches the stored version; unversioned cache reads are unsafe. The
  existing six-hour freshness/retention policy remains pending owner approval.
- Proposed error mapping: unsupported/unknown canonical location → `invalid`;
  service reports no current prediction → `unavailable`; timeout, 5xx,
  connection or auth failure → `unavailable`; malformed/mismatched response →
  `invalid`; valid but out-of-freshness result → `stale`. Exact HTTP codes and
  error body remain unconfirmed.
- Proposed HTTP outcomes: `200` for a schema-valid inference result (including
  an explicit non-available status); `400` for malformed request; `401/403`
  for auth; `404/422` for unsupported location; `429` for rate limiting; `5xx`
  for provider failure. This list is a proposal, not a claim about IT3091.
- Rate limits, quotas, concurrent request limits, and `Retry-After` behavior
  have not been supplied and must be confirmed. Until then, BLUEVERSE will use
  bounded calls, a circuit breaker, no background polling, and avoid retrying
  client errors or rate limits.

## Existing adapter / G00 mismatches to resolve

- The G00 Member 3 example uses uppercase `AVAILABLE`/`UNAVAILABLE`/
  `NOT_REQUESTED`, a sample `it3091-v1.2` model version, and a less complete
  response. Those values are examples, not accepted provider statuses or a
  supported-version policy. This proposal uses a lowercase public status set
  and never treats `it3091-v1.2` as configured truth.
- G00 allows a coarse bounding box or canonical destination ID and mentions
  optional season/month. This implementation proposal sends canonical
  `destinationId`, optional `activityId`, and request time only. IT3091 must
  confirm destination/activity ID mapping or request different coarse input;
  season/month stays omitted until approved.
- The existing planner controller/cache already use destination/activity IDs.
  The old cache key omitted model version; it now includes configured expected
  model version and refuses reads without one. This stricter cache key and the
  existing six-hour freshness/retention window still require provider/G00
  approval. Provider identity is retained separately so synthetic fake output
  cannot be mislabeled as an IT3091 result.
- The draft private call is `POST /v1/predictions`; no confirmed IT3091 path
  was present in the prior adapter, which used a query-style `GET /predict`.
  Confirm the method/path and service authentication before production use.
- Public BLUEVERSE routing remains the established `GET
  /api/planner/biodiversity/predictions` with `planner.biodiversity.read`; the
  versioned additive response shape in the companion public contract is a
  Member 3 proposal pending Member 1 presentation compatibility and G00
  ratification.

## Public result rules

The public planner result uses additive contract version `1` and only these
lowercase statuses: `available`, `not_requested`, `unavailable`, `invalid`,
`stale`. Every non-available result includes a neutral display-ready message
and no predicted species. Clients must treat any unknown future status as
`unavailable`. A real result includes supplier provenance, model version,
inference timestamp, limitations, and only supplier-provided uncertainty text.
Predictions are contextual only; they never imply sighting, safety, eligibility,
availability, operational status, or approval.

## Open questions for IT3091 owners

1. What preprocessing must match training exactly: encoding, scaling,
   categorical handling, feature order, units, defaults, and missing-value
   policy? Who owns/version-controls that preprocessing?
2. Are output probabilities calibrated? What does `habitatSuitability` mean,
   and what are the class/threshold semantics for confidence labels?
3. How are model class labels mapped to species identifiers, scientific names,
   common names, synonyms, and taxonomy versions?
4. Which model versions are accepted? How are model updates, rollback,
   deprecation and expected-version negotiation handled?
5. What evaluation metrics, validation data, geographic/seasonal coverage,
   bias checks, known limitations and failure conditions should be disclosed?
6. What are the production endpoint, network topology, authentication scheme,
   credential rotation, TLS requirements and service identity?
7. Which location inputs are supported: canonical destination/activity IDs,
   coarse bounding boxes, or both? Is season/month necessary? What precision
   and retention are acceptable?
8. What are the response schema, rate limits, quotas, timeout guidance,
   retryable error codes, `Retry-After`, maximum response sizes and freshness
   guarantees?
9. Does the supplier support prediction caching? Is the current six-hour cache
   TTL acceptable, and what are its privacy, retention and invalidation rules?
10. Which provenance, uncertainty, limitation, dataset and inference timestamp
    fields are authoritative and guaranteed on every result?

Approval or corrections from the IT3091 owners are required before this
proposal becomes a production integration contract.
