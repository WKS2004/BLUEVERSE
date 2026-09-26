# v1 device capabilities and evidence media

**Status:** target contract; none of these v1 business workflows is implemented
in the current v0 client foundation. This document assigns the device
interactions to their owning member feature branches and does not add separate
device-only branches.

## Decision and ownership

| Owner | In-scope capability | Business use | Client behavior |
|---|---|---|---|
| Ushan Srinuka (Member 1) — Coastal Experience & Biodiversity Discovery | GPS/device location and location-aware discovery; selected map-provider integration remains a separate backend-mediated responsibility | Find nearby coastal destinations and activities, with user permission and a manual alternative | Flutter offers a one-time current-location action. React supports location search/manual entry and may offer browser geolocation as an explicit convenience. Both produce the same nearby-discovery outcome. |
| Adithya Gunawardana (Member 3) — Smart Coastal Planner & Itinerary Management | Date/time selection | Set the requested planning period and itinerary schedule | Flutter uses native date/time picker controls. React uses accessible date/time inputs. Both submit equivalent, validated values to the public API. |
| Wanshaja Sooriyabandara (Member 4) — Coastal Operations, Advisories & Alerts | Optional photo evidence capture/selection and image-file upload | Attach visual evidence to a BLUEVERSE-managed operational assessment for the authorized reviewer | Flutter supports camera capture and image selection/upload. React supports image-file selection/upload; on camera-capable browsers, direct capture may be offered as a convenience. The reviewer-visible business outcome is equivalent. |

The assignment minimum is met by the required Flutter GPS/device-location
workflow. Adithya Gunawardana's itinerary date/time selection and Wanshaja Sooriyabandara's optional
evidence images are additional interactions chosen because they directly
support their existing v1 workflows. Sanuda Abeysinghe's marine condition query still
requires a normal business period input, but it is not assigned a separate
device feature. These interactions do not replace GPS or authorize adding
every device feature in the assignment's example list.

These allocations are workflow ownership, not an hours-equalization formula.
Adithya Gunawardana's date/time controls serve its planner/API/itinerary/re-evaluation
scope; Wanshaja Sooriyabandara's evidence upload adds storage and security work to its
existing assessment/review/approval scope. Sanuda Abeysinghe's provider-query period
is part of its existing business form and suitability contract. The complete
component responsibilities and evidence remain with each member as defined
in the [single-branch workflow](member-branch-workflow.md).

## Shared cross-client and architecture rules

- React Web and Flutter Mobile provide the same authorized business actions,
  validation meaning, workflow state and results. Their device controls may
  differ to suit web and mobile hardware.
- Both clients call only registered relative `/api/...` operations through
  `services/api`, which authenticates/authorizes and routes to the owning
  private member service. They do not call the map vendor, object storage,
  Auth, PostgreSQL, Agentic AI or component services directly.
- Device permission and access to a sensor are requested only after a clear
  user action and only for the immediate workflow. No background or
  continuous location tracking is in v1.
- A permission prompt, file picker or native widget is not the business
  operation. The public API applies the existing caller authorization and
  routes to the owning member service, which validates, persists and returns
  the authoritative business result.
- Time entry is not server validation. The owning component service defines
  supported period bounds, zone interpretation, provider coverage and the
  resulting condition/suitability evidence; the public API remains the
  authenticated entry point.
- These capabilities are implemented inside the complete owning
  `features/<component>` branch. The work-area numbers in member plans help
  coverage and local dependency analysis; they do not create new branches or
  impose a cross-member implementation order.
- Executable Agentic AI remains post-G07 on `agentic-ai/**`. Member branches
  can prepare the normal API/access boundary, but the device interactions do
  not create or invoke agent runtimes.

## Ushan Srinuka (Member 1) — GPS/device location and map-assisted discovery

### User interaction

1. The person opens coastal discovery and chooses **Use my current location**
   in Flutter or the equivalent explicit location action in React if browser
   geolocation is supported. A manual destination/region search remains
   available in both clients.
