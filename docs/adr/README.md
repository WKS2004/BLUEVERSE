# Architecture Decision Records

ADRs capture significant architectural decisions and their rationale.

## Initial ADR set

1. `ADR-0001-microservices-and-service-boundaries.md`
2. `ADR-0002-docker-hardened-images.md`
3. `ADR-0003-edge-nginx.md`
4. `ADR-0004-permission-based-authorization.md`
5. `ADR-0005-react-state-management.md`
6. `ADR-0006-flutter-state-management.md`
7. `ADR-0007-agentic-ai-framework.md`
8. `ADR-0008-agent-workflow-state.md`
9. `ADR-0009-cloud-deployment.md`
10. `ADR-0010-cross-client-ui-integration-contract.md`
11. `ADR-0011-authentication-service-boundary.md`
12. `ADR-0012-multi-account-device-sessions.md`
13. `ADR-0013-auth-token-lifecycle.md`
14. `ADR-0014-active-session-archive.md`
15. `ADR-0015-cross-platform-role-coverage-and-ui-experience.md`
16. `ADR-0016-equal-client-capability-for-all-roles.md`
17. `ADR-0017-map-provider-integration-boundary.md`
18. `ADR-0018-assessment-evidence-storage-boundary.md`
19. `ADR-0019-biodiversity-inference-integration-ownership.md`
20. `ADR-0020-member-component-service-boundaries.md`

Some decisions remain **Proposed / Pending implementation choice** because the foundation does not invent decisions that have not yet been made.

ADR-0016 is the current equal-client-capability decision. ADR-0015 remains
available as historical context and is superseded.

ADR-0017 assigns the v1 map-provider adapter to Ushan Srinuka (Member 1) and preserves the
ASP.NET Core boundary. Its provider and exact feature scope remain open.

ADR-0018 assigns optional operational-assessment image evidence to Wanshaja Sooriyabandara (Member 4)
and keeps its upload/retrieval path private and API-mediated. The storage
provider, limits and retention/sanitization details remain pre-implementation
decisions.

ADR-0019 assigns the BLUEVERSE IT3091 biodiversity inference adapter and
validated public result contract to Adithya Gunawardana (Member 3), while Ushan Srinuka (Member 1) owns
experience-facing consumption. The separate ML service is ordinary member
feature work before G07; the future agent tool uses the Adithya Gunawardana (Member 3) contract only
after G07. Sanuda Abeysinghe's condition period remains ordinary query input with no
separate device-feature assignment.

ADR-0020 assigns one internal .NET microservice under `services/` to each v1
member component. The existing API is integration-only for these services,
Auth behavior is preserved, and the exact internal contracts are agreed at
G00.
