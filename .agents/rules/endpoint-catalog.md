# Endpoint catalog lookup

Use this rule for any question or task involving API endpoints, routes,
frontend routes, gateway mappings, health/OpenAPI routes, test-only routes or
Agentic AI endpoints.

## Fast path

The endpoint catalog is the first lookup and the normal answer source:

1. Read the human-readable inventory at
   [`docs/api/endpoint-catalog.md`](../../docs/api/endpoint-catalog.md).
2. Use the catalog entry's method, path, owner, source, boundary,
   authorization, purpose and usage to answer the question.
3. Read [`docs/api/endpoint-catalog.json`](../../docs/api/endpoint-catalog.json)
   only when exact machine-readable fields, UI references, source lists or
   validator-oriented detail are needed.

Do not scan every service, client and infrastructure directory when the
catalog already answers the question. Use the `source` field to identify the
smallest relevant implementation path. This is the default context-efficient
workflow for endpoint lookup.

## When source inspection is justified

Inspect the relevant implementation or configuration only when one or more of
these conditions applies:

- the user explicitly asks to verify the implementation, current runtime
  behavior, authorization, request/response schema, side effects or source;
- the user expresses doubt, reports a mismatch, or names a route absent from
  the catalog;
- the catalog validator reports drift, stale Markdown, missing metadata or an
  unsupported route/authorization form;
- the catalog does not contain the needed detail, or the route is dynamic,
  grouped, generated, middleware-defined or otherwise ambiguous;
- the task changes the endpoint or route. In that case inspect the targeted
  source, update the catalog in the same change and follow the mandatory gate
  in [`change-safety.md`](change-safety.md).

When escalation is necessary, start with the catalog-listed `source` paths and
their direct references. Use targeted `rg` searches; do not broaden to a
whole-repository scan unless the listed source is missing, the user asks for
a repository-wide audit, or source/catalog parity cannot otherwise be proven.

## Documentation synchronization

The catalog is a fast index, not permission to trust stale documentation. For
every endpoint or route addition, update, rename, move or removal, update the
JSON source, regenerate Markdown and run:

```text
python .agents/scripts/validate_endpoint_catalog.py --write-markdown
python .agents/scripts/validate_endpoint_catalog.py
```

Update the UI integration registry and run its validator when a React or
Flutter workflow or public API reference is affected. Never invent a route
from target architecture, and never describe a reserved or absent service as
implemented.
