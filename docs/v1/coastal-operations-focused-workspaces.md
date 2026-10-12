# Coastal Operations focused workspaces and quiet search

The [2026-10-02 record navigation and audit follow-up](coastal-operations-record-navigation-and-audit.md) is the latest behavior contract. It supersedes the two-second search interval below and the one-second interval in the Logs/evidence follow-up with 500 ms search across all Coastal Operations collections. Shared gates remain Pending.

Date: 2026-10-01. Owner: Wanshaja Sooriyabandara (`WKS2004`).
User-directed follow-up recorded before implementation; shared G00/G07 gates
and all existing permission, publication and data rules remain in effect.

## List composition in both clients

Use distinct newly generated coastal photographs for Assessment and Alert
heroes. Below the descriptive hero, put OPERATIONS WORKSPACE, the Assessment/
Alert title, its description and permitted creation/refresh actions. Merge
search with the record section rather than giving it a separate large panel.
Retain “Find your coastal reviews/updates” and lifecycle tabs directly above
the compact search bar. Records are the primary content; omit duplicate
collection headings/descriptions. Account navigation provides sibling access.

The bar contains the section-specific title-search placeholder, a Search icon
on the right, a Filters icon/disclosure and a Reset filters icon. Icons have
accessible names, tooltips and comfortable touch targets. All record types
remains the default. Filters/Advanced ID inputs live in the collapsed panel.

## Quiet live search

Fetch after **2 seconds since the last title input**, including clearing it.
Cancel the previous timer on each change, reset and unmount. Enter/Search icon
can apply immediately. Lifecycle, select-filter and reset actions apply without
a Search click. Validate Advanced IDs before a request; partial/malformed IDs
must not reach the API. Prevent duplicate submissions for the same query.

Search/filter/list refresh uses a small animated loading indicator inside the
bar, not the shared blocking loading screen. This explicit user instruction
overrides the general client loading-screen guidance for these collection
reads only. Keep entered text usable and existing cards visible during fetch,
announce busy status accessibly, ignore stale responses and reset pagination
on applied filter changes. Failures retain results with a retry message;
successful empty searches use the existing calm empty state. Other Auth and
mutation loading behavior remains governed by its existing workflow.

## Focused creation and detail

Keep the shared header, footer and account navigation. Creation/editing or
opening either record replaces the entire remaining workspace with its form
or detail; no hero, search, collection or unrelated record is displayed beside
it. Provide an arrow icon and “Back to Assessments/Alerts” at the top left.
Preserve list filters/results when returning, focus the new workspace heading
and return keyboard focus to the initiating control where it remains present.

These are views within the existing registered `/operations/assessments` and
`/operations/alerts` workflows, not new API routes. Assessment detail uses the
existing scoped detail/status/evidence/audit contract. Alert detail uses the
authorized list item plus a recordId-scoped refresh through the existing alert
collection; do not invent an unimplemented alert-detail API. Resource denial
or disappearance is a recoverable detail error, not permission to reuse data
from a different account. Account/permission changes reset focused state.
Actions remain granted by the same permission checks in both clients.

## Assets and evidence

Persist both final generated assets in the web and Flutter asset directories.
Record built-in imagegen prompts/provenance and saved paths with AI-Usage.
Use descriptive alt text/semantics; natural photos illustrate coastal tasks
and are not actual assessment evidence or records.

Update affected tests only with approval. Record build/analyzer, route/UI
validation, approved regression results and responsive browser review after
implementation; preserve provider/integration limitations from the record
experience contract.

### Generated hero provenance

Both photographs were produced with the built-in ImageGen tool on 2026-10-01,
using `photorealistic-natural` prompts, then copied unchanged into both clients.
The tool did not report a specific image model name. These illustrative assets
are separate from uploaded assessment evidence; original PNGs are about 2.6 MB
apiece. No external image attribution or stock-photo claim is implied.

Assessment saved paths:
- `apps/web/src/assets/coastal/assessment-hero.png`
- `apps/mobile/assets/coastal/onboarding/assessment-hero.png`

Final assessment prompt:
> Use case: photorealistic-natural. Asset type: wide photographic hero for BLUEVERSE coastal Assessments workspace. Create a believable editorial photograph on a tropical Sri Lankan shoreline: two coastal field workers thoughtfully inspecting a beach access path and dune vegetation after rain, with a small clipboard and no conspicuous technology. Wide landscape composition, people and path mostly in the right half, calm sea and soft uncluttered darker blue-green space to the left for white overlay headings. Natural overcast morning light, sea-glass teal, deep ocean blues, warm sand, authentic natural textures. Calm coastal care, no dramatic disaster. No text, logos, UI, watermarks, charts or readable signs. One single landscape photograph.

Alert saved paths:
- `apps/web/src/assets/coastal/alerts-hero.png`
- `apps/mobile/assets/coastal/onboarding/alerts-hero.png`

Final alert prompt:
> Use case: photorealistic-natural. Asset type: wide photographic hero for BLUEVERSE coastal Alerts and Advisories workspace. Create a believable editorial photograph of a tropical Sri Lankan beach entrance and boardwalk with a coastal steward guiding a small family toward a clearly maintained safe path; gentle ocean surf visible beyond, modest palms and dunes. Wide landscape composition, people and entrance in the right half with uncluttered deep blue-green coastal space on the left for white overlay headings. Soft afternoon natural light, ocean blue and sea-glass teal with warm sand. Reassuring, clear coastal guidance, no emergency spectacle. No text, logos, UI, watermarks, charts or readable signs. One single landscape photograph, visually distinct from an inspection scene.

## Current implementation and verification

The React and Flutter source implement the requested focused views and quiet
collection search. Hidden/offstage collections retain search state; pending
search timers stop while a focused view is open. Request cancellation/generation
and account/grant boundaries prevent old responses from replacing newer lists.
Alert detail refreshes its ID through the registered, scoped collection route;
unavailable/error outcomes do not display a previous record as current.

- TypeScript compilation, the Vite production build and changed-source ESLint
  passed. Flutter formatting and full static analysis passed without issues.
- Endpoint catalogue regeneration/validation and UI integration validation
  passed: 56 public endpoints, 26 frontend routes; no implemented AI endpoint.
- Synthetic browser review checked both heroes, compact bar/filter disclosure,
  automatic results without a Search click, reset, focused creation/details,
  Back navigation and retained chrome at desktop and 390-pixel viewport widths.
  Document horizontal overflow was absent in the checked phone views. A first
  malformed preview fixture omitted required detail arrays; correcting the
  fixture allowed the complete detail review. Preview files/server were removed.
- The original implementation awaited test-change approval. Approval arrived
  on 2026-10-02; the full-workspace recheck, regression corrections and current
  one-second search results are recorded in the
  [Logs/evidence follow-up](coastal-operations-logs-and-evidence.md). Earlier
  task counts remain historical evidence. Live provider/Docker/real-device
  acceptance is still not claimed.

### Regression acceptance to record after approval

Cover two-second timing after the last character, clearing/rapid changes,
Enter/icon immediate submission and deduplication, lifecycle/dropdown/reset,
Advanced malformed/partial IDs even with collapsed filters, pending requests,
stale list/pagination responses and dependency failure/retry while cards remain.
Cover both authorized sections and denied grants, account/permission changes,
whole-workspace creation/edit/detail with retained chrome, Back preserving search
and focus, absent/revoked records, and distinct image assets on both clients.
