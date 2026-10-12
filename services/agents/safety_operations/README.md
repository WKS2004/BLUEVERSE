# Safety & Operations agent core

This package implements the first provider-independent slice of the
Wanshaja Sooriyabandara (Member 4) Safety & Operations proposal role.

It validates a bounded, versioned request containing the workflow identity,
target, period, policy allowlist, and current planner, experience, marine,
operations, and safety-profile reports. It rejects raw image fields and
returns a typed proposal only when all required evidence is valid. Blocked,
unsuitable, missing, stale, conflicting, or unavailable evidence cannot be
overridden by model output. The role has no approval, publication, or
execution capability.

Every report must be valid at assessment time; marine evidence must also cover
the complete requested operation period. Other report categories use their
source-specific assessment-time validity because they do not expose an
operation-coverage interval.
Report facts are accepted as bounded local input. Hosted model context includes
only the source-backed, typed operational fields allowlisted for each report
category (such as marine status, freshness, and numeric conditions); unknown
and free-form fields remain local. Report sources are retained only locally;
provider context contains validated UUID references, status codes and
timestamps. The input boundary rejects invalid values for recognized fields,
and the Gemini adapter repeats the projection for direct callers.

The model is behind `ModelPort`. `GeminiModelPort` sends one stateless request
to Google's Gemini Interactions API, requests the versioned proposal schema,
and rejects incomplete, malformed, tool-call, or non-text responses. Provider
errors are reduced to safe outcomes; response bodies and credentials are not
included in exceptions. The model name is configured with `GEMINI_MODEL` and
is not silently substituted.

The adapter uses the pinned `v1/interactions` endpoint, rejects HTTP redirects,
and sends the `x-goog-api-key` header only to that endpoint with `store: false`.
It exposes no tools to Gemini.
Google's Free Tier does not carry the Paid Tier's data-use guarantee, so a
privacy/data-handling review is required before provider calls with BLUEVERSE
assessment data. The adapter refuses to initialize unless
`GEMINI_DATA_HANDLING_APPROVED=true` is explicitly configured. The code does
not verify the project's billing tier, free quota, or spend settings; verify
those in AI Studio before enabling it. See Google's [billing and data-use
terms](https://ai.google.dev/gemini-api/docs/billing).

Private HTTP service transport, tool adapters, service authentication,
durable AI execution state, and the Coastal Operations proposal handoff remain
AI-00/runtime integration work.
The fixed read-only tool names are allowlisted, but no handlers are connected
yet and the model receives no executable tool grants. The default package
does not fabricate a result when no model port is configured. It is not yet a
deployable network service.

The Gemini adapter applies `ModelCall.timeout_seconds` to the HTTPS request
and translates transport timeouts to `TimeoutError` or `ModelUnavailable`.
The agent never treats provider failure as a proposal.

Runtime configuration uses environment variables or the approved secret
store. Do not put the Gemini key in source, Compose files, logs, or AI-usage
records:

```text
GEMINI_API_KEY=<Google AI Studio key>
GEMINI_MODEL=<explicit model identifier>
GEMINI_DATA_HANDLING_APPROVED=true
GEMINI_MAX_OUTPUT_TOKENS=2048
```

Run the dependency-free tests from the repository root with:

```powershell
$env:PYTHONDONTWRITEBYTECODE = "1"
python -m unittest discover -s services/agents/tests -p "test_*.py"
```
