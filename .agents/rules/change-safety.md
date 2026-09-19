# Change safety and agent efficiency

Apply this rule to every implementation task.

## Scope control

- Start with `git status --short`, then inspect only relevant files and their
  direct references. Use `rg --files`/`rg` and targeted reads; do not dump the
  whole repository into context.
- Classify the task with `routing.md`, read the smallest applicable rule set,
  and avoid rereading unchanged documents. Prefer parallel read-only inspection.
- Keep the patch minimal but complete. Do not refactor unrelated code, add
  speculative abstractions, duplicate source-of-truth content or create sample
  applications to compensate for missing implementation.
- Preserve user edits and unknown files. Before a destructive or broad action,
  resolve exact targets and obtain explicit authorization when required.

## Quality gates

- Define observable behavior and affected boundaries before editing.
- Validate the normal path plus applicable rejection, boundary, dependency,
  authorization, concurrency, retry and failure cases.
- Run the narrowest useful checks first, then broader routed checks; report
  unavailable tools, missing services and expected foundation failures instead
  of masking them.
- Review `git diff --check`, changed-file scope, secret exposure, docs/tests,
  and final status before handoff.

## Stop conditions

Ask the user before changing/deleting/relaxing existing tests, changing a
public contract or architecture without a clear requirement, handling unclear
destructive scope, bypassing approval/security controls, or writing an AI log
when identity cannot be matched exactly.