2. The client requests one location reading while the screen is active. It
   explains why location is useful before the operating system/browser prompt.
3. The client maps the result to the location input for a nearby query. It
   shows a concise, user-understandable selected-location label and the
   search radius/filters that will be used; raw coordinates need not be shown.
4. `services/api` authenticates/authorizes and routes the search to the private
   Ushan Srinuka (Member 1) service. That service validates coordinate/radius bounds and
   filters, then returns nearby destinations/activities with the same
   eligibility, publication, availability and restriction rules as manual
   search.
5. The client handles the result through the normal loading, populated, empty,
   recoverable-error and retry states. Location does not bypass permissions or
   make unavailable/unpublished experiences discoverable.

Flutter must distinguish permission not yet requested, granted, denied,
permanently denied/restricted, location services disabled, timeout and
unavailable reading. Explain the relevant next step and retain manual search
when location cannot be acquired. React must handle unsupported browsers,
insecure contexts, denied permission, timeout and unavailable reading with
the same manual fallback. Never loop permission prompts.

### Data minimization and map boundary

- Use a one-time reading for the user's explicit nearby request. Do not
  persist a movement trail, reuse the reading for unrelated workflows or
  interpret one-time consent as continuing consent.
- Send the minimum coordinate precision needed for the agreed nearby search.
  Persist raw device coordinates only if a separately documented business
  need and retention rule requires it. Canonical destination coordinates
  remain Ushan Srinuka (Member 1)-owned catalogue data and are distinct from a visitor's
  transient device reading.
- GPS acquisition is a device capability and does not depend on a particular
  map vendor. Map/place/geocoding/display calls are performed by Ushan Srinuka's
  private service behind the public API integration. The map provider and
  exact rendered-map features remain unselected until Ushan Srinuka (Member 1) records the
  provider, terms, attribution, credential handling and supported features in
  the implementation contract.
- Provider outage must leave the manual/list discovery path usable and must
  not make provider content authoritative over BLUEVERSE's catalogue.

## Adithya Gunawardana (Member 3) — date/time selection for planning

Date/time selection is already part of the planner's place, time and duration
inputs; this is a device-appropriate way to capture that in-scope input rather
than an additional planner feature.

- Flutter uses accessible native date and time picker controls. React uses
  semantic date/time inputs with labels and keyboard support.
- Both clients show the selected date, local time, duration and applicable
  location/time-zone context before submission. A date-only value is not
  silently converted to a UTC instant.
- Planning requests validate start/end ordering, duration, supported planning
  horizon and time-zone semantics on the server. A client-side picker cannot
  make an unavailable schedule or unsafe activity eligible.
- The implementation contract must identify whether a selected local time
  uses the destination's time zone or a supplied zone identifier, how daylight
  saving gaps/overlaps are handled, and the API representation before the
  member branch is accepted. React and Flutter must submit the same semantic
  value even if their widgets differ.
- Date/time selection is input, not a notification/reminder feature. Push or
  local notifications are not added by this decision.

## Wanshaja Sooriyabandara (Member 4) — optional assessment image evidence

Image attachments support an operator's existing assessment of a
BLUEVERSE-managed activity, offering or session. They supplement, rather than
replace, the Ushan Srinuka and Sanuda Abeysinghe sourced evidence and deterministic validation. They
are optional in v1 unless a specific assessment rule later requires a
particular evidence type and is documented.

### Capture, upload and review

- Flutter lets an authorized operator capture a photo or choose an existing
  image. React lets the operator choose an image file and may expose a camera
  capture hint on supported mobile browsers.
- Both clients submit the selected image through the Wanshaja Sooriyabandara (Member 4) public API and
  associate it with the authorized assessment/workflow. File selection never
  grants access to an assessment or changes its status.
- The public API authenticates the actor and routes the request to Wanshaja Sooriyabandara's
  service, which checks assessment scope and upload permission, validates the
  content type/count/size and file bytes, rejects malformed/unsafe content,
  and stores accepted bytes privately with
  assessment-linked metadata and audit history.
