# Documentation rules

- Keep setup, CI, deployment and operational instructions executable against
  the current checkout; state prerequisites and known foundation gaps.
- Update the narrowest relevant document when behavior, commands, paths,
  boundaries, workflows or ownership change. Avoid duplicating conflicting
  instructions across README, docs and `.agents`.
- Add/update an ADR for a material architecture, service-boundary, framework,
  state-management, data or deployment decision.
- Distinguish implemented behavior from target architecture. Do not document a
  reserved service, future workflow or planned test suite as available.
- Prefer links to detailed source-of-truth docs over copying long matrices into
  agent rules; verify links and commands during validation.
