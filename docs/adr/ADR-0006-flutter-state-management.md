# ADR-0006: Flutter State Management

**Status:** Accepted for the Flutter Auth foundation

## Context

The Flutter application is checked in. Its Auth flow needs explicit
request, loading, user, session and error state across a small UI surface.
The current code separates presentation from repository and API transport.

## Decision

Use `ChangeNotifier` in `AuthViewModel` and `ListenableBuilder` in the
Auth screen, with `AuthRepository` and `AuthApiService` owning data access.
This fits the implemented Auth flow without adding a state library. Revisit
the choice through an ADR update if later workflows require broader
coordination; keep the same server-owned permissions and public API boundary.

## Consequences

The view model exposes loading, error, user and session state for widget
tests; repository and service boundaries allow mock transport and in-memory
credential tests. See the
[v0 Flutter component](../v0/components/flutter-client.md).
