# ADR-0005: React State Management

**Status:** Accepted for the React Auth foundation

## Context

The React application is checked in. Its Auth experience needs predictable
form, request, loading, user and session state without a separate global
state library. The application can revisit state management as additional
workflows add cross-route coordination.

## Decision

Use React's built-in `useState` and `useEffect` for the implemented
Auth flow, with public API requests isolated in `apps/web/src/auth.ts`.
Keep permission and business decisions server-owned. Introduce a broader
state-management library only with a demonstrated product need and an ADR
update; do not add one merely because a new screen exists.

## Consequences

The current pattern is small and easy to test. Added workflows should keep
request adapters separate from presentation, handle loading/error/denied
states and preserve the shared React/Flutter API contract. See the
[v0 React component](../v0/components/react-web-client.md).
