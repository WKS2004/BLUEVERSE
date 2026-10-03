# Architecture Security Baseline

This is the current security baseline. The public API gateway, internal Auth
service and Member 2's marine-safety service are implemented at
`services/api`, `services/auth` and `services/marine-safety`. Auth issues
signed JWTs and owns identity/session data; the gateway validates issuer,
audience and signing key. Auth-protected operations use role-derived permission
policies and revalidate active accounts, token versions and device sessions
against PostgreSQL. Marine-safety accepts bearer or selected-account browser
cookie tokens, checks the current active Auth user/session, and resolves
`marine.profile.read` / `marine.profile.manage` against current role grants
rather than trusting JWT permission claims. These marine SQL reads are
parameterized and read-only. The marine test host uses deterministic
identity/permission doubles, so PostgreSQL-backed verification of those
queries remains open.

- TLS terminates at the appropriate deployed edge/platform boundary.
- JWT authentication protects secured API endpoints and rejects algorithms
  other than HS256. Access JWTs are short-lived (15 minutes); refresh tokens
  rotate and are stored only as hashes.
- Authorization is permission-based; system roles and the system-role
  management permission cannot be granted by ordinary role managers.
- Passwords are salted PBKDF2-SHA512 hashes and are never returned in DTOs.
- Secrets are provided through environment/secret management.
- `JWT_SIGNING_KEY` is required and must be at least 32 UTF-8 bytes; no source
  fallback key is accepted.
- User, role and permission changes, password changes and logout from every
  device increment token versions so Auth-issued tokens are revoked
  account-wide for Auth-protected requests.
- Device-scoped logout archives the persisted active-session records for one
  device. The account-specific device route archives only the selected
  account’s active session on that device; ended records remain in
  `UserSessionLogs` for audit and are never used for authorization.
- Auth generates an opaque installation ID and a separate proof key when the
  client has no installation credentials. The ID is not a MAC address,
  hardware fingerprint, IP address or security boundary. Browser clients use
  `HttpOnly` protected cookies; native clients use platform secure storage.
- Auth limits each installation to five active account sessions and each
  account to five active sessions. The sixth account login is rejected; the
  sixth account session revokes the oldest active session.
- Session lifetime is absolute: one day by default or 30 days with
  `rememberMe`. Refreshing does not extend that lifetime. Expired sessions and
  refresh tokens are archived/revoked by request checks and periodic cleanup;
  `ActiveSessions` contains only the operational session set.
- Development Compose publishes PostgreSQL as `5432:5432` on all host
  interfaces. Use it only on a trusted development network with the local
  database secret and host firewall. Promotion from `dev` to `main` changes
  this to `127.0.0.1:5432:5432` for loopback-only host access; both mappings
  still use host port `5432`, so neither resolves a local port collision.
  Production database access remains privately managed.
- Agent tools are internal and explicitly authorized.
- Critical business actions require deterministic validation.
- High-impact actions use human approval.
- AI output is treated as untrusted input.
