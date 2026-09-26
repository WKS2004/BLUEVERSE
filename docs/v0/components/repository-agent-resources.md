# Repository and agent resources

## Responsibility and source

The repository structure, root [`AGENTS.md`](../../../AGENTS.md),
[`.agents/routing.md`](../../../.agents/routing.md), focused rules,
workflow skills and validators give humans and agents a reproducible
change path. [ADRs](../../adr/README.md) record material architecture
decisions. [Project documentation](../../project/) records scope,
contribution ownership and AI use. These resources are part of the
foundation; they do not replace inspection of source and tests.

## Working contract

Start with the requested scope and `git status --short`. Select the
applicable rules through the routing matrix; read the owning requirement
and component documentation for the task. Keep changes in their owning
package, preserve public API, permission and database boundaries, add
tests with behavior changes and update docs and ADRs when the contract
changes. Do not generate replacement sample applications or infer that a
documented target is executable software.

The [route catalog](../../api/endpoint-catalog.md) is the first stop for
endpoint lookup; the [UI registry](../../contracts/ui-integration.json)
is the shared workflow contract. Agent guidance describes universal
architecture and process. Release-specific details belong in the
requirements and release documents selected for the task.

## Evidence and contribution

The repository validator checks agent-resource structure, skills, links
and routing. CI checks source, tests, contracts and local-stack behavior
with separate workflows. Record each AI-assisted contribution in the
acting member's [usage log](../../project/ai-usage-log-template.md) after
confirming the account mapping. Do not rewrite historical records or
record secrets, tokens or hidden reasoning.

Use the [agent resource guide](../../development/agent-resources.md),
[Git workflow](../../development/git-workflow.md) and
[verification map](../integration-and-acceptance.md) for operational
details.
