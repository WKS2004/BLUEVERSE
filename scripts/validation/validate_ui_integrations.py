#!/usr/bin/env python3
"""Validate BLUEVERSE's cross-client UI route and public API contract.

The validator is intentionally dependency-free so it can run on a developer
machine and in GitHub Actions before the backend service projects exist.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from urllib.parse import urlparse


DEFAULT_REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_MANIFEST = Path("docs/contracts/ui-integration.json")

CLIENTS = {
    "web": {
        "source_roots": (Path("apps/web/src"), Path("apps/web/e2e")),
        "extensions": {".js", ".jsx", ".ts", ".tsx"},
    },
    "mobile": {
        "source_roots": (Path("apps/mobile/lib"), Path("apps/mobile/test"), Path("apps/mobile/integration_test")),
        "extensions": {".dart"},
    },
}

HTTP_CALL_PATTERNS = {
    "web": (
        re.compile(r"\bfetch\s*\(\s*(['\"`])(?P<value>[^'\"`]+)\1"),
        re.compile(r"\baxios(?:\.[A-Za-z_$][\w$]*)?\s*\(\s*(['\"`])(?P<value>[^'\"`]+)\1"),
        re.compile(r"\bnew\s+Request\s*\(\s*(['\"`])(?P<value>[^'\"`]+)\1"),
    ),
    "mobile": (
        re.compile(
            r"\b(?:http|client|dio|Dio)\s*(?:\(\))?\s*\.\s*(?:get|post|put|patch|delete|head)\s*\(\s*"
            r"(?:Uri\.parse\s*\(\s*)?(['\"])(?P<value>[^'\"]+)\1"
        ),
    ),
}

DYNAMIC_HTTP_CALL_PATTERNS = {
    "web": (
        re.compile(r"\bfetch\s*\(\s*(?P<value>(?!['\"`])[^,\)\n]+)"),
        re.compile(r"\baxios(?:\.[A-Za-z_$][\w$]*)?\s*\(\s*(?P<value>(?!['\"`])[^,\)\n]+)"),
        re.compile(r"\bnew\s+Request\s*\(\s*(?P<value>(?!['\"`])[^,\)\n]+)"),
    ),
    "mobile": (
        re.compile(
            r"\b(?:http|http\.Client|client|dio|Dio)\s*(?:\(\))?\s*\.\s*"
            r"(?:get|post|put|patch|delete|head)\s*\(\s*(?:Uri\.parse\s*\(\s*)?"
            r"(?P<value>(?!['\"])[^,\)\n]+)"
        ),
    ),
}

ROUTE_PATTERNS = {
    "web": (
        re.compile(
            r"<(?:Route|Link|NavLink)\b[^>]*\b(?:path|to)\s*=\s*(['\"`])(?P<value>[^'\"`]+)\1",
            re.IGNORECASE,
        ),
        re.compile(r"\b(?:path|to|initialRoute)\s*:\s*(['\"`])(?P<value>[^'\"`]+)\1"),
        re.compile(r"\b(?:navigate|push|replace|go)\s*\(\s*(['\"`])(?P<value>[^'\"`]+)\1"),
        re.compile(r"\b(?:window\.)?location\.(?:assign|replace)\s*\(\s*(['\"`])(?P<value>[^'\"`]+)\1"),
        re.compile(r"\b(?:window\.)?location\.href\s*=\s*(['\"`])(?P<value>[^'\"`]+)\1"),
        re.compile(
            r"\bhistory\.(?:pushState|replaceState)\s*\(\s*[^,]*,\s*[^,]*,\s*"
            r"(['\"`])(?P<value>[^'\"`]+)\1"
        ),
        re.compile(r"<a\b[^>]*\bhref\s*=\s*(['\"`])(?P<value>[^'\"`]+)\1", re.IGNORECASE),
    ),
    "mobile": (
        re.compile(r"\b(?:GoRoute|ShellRoute)\s*\([^)]*\bpath\s*:\s*(['\"])(?P<value>[^'\"]+)\1"),
        re.compile(
            r"\b(?:pushNamed|pushReplacementNamed|popAndPushNamed|go|goNamed|push|replace|replaceNamed)\s*\(\s*"
            r"(?:[^,]+,\s*)?(['\"])(?P<value>[^'\"]+)\1"
        ),
        re.compile(r"\broutes\s*:\s*\{\s*(['\"])(?P<value>[^'\"]+)\1"),
        re.compile(r"\binitialRoute\s*:\s*(['\"])(?P<value>[^'\"]+)\1"),
    ),
}

API_LITERAL_PATTERN = re.compile(r"(?P<quote>['\"`])(?P<value>/api/[^'\"`?\s\\]+)(?P=quote)")
INTERNAL_HOST_PATTERN = re.compile(
    r"(?i)(?:\bhttps?://|//)(?:auth|postgres|agentic-ai|agentic|database)(?::\d+)?(?:[/\\:]|\b)"
    r"|(?<![A-Za-z0-9_.-])(?:auth|postgres|agentic-ai|agentic|database):\d+(?:[/\\:]|\b)"
)
VERSIONED_API_PATTERN = re.compile(r"^/api/v\d+(?:/|$)", re.IGNORECASE)
ASSET_SUFFIX_PATTERN = re.compile(r"\.(?:avif|css|gif|ico|jpeg|jpg|js|json|png|svg|webp|woff2?)(?:$|[?#])", re.IGNORECASE)
TEMPLATE_PARAMETER_PATTERN = re.compile(r"\{[^{}]+\}")


def _relative(path: Path, root: Path) -> str:
    return str(path.relative_to(root)).replace("\\", "/")


def _read_json(path: Path, errors: list[str], root: Path) -> dict | None:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        errors.append(f"cannot read {_relative(path, root)} as JSON: {error}")
        return None
    if not isinstance(value, dict):
        errors.append(f"{_relative(path, root)} must contain a JSON object")
        return None
    return value


def _normalise_path(value: str) -> str:
    value = value.strip()
    if "?" in value:
        value = value.split("?", 1)[0]
    if "#" in value:
        value = value.split("#", 1)[0]
    if not value.startswith("/"):
        value = "/" + value
    if len(value) > 1:
        value = value.rstrip("/")
    return value or "/"


def _valid_frontend_route(value: object) -> bool:
    return isinstance(value, str) and value.startswith("/") and not value.startswith("/api/")


def _endpoint_matches(template: str, actual: str, prefix: bool = False) -> bool:
    template = _normalise_path(template)
    template = re.sub(r"\$\{[^{}]+\}", "{parameter}", template)
    actual = _normalise_path(actual)
    if prefix:
        template = template.rstrip("/")
        return actual == template or actual.startswith(template + "/")
    pattern = re.escape(template)
    pattern = re.sub(r"\\\{[^{}]+\\\}", r"[^/]+", pattern)
    return re.fullmatch(pattern + r"/?", actual) is not None


def _is_source_file(path: Path, client: str) -> bool:
    return path.is_file() and path.suffix.lower() in CLIENTS[client]["extensions"]


def _source_files(repo_root: Path, client: str) -> list[Path]:
    files: list[Path] = []
    for relative_root in CLIENTS[client]["source_roots"]:
        root = repo_root / relative_root
        if not root.is_dir():
            continue
        files.extend(path for path in root.rglob("*") if _is_source_file(path, client))
    return sorted(files)


def _extract_named_literals(patterns: tuple[re.Pattern[str], ...], text: str) -> list[str]:
    values: list[str] = []
    for pattern in patterns:
        for match in pattern.finditer(text):
            value = match.group("value").strip()
            if value:
                values.append(value)
    return values


def _extract_api_literals(text: str) -> list[tuple[str, bool]]:
    values: list[tuple[str, bool]] = []
    for match in API_LITERAL_PATTERN.finditer(text):
        end = match.end()
        value = re.sub(r"\$\{[^{}]+\}", "{parameter}", match.group("value"))
        is_template_prefix = text[end : end + 2] == "${"
        values.append((value, is_template_prefix))
    return values


def _composed_controller_path_exists(endpoint_path: str, source: str) -> bool:
    """Recognize controller paths composed from a class prefix and action template."""
    segments = _normalise_path(endpoint_path).strip("/").split("/")
    first_parameter = next(
        (index for index, segment in enumerate(segments) if segment.startswith("{") and segment.endswith("}")),
        None,
    )
    if first_parameter is None:
        return False
    prefix = "/".join(segments[:first_parameter])
    if not prefix or prefix not in source:
        return False
    return all(segment in source for segment in segments[first_parameter:])


def _internal_target_pattern(manifest: dict) -> re.Pattern[str]:
    public_api = manifest.get("publicApi")
    targets = public_api.get("forbiddenClientTargets") if isinstance(public_api, dict) else None
    if not isinstance(targets, list):
        return INTERNAL_HOST_PATTERN
    names = [re.escape(target) for target in targets if isinstance(target, str) and target.strip()]
    if not names:
        return INTERNAL_HOST_PATTERN
    target_names = "|".join(names)
    return re.compile(
        rf"(?i)(?:\bhttps?://|//)(?:{target_names})(?::\d+)?(?:[/\\:]|\b)"
        rf"|(?<![A-Za-z0-9_.-])(?:{target_names}):\d+(?:[/\\:]|\b)"
    )


def _allowed_absolute_hosts(manifest: dict) -> set[str]:
    public_api = manifest.get("publicApi")
    hosts = public_api.get("allowedAbsoluteHosts") if isinstance(public_api, dict) else None
    if not isinstance(hosts, list):
        return set()
    return {
        host.strip().lower().rstrip(".")
        for host in hosts
        if isinstance(host, str) and host.strip()
    }


def _is_frontend_route(value: str) -> bool:
    return value.startswith("/") and not value.startswith("/api/") and not ASSET_SUFFIX_PATTERN.search(value)


def _frontend_route_matches(declared: str, actual: str) -> bool:
    actual_had_trailing_slash = actual.rstrip().endswith("/")
    declared = _normalise_path(declared)
    actual = _normalise_path(actual)
    pattern = re.escape(declared)
    pattern = re.sub(r":[A-Za-z0-9_-]+", r"[^/]+", pattern)
    pattern = re.sub(r"\{[^{}]+\}", r"[^/]+", pattern)
    if re.fullmatch(pattern + r"/?", actual):
        return True
    if actual_had_trailing_slash and declared.startswith(actual + "/"):
        next_segment = declared[len(actual) + 1 :].split("/", 1)[0]
        if (next_segment.startswith(":") and len(next_segment) > 1) or (
            next_segment.startswith("{") and next_segment.endswith("}")
        ):
            return True
    return False


def _validate_manifest(repo_root: Path, manifest: dict, errors: list[str]) -> tuple[dict[str, dict], dict[str, dict]]:
    if manifest.get("schema") != 1:
        errors.append("ui integration manifest schema must be 1")

    public_api = manifest.get("publicApi")
    if not isinstance(public_api, dict):
        errors.append("ui integration manifest must define publicApi")
    else:
        if public_api.get("gatewayPath") != "/api":
            errors.append("publicApi.gatewayPath must be /api")
        if public_api.get("entryService") != "api":
            errors.append("publicApi.entryService must be api")
        forbidden = public_api.get("forbiddenClientTargets")
        if not isinstance(forbidden, list) or not forbidden:
            errors.append("publicApi.forbiddenClientTargets must be a non-empty list")
        allowed_absolute_hosts = public_api.get("allowedAbsoluteHosts")
        if not isinstance(allowed_absolute_hosts, list) or any(
            not isinstance(host, str) or not host.strip() for host in allowed_absolute_hosts
        ):
            errors.append("publicApi.allowedAbsoluteHosts must be a list of non-empty host names")

    endpoint_map: dict[str, dict] = {}
    endpoints = manifest.get("endpoints")
    if not isinstance(endpoints, list):
        errors.append("ui integration manifest endpoints must be a list")
        endpoints = []
    for endpoint in endpoints:
        if not isinstance(endpoint, dict):
            errors.append("every endpoint entry must be an object")
            continue
        endpoint_id = endpoint.get("id")
        path = endpoint.get("path")
        if not isinstance(endpoint_id, str) or not endpoint_id.strip():
            errors.append("every endpoint must have a non-empty id")
            continue
        if endpoint_id in endpoint_map:
            errors.append(f"duplicate UI API endpoint id: {endpoint_id}")
        endpoint_map[endpoint_id] = endpoint
        if endpoint.get("method") not in {"GET", "POST", "PUT", "PATCH", "DELETE"}:
            errors.append(f"endpoint {endpoint_id} has an unsupported HTTP method")
        if not isinstance(path, str) or not path.startswith("/api/"):
            errors.append(f"endpoint {endpoint_id} must use a public /api/... path")
        elif VERSIONED_API_PATTERN.search(_normalise_path(path)):
            errors.append(f"endpoint {endpoint_id} must not introduce /api/v1-style versioning")
        if endpoint.get("publicBoundary") != "api":
            errors.append(f"endpoint {endpoint_id} must declare publicBoundary=api")
        if not isinstance(endpoint.get("ownerService"), str) or not endpoint["ownerService"].strip():
            errors.append(f"endpoint {endpoint_id} must name its owning backend service")
        if not isinstance(endpoint.get("operationId"), str) or not endpoint["operationId"].strip():
            errors.append(f"endpoint {endpoint_id} must declare its backend operationId")

    workflows = manifest.get("workflows")
    if not isinstance(workflows, list) or not workflows:
        errors.append("ui integration manifest must contain at least one workflow")
        workflows = []
    workflow_map: dict[str, dict] = {}
    referenced_endpoint_ids: set[str] = set()
    for workflow in workflows:
        if not isinstance(workflow, dict):
            errors.append("every UI workflow entry must be an object")
            continue
        workflow_id = workflow.get("id")
        if not isinstance(workflow_id, str) or not workflow_id.strip():
            errors.append("every UI workflow must have a non-empty id")
            continue
        if workflow_id in workflow_map:
            errors.append(f"duplicate UI workflow id: {workflow_id}")
        workflow_map[workflow_id] = workflow
        for client in CLIENTS:
            surface = workflow.get(client)
            if not isinstance(surface, dict):
                errors.append(f"workflow {workflow_id} must define a {client} surface")
                continue
            route = surface.get("route")
            source = surface.get("source")
            if not _valid_frontend_route(route):
                errors.append(f"workflow {workflow_id} {client}.route must be a frontend route")
            if not isinstance(source, str) or not source:
                errors.append(f"workflow {workflow_id} {client}.source must be set")
            else:
                source_path = (repo_root / source).resolve()
                expected_root = (repo_root / CLIENTS[client]["source_roots"][0]).resolve()
                try:
                    source_path.relative_to(expected_root)
                except ValueError:
                    errors.append(f"workflow {workflow_id} {client}.source is outside its client source tree: {source}")
                if not source_path.is_file():
                    errors.append(f"workflow {workflow_id} {client}.source does not exist: {source}")

        api_refs = workflow.get("apiRefs")
        if not isinstance(api_refs, list):
            errors.append(f"workflow {workflow_id}.apiRefs must be a list")
            continue
        for endpoint_id in api_refs:
            if not isinstance(endpoint_id, str) or endpoint_id not in endpoint_map:
                errors.append(f"workflow {workflow_id} references an undeclared API endpoint: {endpoint_id!r}")
            else:
                referenced_endpoint_ids.add(endpoint_id)

    for endpoint_id in sorted(set(endpoint_map) - referenced_endpoint_ids):
        errors.append(f"endpoint {endpoint_id} is not referenced by any UI workflow")

    return workflow_map, endpoint_map


def _backend_files(repo_root: Path, service_name: str) -> list[Path]:
    service_root = repo_root / "services" / service_name
    if not service_root.is_dir():
        return []
    generated = {"bin", "obj", "node_modules", ".dart_tool", "build", "dist", "coverage"}
    allowed = {".cs", ".csproj", ".json", ".yaml", ".yml", ".http"}
    return sorted(
        path
        for path in service_root.rglob("*")
        if path.is_file() and path.suffix.lower() in allowed and not any(part in generated for part in path.parts)
    )


def _validate_backend_contract(repo_root: Path, endpoint_map: dict[str, dict], errors: list[str]) -> None:
    """Require checked-in service/OpenAPI evidence for registered endpoints."""

    by_service: dict[str, list[tuple[str, dict]]] = {}
    for endpoint_id, endpoint in endpoint_map.items():
        owner_service = endpoint.get("ownerService")
        if not isinstance(owner_service, str) or not owner_service.strip():
            continue
        by_service.setdefault(owner_service, []).append((endpoint_id, endpoint))

    for service_name, endpoints in by_service.items():
        files = _backend_files(repo_root, service_name)
        if not files:
            errors.append(
                f"registered UI API endpoints require checked-in backend/OpenAPI evidence under services/{service_name}"
            )
            continue
        contents: list[tuple[Path, str]] = []
        for path in files:
            try:
                contents.append((path, path.read_text(encoding="utf-8")))
            except OSError as error:
                errors.append(f"cannot read backend contract evidence {_relative(path, repo_root)}: {error}")

        for endpoint_id, endpoint in endpoints:
            operation_id = endpoint.get("operationId")
            if not isinstance(operation_id, str) or not operation_id.strip():
                continue
            if not any(operation_id in content for _, content in contents):
                errors.append(
                    f"endpoint {endpoint_id} operationId {operation_id!r} is absent from services/{service_name} source/OpenAPI evidence"
                )
                continue
            endpoint_path = endpoint.get("path")
            path_candidates = (
                endpoint_path,
                endpoint_path[1:] if isinstance(endpoint_path, str) and endpoint_path.startswith("/") else None,
            )
            if not isinstance(endpoint_path, str) or not (
                any(
                    candidate and any(candidate in content for _, content in contents)
                    for candidate in path_candidates
                )
                or any(
                    _composed_controller_path_exists(endpoint_path, content)
                    for _, content in contents
                )
            ):
                errors.append(
                    f"endpoint {endpoint_id} public path {endpoint_path!r} is absent from services/{service_name} source/OpenAPI evidence"
                )


def _validate_route_literals(client: str, path: Path, text: str, declared_routes: set[str], errors: list[str], root: Path) -> None:
    for value in _extract_named_literals(ROUTE_PATTERNS[client], text):
        if not _is_frontend_route(value):
            continue
        route = _normalise_path(value)
        if not any(_frontend_route_matches(declared, value) for declared in declared_routes):
            errors.append(
                f"{_relative(path, root)} declares frontend route {value!r} that is absent from docs/contracts/ui-integration.json"
            )


def _validate_network_value(
    client: str,
    path: Path,
    value: str,
    endpoint_map: dict[str, dict],
    allowed_absolute_hosts: set[str],
    errors: list[str],
    root: Path,
) -> None:
    parsed = urlparse(value)
    if parsed.scheme or parsed.netloc:
        host = (parsed.hostname or "").lower().rstrip(".")
        if host not in allowed_absolute_hosts:
            errors.append(f"{_relative(path, root)} uses an unapproved public API host: {value!r}")
            return
        api_path = parsed.path
        if not api_path.startswith("/api/"):
            errors.append(f"{_relative(path, root)} uses a non-public API URL: {value!r}")
            return
        value = api_path
    elif value.startswith("/"):
        if not value.startswith("/api/"):
            errors.append(f"{_relative(path, root)} uses a frontend route as a network target: {value!r}")
            return
    else:
        errors.append(f"{_relative(path, root)} uses an unresolved non-public network target: {value!r}")
        return

    normalised = _normalise_path(value)
    if VERSIONED_API_PATTERN.search(normalised):
        errors.append(f"{_relative(path, root)} uses forbidden versioned API path: {value!r}")
        return
    if not any(_endpoint_matches(endpoint.get("path", ""), normalised) for endpoint in endpoint_map.values()):
        errors.append(
            f"{_relative(path, root)} calls undeclared public API endpoint {value!r}; add its endpoint and workflow reference first"
        )


def _validate_client_sources(
    repo_root: Path,
    client: str,
    workflow_map: dict[str, dict],
    endpoint_map: dict[str, dict],
    internal_target_pattern: re.Pattern[str],
    allowed_absolute_hosts: set[str],
    errors: list[str],
) -> None:
    declared_routes = {
        _normalise_path(workflow[client]["route"])
        for workflow in workflow_map.values()
        if isinstance(workflow.get(client), dict) and isinstance(workflow[client].get("route"), str)
    }

    for path in _source_files(repo_root, client):
        try:
            text = path.read_text(encoding="utf-8")
        except OSError as error:
            errors.append(f"cannot read {_relative(path, repo_root)}: {error}")
            continue

        if internal_target_pattern.search(text):
            errors.append(f"{_relative(path, repo_root)} contains a direct internal-service target")

        _validate_route_literals(client, path, text, declared_routes, errors, repo_root)

        for value in _extract_named_literals(HTTP_CALL_PATTERNS[client], text):
            if internal_target_pattern.search(value):
                continue
            _validate_network_value(client, path, value, endpoint_map, allowed_absolute_hosts, errors, repo_root)

        for pattern in DYNAMIC_HTTP_CALL_PATTERNS[client]:
            for match in pattern.finditer(text):
                errors.append(
                    f"{_relative(path, repo_root)} uses a dynamic network target {match.group('value').strip()!r}; "
                    "use a literal registered public /api/... path or an allowlisted absolute host"
                )

        for value, is_prefix in _extract_api_literals(text):
            normalised = _normalise_path(value)
            if VERSIONED_API_PATTERN.search(normalised):
                errors.append(f"{_relative(path, repo_root)} contains forbidden versioned API path: {value!r}")
                continue
            if not any(_endpoint_matches(endpoint.get("path", ""), normalised, prefix=is_prefix) for endpoint in endpoint_map.values()):
                errors.append(
                    f"{_relative(path, repo_root)} contains undeclared public API path {value!r}; add its endpoint and workflow reference first"
                )


def validate_repository(repo_root: Path = DEFAULT_REPO_ROOT, manifest_path: Path | None = None) -> list[str]:
    """Return contract violations for a repository checkout."""

    repo_root = repo_root.resolve()
    manifest_path = (repo_root / (manifest_path or DEFAULT_MANIFEST)).resolve()
    errors: list[str] = []
    if not manifest_path.is_file():
        return [f"missing UI integration manifest: {_relative(manifest_path, repo_root)}"]
    manifest = _read_json(manifest_path, errors, repo_root)
    if manifest is None:
        return errors

    workflow_map, endpoint_map = _validate_manifest(repo_root, manifest, errors)
    internal_target_pattern = _internal_target_pattern(manifest)
    allowed_absolute_hosts = _allowed_absolute_hosts(manifest)
    _validate_backend_contract(repo_root, endpoint_map, errors)
    _validate_client_sources(
        repo_root, "web", workflow_map, endpoint_map, internal_target_pattern, allowed_absolute_hosts, errors
    )
    _validate_client_sources(
        repo_root, "mobile", workflow_map, endpoint_map, internal_target_pattern, allowed_absolute_hosts, errors
    )
    return errors


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=DEFAULT_REPO_ROOT)
    parser.add_argument("--manifest", type=Path, default=None)
    args = parser.parse_args(argv)
    errors = validate_repository(args.repo_root, args.manifest)
    if errors:
        print("UI integration contract: FAILED")
        for error in errors:
            print(f"- {error}")
        return 1

    print("UI integration contract: OK")
    print("- React and Flutter workflow routes are registered together")
    print("- Client API calls are constrained to declared public /api/... endpoints")
    print("- Dynamic targets, unapproved hosts, direct internal targets and /api/v1-style paths are rejected")
    return 0


if __name__ == "__main__":
    sys.exit(main())
