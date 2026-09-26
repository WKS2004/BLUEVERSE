# v1 Component Owners and AI Usage Accounts

This is the canonical mapping for v1 component ownership, feature branches,
Agentic AI branches and individual AI usage logs. GitHub usernames are exact
account names; the full-name column records the member's actual name. The
`Member 1` through `Member 4` identifiers are retained as stable requirement
trace labels from `PROJECT_REQUIREMENTS.md`; they are not the preferred owner
names in operational documentation.

| Component member | Component | GitHub username | Full name (actual) | Feature branch | Agentic AI branch | AI usage log |
|---|---|---|---|---|---|---|
| Member 1 | Coastal Experience & Biodiversity Discovery | `Ushan-Srinuka` | Ushan Srinuka | `features/experience-biodiversity` | `agentic-ai/experience-biodiversity` | `Ushan-Srinuka-ai-usage.md` |
| Member 2 | Marine Conditions & Safety Intelligence | `sanudaabey` | Sanuda Abeysinghe | `features/marine-safety` | `agentic-ai/marine-conditions` | `sanudaabey-ai-usage.md` |
| Member 3 | Smart Coastal Planner & Itinerary Management | `AdithyaGunawardana` | Adithya Gunawardana | `features/coastal-planner` | `agentic-ai/planning-coordination` | `AdithyaGunawardana-ai-usage.md` |
| Member 4 | Coastal Operations, Advisories & Alerts | `WKS2004` | Wanshaja Sooriyabandara | `features/coastal-operations` | `agentic-ai/safety-operations` | [`WKS2004-ai-usage.md`](../ai-contribution/WKS2004-ai-usage.md) |

## Usage rules

1. Use the full name and exact GitHub username in owner assignments, branch
   trackers, AI role allocations and contribution records. Keep the stable
   `Member N` identifier where it is needed to trace frozen requirements.
2. Each feature branch contains that member's complete business component.
   All four feature branches can be developed at the same time after G00.
3. The corresponding `agentic-ai/**` branch is for that member's actual
   Agentic AI role and starts only after all four components pass G07.
4. Before writing an AI usage record, match the acting account and actual name
   against this table. If either value is missing or ambiguous, ask the user;
   do not infer identity from an email address or display name.
5. Keep one log file per exact GitHub username under
   `docs/ai-contribution/`. Create the other three log files only when those
   members have a meaningful AI-assisted contribution to record.
6. Do not include credentials, hidden model reasoning or confidential personal
   information in a log. Each student's assessed reflection is written by
   that student.
