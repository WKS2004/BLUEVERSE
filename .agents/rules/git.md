# Git rules

- Preserve user changes; inspect `git status --short` and the relevant diff
  before editing, and keep unrelated work out of the change.
- Use focused, explainable commits and the repository's branch/PR flow. Do not
  rewrite history, reset or discard files without explicit authorization.
- Never commit `.env`, credentials, tokens, generated build output, local IDE
  state, dependency caches or machine-specific configuration.
- Preserve traceable individual ownership through meaningful files, tests,
  review evidence and the acting member's AI-usage record.
- Before handoff, review the diff for accidental files, secrets, stale docs,
  missing tests and unreported validation failures.
