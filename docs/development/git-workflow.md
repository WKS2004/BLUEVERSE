# Git Workflow

## Branches

- `main` — stable integration branch
- `dev` — active development integration branch
- feature branches — focused work

## Pull requests

Every substantial change should be reviewed through a pull request.

Repository/source CI runs for pushes to and pull requests targeting `main`,
`dev` and `features/**`. Docker image CI runs for pushes to and pull requests
targeting `main` and `dev`. The `main` branch always runs the web and backend
image builds. On `dev`, those builds are scoped to relevant application,
service, Docker infrastructure and workflow changes. The Compose stack health
workflow runs after both Docker build workflows complete successfully for the
same commit.

A PR should explain:

- what changed
- why it changed
- validation performed
- architecture impact
- database/API/client impact

## Individual contribution

Because SE3090 assesses individual technical ownership, preserve meaningful Git history, reviews, issues and test evidence.
