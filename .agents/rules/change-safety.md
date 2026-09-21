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
- Treat `.agents` rules and skills as agent guidance. Ordinary feature or
  endpoint work updates application sources and `docs/api/endpoint-catalog.*`,
  not the guidance itself. Edit `.agents` guidance only when the user asks for
  agent-resource changes or explicitly approves a necessary guidance change.

## Mandatory endpoint-documentation gate

Treat endpoint documentation as part of the implementation, not as optional
follow-up work. This applies to every addition, update, rename, move or
removal of a frontend route, client API target, ASP.NET controller or minimal
API route, health route, OpenAPI/Swagger route, gateway/YARP/Nginx mapping,
public or internal service endpoint, test-only fixture endpoint, or Agentic AI
endpoint.

For every such change, the agent must:

1. Read `docs/api/endpoint-catalog.md` before editing the route source.
2. Update `docs/api/endpoint-catalog.json` in the same change with the exact
   method/path, source, owner, public boundary, authorization, purpose and
   usage. Remove entries for removed routes; do not leave stale or invented
   entries.
3. Update `docs/contracts/ui-integration.json` when a registered React or
   Flutter workflow or its public API references are affected.
4. Regenerate the readable catalog with
   `python .agents/scripts/validate_endpoint_catalog.py --write-markdown`.
5. Run `python .agents/scripts/validate_endpoint_catalog.py`; also run
   `python scripts/validation/validate_ui_integrations.py` and its tests when
   a client workflow, frontend route or client request target is affected.

An endpoint change is incomplete until these checks pass. Do not claim
completion while the source/catalog validator reports drift, stale Markdown,
missing ownership or authorization metadata, an undocumented route, or an
unsupported route form. Routine endpoint work updates `docs/api/`; it does
not rewrite `.agents` guidance.

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
