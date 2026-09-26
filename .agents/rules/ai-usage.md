# AI Usage Contribution Rules

These rules apply whenever an AI agent assists with a repository task.

## Record location and fields

- Update only the acting member's
  `docs/ai-contribution/<GitHub-Username>-ai-usage.md` file.
- Use the exact GitHub username in the filename and the actual team member name
  in the record; do not substitute a GitHub display name for the real name.
- Every new meaningful task record must include the date/time or time range,
  timezone, GitHub username, actual team member name, agent name, tool/app, AI
  model, user-request summary, agent-action summary, what AI output was
  accepted, changed or rejected, and verification/evidence.
- Use the format in `docs/project/ai-usage-log-template.md`.
- The assessed individual AI reflection is the student's own writing; an
  agent must not author it.

## Identity verification

Before editing a contribution file, compare the acting account with
`docs/project/ai-team-members.md`. If the username or actual name is not known,
does not match, or could refer to more than one member, ask the user to confirm
the account. Do not infer identity from a name, email address or unverified
context.

## Historical records

Preserve existing entries. Do not change, delete or rewrite an earlier entry
unless the user explicitly asks for that historical change. A request whose
sole purpose is changing an earlier contribution record is not a new AI usage
event and must not be logged.

## Privacy and accuracy

Never record secrets, credentials, tokens, hidden model reasoning or
confidential personal data. Summarize the request and agent work factually, and
record meaningful verification such as tests, review, structural checks or
changed files.

Add the entry only after validation so the evidence is accurate. Do not infer
the acting account from a repository owner, display name or email alone; the
username and actual name must match `docs/project/ai-team-members.md`.
