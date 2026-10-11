# Coastal Planner Biodiversity Public Contract v1

This contract lets any registered client present Coastal Planner biodiversity
context without contacting IT3091 directly. The private IT3091 wire proposal
remains unconfirmed; see [the proposal](it3091-biodiversity-contract-proposal.md).

## Route and authorization

`GET /api/planner/biodiversity/predictions?destinationId={guid}[&activityId={guid}]`
requires `planner.biodiversity.read`. The API gateway routes it to the private
Coastal Planner service. The planner sends only canonical destination ID,
optional activity ID, request time and a generated correlation ID to the
inference provider. It never forwards user identity or precise device location.

## Version and additive evolution

Responses include `contractVersion: 1`. Within v1, fields may be added but
existing field meanings and status values must not be repurposed. A breaking
change requires a separately reviewed versioning decision. Clients treat an
unknown `contractVersion`, status, or response schema as `unavailable` and must
not display accompanying prediction fields.

## Statuses

The closed status set is:

| Status | Meaning | Display-ready message |
|---|---|---|
| `available` | A validated, fresh result from the configured provider is present. Local fake results identify themselves as synthetic. | “Wildlife context is available. A prediction does not guarantee a sighting.” |
| `not_requested` | The optional context was not requested or the feature is disabled. | “Wildlife context was not requested.” |
| `unavailable` | Provider, network, authentication, timeout, rate-limit, or server failure prevents a current result. | “Wildlife predictions are temporarily unavailable. Please try again later.” |
| `invalid` | The request location is unsupported or a response fails schema, identity, version, or range checks. | “Wildlife predictions could not be verified for this location.” |
| `stale` | A provider result is outside the accepted freshness window. | “The wildlife prediction is out of date. Request a fresh result.” |

Every non-`available` result has an empty `predictedSpecies`, null
`modelMetadata`, a neutral `message`, and no prediction uncertainty. Clients
must map any unknown status to `unavailable`, discard prediction data, and use
the unavailable message. They must not turn a missing value into zero or
infer an outcome from a probability or confidence label.

## Response shape

```json
{
  "contractVersion": 1,
  "destinationId": "canonical-guid",
  "activityId": "canonical-guid-or-null",
  "status": "available",
  "message": "Wildlife context is available. A prediction does not guarantee a sighting.",
  "predictedSpecies": [{
    "speciesId": "supplier-species-guid",
    "scientificName": "supplier scientific name",
    "commonName": "supplier common name",
    "habitatSuitability": 0.0,
    "confidenceLevel": "supplier-defined",
    "uncertaintyText": "supplier-provided text or null"
  }],
  "modelMetadata": {
    "modelVersion": "supplier version",
    "inferenceTimestamp": "2026-10-11T00:00:00Z"
  },
  "provenance": {
    "provider": "supplier-provided provenance label (local fake: synthetic-it3091-fake)",
    "modelVersion": "supplier version",
    "inferenceTimestamp": "2026-10-11T00:00:00Z"
  },
  "uncertaintyText": "supplier-provided uncertainty or null",
  "limitations": "supplier-provided limitations"
}
```

`habitatSuitability` is preserved as the supplier field and is validated as a
finite number from 0 through 1. It is **not documented as a calibrated
probability** until IT3091 confirms its meaning. `confidenceLevel` and the
species ID/name mapping are also supplier-defined and unconfirmed. Display
supplier limitations and uncertainty without strengthening their claim.
Until the supplier provides metrics, the planner makes no claims about
calibration, geographic coverage, or prediction quality.

## Invariants

- Predictions are optional contextual enrichment. They do not assert observed
  species presence and never determine safety, activity eligibility,
  availability, operating status, or approval.
- Client calls go only to the public `/api/...` route. IT3091 hostnames,
  credentials, and its private wire schema stay server-side.
- No prediction is shown without matching destination/activity identity,
  supported contract version, valid ranges, model provenance, and accepted
  freshness.
- A non-available or unknown response cannot be converted into a prediction.
- Model output does not alter recommendation candidates or deterministic
  marine/operations evidence.

## Unconfirmed decisions

This client-facing shape is a Member 3 proposal because Member 1 coordination
and four-owner G00 ratification are pending. Supplier field semantics,
version policy, retention, and quality/uncertainty language remain open until
IT3091 owners and the G00 owners approve or correct the proposal.
