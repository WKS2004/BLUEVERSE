# Marine Safety dependency availability

Marine Safety owns its records and readiness. Other member services supply data
through agreed backend API endpoints. Their absence affects operations that need
that data, while independent Marine capabilities remain accessible through the
public API and the existing permission model.

## Operation boundaries

| Operation | Required external data | Behaviour when that data is unavailable |
|---|---|---|
| Own health/readiness | None from other member components | Checks Marine's database/schema only. |
| Condition lookup | Open-Meteo evidence | Catalogue absence has no effect. Complete provider failure returns structured 503; partial evidence retains null/missing fields. |
| Saved profiles, snapshots and assessments | None from other member components | Reads Marine-owned stored evidence without calling the catalogue or provider. |
| Profile deactivation | None from other member components | Retains the profile/history and deactivates the saved version. |
| Published activity catalogue | Member 1 published activities | Structured 503 when unavailable; 200 `[]` when the producer is available and has no published rows. |
| Profile create/edit/review | Verified published activity data | Structured 503; no new draft/review result is stored. |
| Suitability evaluation | Verified published activity data and applicable condition evidence | Required dependency failure returns structured 503 without creating an assessment. No fabricated positive result. |

Protected requests still require verified Auth identity and permissions at the
public API. Auth verification failure returns 503 with unavailable actor data;
it never authorizes a request by assuming identity or using hard-coded roles.

## Structured response

For example, an absent activity producer returns HTTP 503,
`Content-Type: application/problem+json` and `Retry-After: 5`:

```json
{
  "status": 503,
  "title": "Activity Data Unavailable",
  "detail": "The activity catalogue is unavailable. Retry to refresh activity data.",
  "availability": "UNAVAILABLE",
  "dependency": "experience-biodiversity",
  "code": "ACTIVITY_DATA_UNAVAILABLE",
  "reason": "UNREACHABLE",
  "missingFields": ["activityCatalogue"],
  "retryable": true,
  "data": null
}
```

| Field | Meaning |
|---|---|
| `availability` | `UNAVAILABLE` describes the requested data/operation, not every Marine capability. |
| `dependency` | Logical producer identifier; never a private network URL or connection string. |
| `code` | Stable consumer-facing code: `ACTIVITY_DATA_UNAVAILABLE`, `CONDITION_DATA_UNAVAILABLE`, `ACCOUNT_VERIFICATION_UNAVAILABLE`, `MARINE_ACCESS_UNAVAILABLE` or `MARINE_SERVICE_UNAVAILABLE`. |
| `reason` | Safe reason label. Catalogue uses `UNREACHABLE`, `UPSTREAM_UNAVAILABLE`, `TIMEOUT`, `INVALID_RESPONSE` or `UNCONFIGURED`. Other failures use `UNAVAILABLE` or the gateway's `UNREACHABLE`/`TIMEOUT`. |
| `missingFields` | Required data absent for this operation: for example `activityCatalogue`, condition variables, `verifiedActor`, `verifiedAccess` or `marineSafetyData`. |
| `retryable` | A later manual retry can be offered where safe. This never authorizes automatic replay of a dispatched write. |
| `data` | `null` because valid data was not obtained; distinct from an empty successful list. |

Provider failures use `CONDITION_DATA_UNAVAILABLE`, dependency `open-meteo` and
`Retry-After: 60`. Partial snapshots retain the established response schema;
missing values remain null and appear in `missingFields`.

If the gateway cannot reach Marine, it reports `MARINE_SERVICE_UNAVAILABLE`.
GET/HEAD have a retry hint; POST/PUT/DELETE report an unconfirmed outcome,
`retryable: false` and no retry hint. Check saved history/profile state before
manually repeating the action. Responses already received from a service are
preserved, as are routes outside Marine.

## Startup and clients

`EXPERIENCE_CATALOGUE_URL` remains optional for startup. Compose supplies the
private service default. An invalid or empty configured URL is represented as
unconfigured dependency data when the catalogue is requested. The five-second
catalogue timeout is bounded; request cancellation is preserved.

Both clients show "Activity data unavailable" with a retry action while keeping
saved records visible. Profile draft/edit/review and evaluation need available
published activity data; deactivation remains independent. No temporal wording,
placeholder records, implicit safe outcomes or dependency-wide restart is used.

Successful list shapes, routes and permissions are preserved. Availability
metadata is additive to failure bodies and both client error adapters retain it.
See [ADR-0022](../adr/ADR-0022-member-service-data-availability-and-fault-isolation.md)
and [the endpoint catalog](endpoint-catalog.md).
