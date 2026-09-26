# AI Usage Contribution Log

Each team member maintains one file at
`docs/ai-contribution/<GitHub-Username>-ai-usage.md`. The filename must use the
member's exact GitHub username. The shared identity mapping is maintained in
[`ai-team-members.md`](ai-team-members.md).

## Required record format

Add one entry for each meaningful AI-assisted implementation, documentation,
testing, design or review task:

```markdown
## YYYY-MM-DD — Short task title

- Date/time or time range: 2026-09-19 22:59–23:15 (Asia/Colombo)
- GitHub Username: `username`
- Team Member Name (actual): Full Real Name
- Agent Name: Agent name
- Tool/App: ChatGPT Codex
- AI Model: Model name
- Summary of the user's request: Concise paraphrase
- Summary of what the AI Agent did: Concise, factual summary
- AI output accepted/changed/rejected: State what was retained, revised or
  discarded after review; write `none` where a category does not apply.
- Verification/evidence: Checks performed and relevant files, tests, commit or PR
```

Use an ISO-like date/time and include the timezone when possible. If a task
spans multiple sessions, record the complete time range or separate entries.
Use the actual team member name supplied by the team, not a GitHub display name.

## Identity and history rules

1. Before updating a log, match the acting GitHub username and actual name to
   [`ai-team-members.md`](ai-team-members.md).
2. If the username or name is missing, mismatched or ambiguous, ask the user to
   confirm the account before editing any contribution file. Never guess.
3. Update only the acting member's file. Create it when it does not exist.
4. Do not rewrite earlier entries unless the user explicitly requests a
   historical correction. A request solely to correct, rewrite or delete an
   earlier contribution must not be logged as a new contribution.
5. Do not include passwords, access tokens, API keys, hidden model reasoning or
   confidential personal data.
6. The approximately one-page assessed individual AI reflection belongs in
   the student's Individual Report and must be written by that student. It
   addresses the tools used, strengths and mistakes of AI output, changes or
   rejections, and the student's own learning. Do not generate it with AI.

## Example

```markdown
## 2026-09-19 — Example documentation task

- Date/time or time range: 2026-09-19 22:59–23:05 (Asia/Colombo)
- GitHub Username: `WKS2004`
- Team Member Name (actual): Wanshaja Sooriyabandara
- Agent Name: Codex
- Tool/App: ChatGPT Codex
- AI Model: GPT-5
- Summary of the user's request: Describe the requested work.
- Summary of what the AI Agent did: Describe the implemented work.
- AI output accepted/changed/rejected: Describe the reviewed result and revisions.
- Verification/evidence: List checks and changed files.
```
