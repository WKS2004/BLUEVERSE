# Git rules

- Preserve user changes; inspect `git status --short` and the relevant diff
  before editing, and keep unrelated work out of the change.
- Do not create commits or push branches automatically. Commit or push only
  when the user explicitly requests that action; an implementation request
  alone is not authorization. When authorized, use focused, explainable commits
  and the repository's branch/PR flow.
- This rule governs agent-initiated Git actions; it does not disable or prohibit
  repository-approved GitHub Actions. Preserve the existing `dev-backup`
  mistaken-commit rescue and synchronization workflow and the `.github`
  configuration sync workflow. Change or disable those workflows only when
  the user explicitly requests a workflow change.
- Do not rewrite history, reset or discard files without explicit authorization.
- Never commit `.env`, credentials, tokens, generated build output, local IDE
  state, dependency caches or machine-specific configuration.
- Preserve traceable individual ownership through meaningful files, tests,
  review evidence and the acting member's AI-usage record.
- Before handoff, review the diff for accidental files, secrets, stale docs,
  missing tests and unreported validation failures.
