# Architecture Security Baseline

This is the intended security baseline. The public API foundation is checked
in at `services/api`, but Auth and the protected domain workflows are not yet
implemented. JWT, permission policies, password hashing and audit behavior
therefore remain acceptance criteria rather than currently verifiable behavior.

- TLS terminates at the appropriate deployed edge/platform boundary.
- JWT authentication protects secured API endpoints.
- Authorization is permission-based.
- Passwords are hashed by the auth/authentication implementation.
- Secrets are provided through environment/secret management.
- PostgreSQL is published on local host port `5432` for pgAdmin4 only; production database access remains private.
- Agent tools are internal and explicitly authorized.
- Critical business actions require deterministic validation.
- High-impact actions use human approval.
- AI output is treated as untrusted input.
