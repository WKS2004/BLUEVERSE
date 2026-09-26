# ADR-0016: Equal client capability for all roles

**Status:** Accepted

## Context

The v1 requirements describe different React and Flutter screens and use
Flutter initiation with React review in the assessed workflow. That example
must not be interpreted as ownership of management by React or tourism and
field work by Flutter. The team requires every stakeholder to be able to
use either application without losing a permitted business capability.
ADR-0015 established co-equal client surfaces but still described possible
platform emphases and exceptions.

## Options considered

1. Assign stakeholder groups or workflows to one frontend.
2. Give each frontend a different priority while retaining partial overlap.
3. Provide every permitted role, workflow and business action in both
   clients through one public API and permission contract.

## Decision

Use option 3. React Web and Flutter Mobile have equal business capability
coverage for tourists, coastal operators, operations reviewers and platform
administrators, and for future stakeholder roles. Neither client owns or
prioritizes a role, component, workflow, action or approval. Each workflow
has both client routes and the same authoritative ASP.NET Core API,
business data, role-to-permission rules and observable outcome.

Responsive layout, navigation, keyboard/touch input and device features
can differ. Flutter GPS is a v1 device feature; React still offers the
same location-aware discovery workflow through a suitable input. The
canonical Flutter-to-React assessment is an evaluation sequence, not a
restriction on initiation or review in the other client.

This decision supersedes ADR-0015's platform-emphasis and exception
language while preserving its shared public API and user-friendly
experience principles.

## Consequences

- Every participating role and action needs a usable React and Flutter
  experience, a shared workflow ID, public API references and permission
  behavior.
- Client tests must exercise the same permitted and denied business paths
  in both applications; device-specific interactions are additional cases.
- The shared registry and endpoint catalog must be synchronized with
  implementation. Requirements examples do not become live routes merely
  by appearing in documentation.
- Delivery effort increases because both client surfaces must be complete,
  but users can choose either without role-based loss of capability.
