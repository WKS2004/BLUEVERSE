# BLUEVERSE UI Experience Principles

## Purpose

BLUEVERSE should feel like a believable coastal tourism and marine-resilience
product that helps people complete useful work. The interface is not a
technical demonstration, an analytics wall or a proxy for the internal
architecture.

These principles apply to React Web and Flutter Mobile, and to every role that
uses a workflow: clients and tourists, staff and operations teams, and
administrators or reviewers.

## Cross-platform product rule

React Web and Flutter Mobile are equal product surfaces. Every permitted
workflow for every participating role has a route and usable experience in
both clients, tied to one workflow ID, one public API contract and the same
role → permission behavior. Neither client receives a stakeholder, workflow
or business-action priority. Screen size, navigation, keyboard/touch input
and device features may change how a task is presented without removing
the task or changing its outcome. Flutter GPS is a mobile input method; an
equivalent location-aware workflow remains usable in React.

### Visual direction and platform styling

The shared visual direction is coastal editorial: light paper and sea-glass
surfaces, ocean-blue actions, restrained teal, natural coastal photography,
spacious and varied layouts, and calm motion. The detailed, source-grounded
reference is the [BLUEVERSE Design System](../../DESIGN.md).
Apply its palette, type hierarchy, image treatment, component character and
interaction states to both clients. React implements them with Tailwind and
shared web components; Flutter maps them into native widgets and `ThemeData`.
Layouts can adapt by screen and platform. Visual styling never changes the
shared role, permission, workflow or business-action requirements.

## Experience standards

### User-friendly

- Put the user's goal and next action before implementation detail.
- Use plain, respectful language and familiar domain terms.
- Make loading, empty, success, validation, denied, offline, timeout and
  dependency-failure states understandable and recoverable.
- Keep forms short, preserve safe drafts where appropriate and explain what
  will happen before a consequential action.
- Support readable type, keyboard or touch operation, visible focus, useful
  labels and accessible status/error announcements.

### Scope-aligned

- Every screen maps to a real BLUEVERSE workflow, role and permission.
- The content should relate to coastal tourism, marine conditions, safety,
  environmental resilience or coastal livelihoods as appropriate to the phase.
- Do not add generic productivity screens, invented capabilities or decorative
  analytics that do not help the current workflow.
- Keep business rules and authorization on the public API; the UI can guide and
  explain permissions but cannot replace server enforcement.

### Realistic

- Use believable names, places, statuses, dates, units and action labels.
- Show the context needed for a decision instead of isolated numbers or empty
  cards.
- Label fixtures, mock content and simulated AI output as demonstration data.
- Represent uncertainty honestly and explain the practical implication of a
  recommendation when an AI workflow is introduced.
- Design complete paths: discover or create, review, confirm, recover from
  error and see the resulting status.

### Calm rather than technical or analytical by default

Avoid making the following the dominant experience unless the user's job
specifically requires them:

- raw JSON, route names, service names or infrastructure terminology;
- dense metric grids, unexplained charts or “control room” decoration;
- model internals, confidence scores or execution traces without useful
  context;
- generic placeholder cards that imply unfinished product scope; and
- color, motion or alerts that create urgency without a clear action.

Technical detail may be available progressively for authorized staff or
administrators when it supports a real task, but it should be explained in
plain language and kept secondary to the decision or action.

## Delivery checklist

Before a UI workflow is considered ready, confirm:

1. It has one shared workflow ID, a React route, a Flutter route and every
   public API reference in `docs/contracts/ui-integration.json`.
2. The participating roles and required permissions are explicit, and the
   server remains authoritative.
3. Every permitted role and action in the workflow is usable in both clients;
   platform-specific input or layout does not reduce capability coverage.
4. Normal, loading, empty, validation, denied, malformed, timeout/retry and
   dependency-failure states are covered where relevant.
5. Copy, sample data and visuals feel credible for the current BLUEVERSE phase
   and do not present internal implementation as the product.
6. Accessibility, responsive/adaptive layout and safe recovery have been
   checked on the intended web and mobile targets.

The integration contract and this experience standard complement each other:
the contract proves that the UI is connected to the correct system boundary;
these principles ensure that the connected UI is useful and believable.
