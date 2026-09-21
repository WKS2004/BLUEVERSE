# Security Checklist

Use this checklist as implementation evidence. An unchecked item is not
implemented or has not yet been verified in the current repository.

## Repository

- [x] `.env` ignored
- [x] secrets absent from commits
- [x] no credentials in Dockerfiles
- [x] no private data in test fixtures

## API

- [x] JWT authentication with required 32-byte minimum signing key
- [x] permission-based authorization with system-role escalation protection
- [x] persisted device sessions with a five-account-per-device limit and scoped logout
- [x] active sessions separated from ended-session lifecycle logs; archived rows cannot authenticate
- [x] server-issued device credentials with hashed keys and secure web/native transport
- [x] rotating hashed refresh tokens with replay revocation and one/30-day absolute expiry
- [x] password changes isolated to `POST /api/auth/change-password`; profile updates do not accept password fields
- [x] validation
- [x] secure CORS
- [x] structured error handling
- [ ] rate/abuse controls considered where appropriate

## Database

- [x] restricted network exposure for internal Auth/database networks
- [x] migration-based Auth schema changes
- [ ] least-privilege credentials
- [x] no plaintext passwords/tokens

## Agentic AI

- [ ] tool authorization
- [ ] deterministic validation
- [ ] prompt-injection defenses
- [ ] human approval for high-impact actions
- [ ] no hidden reasoning persistence
- [ ] safe failure
