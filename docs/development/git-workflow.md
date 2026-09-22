# Git Workflow

## Branches

`main` is the stable release branch. `dev` is the shared integration branch;
changes normally enter it through reviewed pull requests from an active work
branch. `main` should receive finalized changes from `dev` through a reviewed
pull request.

The repository naming policy accepts these lowercase branch families:

- `features/<feature-name>` — normal product work
- `agentic-ai/<name>` — Agentic AI implementation and evaluation work
- `maintenance/<name>` — maintenance work
- `bug-fixes/<name>` — defect-fix work
- `claude/<name>`, `codex/<name>`, `antigravity/<name>`, `gemini/<name>` —
  tool- or team-specific work branches

The following names are reserved for repository automation or foundation
state: `main`, `dev`, `dev-backup`, `default-template`,
`github-sync/<target>/<run>`, `docker-workflow-changes/<target>/<timestamp>`
and `dev-backup-mistaken-commits/<actor>/<timestamp>`.

`branch-policy.yml` checks a branch immediately after creation. Branch names
must be fully lowercase and match an approved name or family; an invalid branch
is deleted by the workflow. A workflow cannot stop the original Git push after
the Git server accepts it, so required branch rules remain necessary for
enforcement.

## Pull requests and required checks

The normal flow is:

```text
features/**, maintenance/**, bug-fixes/**, agentic-ai/**
                           │
                           └── reviewed pull request → dev
                                                               │
                                                               └── reviewed pull request → main
```

Configure GitHub rulesets or branch protection so that:

- direct pushes to `main` are blocked;
- pull requests into `main` require `dev` as the source policy used by the
  team, required reviews and all required status checks;
- pull requests into `dev` require review and the relevant source checks;
- force-push and deletion are disabled for `main`, `dev` and `dev-backup`;
- the `dev-backup` maintenance workflow is allowed to update `dev-backup`, or
  its repository rule explicitly grants the workflow token a bypass; and
- GitHub Actions is allowed to create and queue the `.github` synchronization
  pull requests if that automation is enabled.

GitHub Actions status checks report whether a commit passed; they do not
cancel an already accepted push. Required checks in a ruleset are the control
that prevents a pull request from merging when a workflow fails.

## Path-aware checks

Source and test workflows run for `main`, `dev` and the supported development
branch families. `main` and `dev` always execute their relevant checks. On an
active work branch, each workflow determines whether its paths changed and
prints a successful `Not affected` result when the workflow does not apply.
This keeps the check names visible without spending time on unrelated suites.

The special automation branches are not included in the source/test workflow
branch filters. Their jobs are driven only by their own automation workflows.

## GitHub configuration synchronization

`github-config-sync.yml` watches pushes that change `.github/**`. For every
durable development branch except `main` and `dev-backup`, it creates a
temporary `github-sync/<target>/<run-id>` branch containing only the `.github`
folder, opens a pull request, queues automatic squash merging and requests
branch cleanup after the merge. Temporary recovery/automation branches are
excluded as sources and targets. The source and target application/service
files are not copied.

The workflow skips the standard synchronization commit subject to avoid a
second propagation wave after a sync PR is merged. If a repository rule does
not permit automatic merging, the PR remains available for a collaborator and
the workflow reports the target as failed instead of silently discarding it.

## Dev backup

`dev-backup.yml` runs only for `dev` and `dev-backup` pushes. It creates
`dev-backup` from `dev` when the backup does not exist. When an unwanted commit
lands on `dev-backup`, the workflow preserves the complete backup history on a
timestamped `dev-backup-mistaken-commits/<actor>/<timestamp>` branch using an
explicit merge commit, then synchronizes `dev-backup` to the exact `dev` SHA.
The timestamp is generated in `Asia/Colombo` (`GMT+05:30`) with millisecond
precision.

The final synchronization uses a force-with-lease update so an unexpected
concurrent update is not overwritten. The repository rule for `dev-backup`
must permit that bot update; otherwise the workflow intentionally fails with a
configuration message and leaves the branch unchanged.

## Pull request checklist

Every substantial pull request should state:

- what changed and why;
- the source and target branch flow;
- the exact tests, checks and test IDs run;
- API, database, UI contract, Docker and security impact; and
- documentation or ADR updates required by the change.

The pull request template captures the shared UI workflow ID, React and
Flutter routes, public API endpoint references, endpoint-catalog updates and
the final failed-test-case evidence when those boundaries are affected.
