# Security Checklist

Use this checklist as implementation evidence. An unchecked item is not
implemented or has not yet been verified in the current repository.

## Repository

- [ ] `.env` ignored
- [ ] secrets absent from commits
- [ ] no credentials in Dockerfiles
- [ ] no private data in test fixtures

## API

- [ ] JWT authentication
- [ ] permission-based authorization
- [ ] validation
- [ ] secure CORS
- [ ] structured error handling
- [ ] rate/abuse controls considered where appropriate

## Database

- [ ] restricted network exposure
- [ ] migration-based schema changes
- [ ] least-privilege credentials
- [ ] no plaintext passwords/tokens

## Agentic AI

- [ ] tool authorization
- [ ] deterministic validation
- [ ] prompt-injection defenses
- [ ] human approval for high-impact actions
- [ ] no hidden reasoning persistence
- [ ] safe failure
