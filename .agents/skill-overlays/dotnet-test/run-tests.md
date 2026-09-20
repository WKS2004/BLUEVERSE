---
core: dotnet-test/run-tests
binding-revision: "1"
mode: extend
---

# BLUEVERSE .NET test runner overlay

- Read `AGENTS.md`, `.agents/routing.md`, `.agents/rules/testing.md` and
  `.agents/rules/validation.md` before selecting a command.
- Use the SDK pinned by `global.json` (`10.0.400`) and discover an actual
  `.sln`, `.slnx`, `.csproj` or test project before running `dotnet test`.
- Treat `services/api` and `services/auth` as unimplemented until tracked source
  projects exist. Ignored `bin/` and `obj/` output cannot justify a test
  command or a success claim.
- Prefer the smallest relevant project/filter. Do not restore, build or execute
  a nonexistent suite, and do not add test packages merely to make a command
  work.
- Authoritative test paths are under `test/`; preserve stable IDs, complete
  observable assertions, failure evidence and the permission requirement for
  changing existing tests.
- Report missing SDKs, projects, services, credentials or platform support as
  blockers rather than substituting another runner.