- Reviewers with access to that assessment can view evidence through an
  authorized public API response. Storage paths, public static links and
  storage-provider credentials never reach either client. An unavailable
  upload/storage dependency is reported clearly; the client must not claim
  that an image was attached when it was not.
- Evidence is immutable after it is included in a submitted assessment
  version. A later correction/addition is a separately authorized and audited
  supplemental evidence version, so a reviewer can identify exactly what was
  considered. Draft removal, submitted-version behavior and retention must
  follow the final Wanshaja Sooriyabandara (Member 4) lifecycle contract.
- The Wanshaja Sooriyabandara (Member 4) implementation contract must set the supported image formats,
  maximum number and size of files per assessment, private storage provider,
  scanning/sanitization method and retention/deletion schedule before code is
  accepted. Postgres stores searchable metadata and relationships, not image
  binaries. The ASP.NET Core service streams content to a private storage
  adapter and controls every read/write.
- Camera permission is requested only when the operator chooses capture. If
  camera access is denied or unavailable, the operator may select an image or
  continue without optional images. Keep relevant image metadata minimal and
  remove embedded location metadata unless the approved assessment contract
  explicitly requires it.

### Agentic AI boundary

Uploaded images are untrusted human evidence. The v1 Safety & Operations Agent
does not receive raw image bytes, direct storage access, file URLs or
unvalidated extracted text. Human reviewers inspect the authorized evidence.
Any later model vision/OCR capability requires a separate post-G07 tool/input
contract, data-minimization and threat review, deterministic validation and
evaluation; it is not implied by this upload scope.

Image evidence is limited to assessments of BLUEVERSE-managed operations. It
does not add general file sharing, user profile uploads, social content,
pollution reporting, environmental incident management, emergency response
or governmental beach-closure workflows, which remain outside v1.

## Cross-client acceptance evidence

The owning branch must demonstrate:

- Flutter GPS permission grant, denial, permanent denial/restriction, disabled
  service, timeout/unavailable and manual fallback; React geolocation support,
  permission/failure cases and manual fallback;
- the same authorized nearby discovery result and API request semantics from
  manual and current-location input;
- equivalent valid/invalid date, time, duration and time-zone behavior on
  React and Flutter for Adithya Gunawardana (Member 3) itinerary scheduling, including boundary and
  daylight-saving cases where the selected zone observes them;
- optional image selection/capture and authorized upload/review from both
  clients, with identical API validation and assessment state;
- rejection of unauthorized, oversized, disallowed, malformed or spoofed
  image content, storage outage, failed upload, duplicate/retry behavior and
  an audit trail that distinguishes draft, submitted and supplemental
  evidence; and
- proof that no location trail is retained, no attachment is publicly
  accessible, and no raw media reaches Agentic AI.

The current route and endpoint inventories continue to describe implemented
behavior only. Add the actual client routes, API operations and workflow
registry entries when the owning component branches implement them; do not
register target-only endpoints or routes in advance.

## Related contracts

- [Ushan Srinuka (Member 1) component and work plan](components/member-1-coastal-experience-biodiversity-discovery.md)
  · [Ushan Srinuka (Member 1) phases](phases/member-1-phase-plan.md)
- [Adithya Gunawardana (Member 3) component and work plan](components/member-3-smart-coastal-planner-itinerary-management.md)
  · [Adithya Gunawardana (Member 3) phases](phases/member-3-phase-plan.md)
- [Wanshaja Sooriyabandara (Member 4) component and work plan](components/member-4-coastal-operations-advisories-alerts.md)
  · [Wanshaja Sooriyabandara (Member 4) phases](phases/member-4-phase-plan.md)
- [Component relationships](component-relationships.md)
- [Member branch and integration workflow](member-branch-workflow.md)
- [Assessment evidence storage boundary](../adr/ADR-0018-assessment-evidence-storage-boundary.md)
- [Requirements coverage and readiness](requirements-coverage-and-readiness.md)
