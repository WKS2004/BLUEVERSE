"""Validate the repository endpoint catalog against implementation sources.

This validator intentionally uses only the Python standard library so it can
run in CI before application dependencies are restored. The catalog is a fast
index, but source declarations remain the implementation authority: drift is
an error rather than a reason to silently trust stale documentation.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path
from typing import Any, Iterable


REPO_ROOT = Path(__file__).resolve().parents[2]
CATALOG_PATH = REPO_ROOT / "docs" / "api" / "endpoint-catalog.json"
MARKDOWN_PATH = REPO_ROOT / "docs" / "api" / "endpoint-catalog.md"
UI_MANIFEST_PATH = REPO_ROOT / "docs" / "contracts" / "ui-integration.json"

HTTP_METHODS = {"GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"}
CLIENT_ROOTS = (
    REPO_ROOT / "apps" / "web" / "src",
    REPO_ROOT / "apps" / "mobile" / "lib",
)
CLIENT_API_PATTERN = re.compile(r"['\"`](/api/[^'\"`?\s\\]+)['\"`]")


def _load_json(path: Path, errors: list[str]) -> dict[str, Any] | None:
    if not path.is_file():
        errors.append(f"missing JSON file: {_relative(path)}")
        return None
    try:
        value = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as exc:
        errors.append(f"invalid JSON in {_relative(path)}: {exc}")
        return None
    if not isinstance(value, dict):
        errors.append(f"JSON root must be an object: {_relative(path)}")
        return None
    return value


def _relative(path: Path) -> str:
    try:
        return path.relative_to(REPO_ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def _normalise_path(path: str) -> str:
    value = path.split("?", 1)[0].strip()
    if not value.startswith("/"):
        value = "/" + value
    if len(value) > 1:
        value = value.rstrip("/")
    return value


def _join_route(base: str, suffix: str) -> str:
    base_value = base.rstrip("/")
    suffix_value = suffix.strip("/")
    if not suffix_value:
        return _normalise_path(base_value)
    return _normalise_path(f"{base_value}/{suffix_value}")


def _sources(value: Any) -> Iterable[str]:
    if isinstance(value, str):
        return (value,)
    if isinstance(value, list) and all(isinstance(item, str) for item in value):
        return value
    return ()


def _all_catalog_entries(catalog: dict[str, Any]) -> list[dict[str, Any]]:
    entries: list[dict[str, Any]] = []
    for section in ("publicApiEndpoints", "testOnlyEndpoints"):
        values = catalog.get(section, [])
        if isinstance(values, list):
            entries.extend(item for item in values if isinstance(item, dict))
    ai_section = catalog.get("aiEndpoints", {})
    if isinstance(ai_section, dict) and isinstance(ai_section.get("entries"), list):
        entries.extend(item for item in ai_section["entries"] if isinstance(item, dict))
    return entries


def _validate_catalog_shape(
    catalog: dict[str, Any], errors: list[str]
) -> tuple[dict[str, dict[str, Any]], dict[str, dict[str, Any]]]:
    if catalog.get("schema") != 1:
        errors.append("docs/api/endpoint-catalog.json must use schema 1")

    public = catalog.get("publicApiEndpoints")
    test_only = catalog.get("testOnlyEndpoints")
    frontend = catalog.get("frontendRoutes")
    gateway = catalog.get("gatewayRoutes")
    if not isinstance(public, list) or not public:
        errors.append("catalog publicApiEndpoints must be a non-empty list")
        public = []
    if not isinstance(test_only, list):
        errors.append("catalog testOnlyEndpoints must be a list")
        test_only = []
    if not isinstance(frontend, list) or not frontend:
        errors.append("catalog frontendRoutes must be a non-empty list")
        frontend = []
    if not isinstance(gateway, list) or not gateway:
        errors.append("catalog gatewayRoutes must be a non-empty list")

    for section in ("publicApiEndpoints", "testOnlyEndpoints", "frontendRoutes", "gatewayRoutes"):
        values = catalog.get(section)
        if isinstance(values, list) and any(not isinstance(value, dict) for value in values):
            errors.append(f"{section} entries must be objects")

    by_id: dict[str, dict[str, Any]] = {}
    for entry in _all_catalog_entries(catalog):
        entry_id = entry.get("id")
        if not isinstance(entry_id, str) or not entry_id:
            errors.append("every endpoint catalog entry needs a non-empty id")
            continue
        if entry_id in by_id:
            errors.append(f"duplicate endpoint catalog id: {entry_id}")
        by_id[entry_id] = entry

        for field in ("description", "usage", "ownerService"):
            if not isinstance(entry.get(field), str) or not entry[field].strip():
                errors.append(f"{entry_id} needs a non-empty {field}")
        if entry in public:
            for field in ("auth", "operationId"):
                if not isinstance(entry.get(field), str) or not entry[field].strip():
                    errors.append(f"public endpoint {entry_id} needs a non-empty {field}")

        method = entry.get("method")
        if not isinstance(method, str) or method not in HTTP_METHODS:
            errors.append(f"{entry_id} has unsupported HTTP method: {method!r}")
        path = entry.get("path")
        if not isinstance(path, str) or not path.startswith("/"):
            errors.append(f"{entry_id} must have an absolute route path")
        elif entry in public and not path.startswith("/api/"):
            errors.append(f"public endpoint {entry_id} must stay below /api/")
        elif isinstance(path, str) and re.search(r"/api/v\d+(?:/|$)", path):
            errors.append(f"versioned API path is forbidden: {entry_id} -> {path}")

        source_values = tuple(_sources(entry.get("source")))
        if not source_values:
            errors.append(f"{entry_id} needs one or more source paths")
        for source in source_values:
            if not (REPO_ROOT / source).is_file():
                errors.append(f"{entry_id} source does not exist: {source}")

    frontend_by_id: dict[str, dict[str, Any]] = {}
    for entry in frontend:
        if not isinstance(entry, dict):
            errors.append("every frontend route catalog entry must be an object")
            continue
        entry_id = entry.get("id")
        if not isinstance(entry_id, str) or not entry_id:
            errors.append("every frontend route needs a non-empty id")
            continue
        if entry_id in frontend_by_id:
            errors.append(f"duplicate frontend route id: {entry_id}")
        frontend_by_id[entry_id] = entry
        for field in ("workflowId", "usage"):
            if not isinstance(entry.get(field), str) or not entry[field].strip():
                errors.append(f"{entry_id} needs a non-empty {field}")
        if entry.get("client") not in {"react", "flutter"}:
            errors.append(f"{entry_id} has unsupported frontend client")
        if not isinstance(entry.get("route"), str) or not entry["route"].startswith("/"):
            errors.append(f"{entry_id} must have a frontend route path")
        source_values = tuple(_sources(entry.get("source")))
        if not source_values:
            errors.append(f"{entry_id} needs a frontend source path")
        for source in source_values:
            if not (REPO_ROOT / source).is_file():
                errors.append(f"{entry_id} source does not exist: {source}")

    gateway_values = catalog.get("gatewayRoutes", [])
    gateway_ids: set[str] = set()
    if isinstance(gateway_values, list):
        for entry in gateway_values:
            if not isinstance(entry, dict):
                errors.append("every gateway route catalog entry must be an object")
                continue
            entry_id = entry.get("id")
            if not isinstance(entry_id, str) or not entry_id:
                errors.append("every gateway route needs a non-empty id")
            elif entry_id in gateway_ids:
                errors.append(f"duplicate gateway route id: {entry_id}")
            else:
                gateway_ids.add(entry_id)
            for field in ("method", "upstream", "usage"):
                if not isinstance(entry.get(field), str) or not entry[field].strip():
                    errors.append(f"gateway route {entry_id} needs a non-empty {field}")
            if not isinstance(entry.get("path"), str) or not entry["path"].startswith("/"):
                errors.append(f"gateway route {entry_id} must have an absolute path")
            source_values = tuple(_sources(entry.get("source")))
            if not source_values:
                errors.append(f"gateway route {entry_id} needs a source path")
            for source in source_values:
                if not (REPO_ROOT / source).is_file():
                    errors.append(f"gateway route {entry_id} source does not exist: {source}")

    ai_section = catalog.get("aiEndpoints")
    if not isinstance(ai_section, dict):
        errors.append("catalog aiEndpoints must be an object")
    else:
        if ai_section.get("status") not in {"none-implemented", "implemented"}:
            errors.append("invalid AI endpoint status")
        if not isinstance(ai_section.get("entries"), list) or any(
            not isinstance(entry, dict) for entry in ai_section.get("entries", [])
        ):
            errors.append("AI endpoint entries must be a list of objects")
        if ai_section.get("status") == "none-implemented" and ai_section.get("entries") != []:
            errors.append("aiEndpoints with status none-implemented must have an empty entries list")

    return by_id, frontend_by_id


def _validate_ui_contract(
    catalog: dict[str, Any],
    endpoint_by_id: dict[str, dict[str, Any]],
    frontend_by_id: dict[str, dict[str, Any]],
    errors: list[str],
) -> None:
    ui = _load_json(UI_MANIFEST_PATH, errors)
    if ui is None:
        return

    manifest_endpoints = ui.get("endpoints")
    workflows = ui.get("workflows")
    if not isinstance(manifest_endpoints, list) or not isinstance(workflows, list):
        errors.append("UI integration manifest must contain endpoints and workflows lists")
        return

    workflow_by_id = {
        item.get("id"): item
        for item in workflows
        if isinstance(item, dict) and isinstance(item.get("id"), str)
    }
    manifest_endpoint_ids: set[str] = set()
    for item in manifest_endpoints:
        if not isinstance(item, dict):
            errors.append("every UI manifest endpoint must be an object")
            continue
        endpoint_id = item.get("id")
        if not isinstance(endpoint_id, str):
            errors.append("every UI manifest endpoint needs an id")
            continue
        manifest_endpoint_ids.add(endpoint_id)
        catalog_item = endpoint_by_id.get(endpoint_id)
        if catalog_item is None:
            errors.append(f"UI endpoint {endpoint_id} is missing from endpoint catalog")
            continue
        for field in ("method", "path", "publicBoundary", "ownerService", "operationId"):
            if catalog_item.get(field) != item.get(field):
                errors.append(
                    f"UI endpoint {endpoint_id} differs from catalog in {field}: "
                    f"{item.get(field)!r} != {catalog_item.get(field)!r}"
                )

    for workflow in workflows:
        if not isinstance(workflow, dict):
            continue
        workflow_id = workflow.get("id")
        for endpoint_id in workflow.get("apiRefs", []):
            if endpoint_id not in endpoint_by_id:
                errors.append(f"workflow {workflow_id} references undocumented endpoint {endpoint_id}")
            elif endpoint_id not in manifest_endpoint_ids:
                errors.append(f"workflow {workflow_id} references endpoint absent from UI endpoint list: {endpoint_id}")

        for client, catalog_client in (("web", "react"), ("mobile", "flutter")):
            surface = workflow.get(client)
            if not isinstance(surface, dict):
                continue
            route = surface.get("route")
            source = surface.get("source")
            matches = [
                item
                for item in frontend_by_id.values()
                if item.get("client") == catalog_client
                and item.get("workflowId") == workflow_id
                and item.get("route") == route
                and item.get("source") == source
            ]
            if not matches:
                errors.append(
                    f"UI workflow {workflow_id} {client} route is missing or differs in endpoint catalog"
                )

    for endpoint_id, catalog_item in endpoint_by_id.items():
        for workflow_id in catalog_item.get("uiWorkflowIds", []):
            workflow = workflow_by_id.get(workflow_id)
            if workflow is None:
                errors.append(f"catalog endpoint {endpoint_id} references unknown UI workflow {workflow_id}")
            elif endpoint_id not in workflow.get("apiRefs", []):
                errors.append(
                    f"catalog endpoint {endpoint_id} says it is used by {workflow_id}, "
                    "but the UI workflow does not reference it"
                )


def _source_text(text: str) -> str:
    """Remove C# comments while preserving quoted route and policy strings."""
    return re.sub(
        r'"(?:\\.|[^"\\])*"|//[^\n]*|/\*[\s\S]*?\*/',
        lambda match: match.group() if match.group().startswith('"') else " ",
        text,
    )


def _permission_constants(repo_root: Path) -> dict[str, str]:
    source = repo_root / "services" / "auth" / "Authorization" / "PermissionCodes.cs"
    if not source.is_file():
        return {}
    text = _source_text(source.read_text(encoding="utf-8-sig"))
    return {
        name: code
        for name, code in re.findall(
            r'\bconst\s+string\s+(\w+)\s*=\s*"([^\"]+)"\s*;', text
        )
    }


def _authorization(
    attributes: str,
    errors: list[str],
    source: str,
    permission_constants: dict[str, str],
) -> str:
    if re.search(r"\[\s*AllowAnonymous\s*\]", attributes):
        return "anonymous"
    permission_arguments = re.findall(
        r"\[\s*HasPermission\s*\(\s*([^)]*?)\s*\)\s*\]", attributes
    )
    permissions: list[str] = []
    for argument in permission_arguments:
        literal = re.fullmatch(r'"([^\"]+)"', argument.strip())
        constant = re.fullmatch(r"PermissionCodes\.(\w+)", argument.strip())
        if literal:
            permissions.append(literal.group(1))
        elif constant and constant.group(1) in permission_constants:
            permissions.append(permission_constants[constant.group(1)])
        else:
            errors.append(f"unsupported HasPermission argument in {source}; use a literal or PermissionCodes constant")
    if re.search(r"\[\s*HasPermission\b", attributes) and not permission_arguments:
        errors.append(f"unsupported HasPermission declaration in {source}; extend source validation")
    if len(permissions) == 1:
        return "permission:" + permissions[0]
    if len(permissions) > 1:
        # Multiple HasPermission attributes are ANDed by ASP.NET Core's
        # authorization policy. Preserve that all-of relationship in the
        # catalog rather than flattening it to a single permission.
        return "permission:all(" + ",".join(permissions) + ")"
    if re.search(r"\[\s*Authorize\s*\(", attributes):
        errors.append(f"unsupported authorization options in {source}; extend source validation")
    return "authenticated" if re.search(r"\[\s*Authorize\b", attributes) else "anonymous"


def _closing_parenthesis(text: str, opening: int) -> int | None:
    depth = 0
    quoted = False
    escaped = False
    for index in range(opening, len(text)):
        char = text[index]
        if quoted:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == '"':
                quoted = False
            continue
        if char == '"':
            quoted = True
        elif char == "(":
            depth += 1
        elif char == ")":
            depth -= 1
            if depth == 0:
                return index
    return None


def discover_endpoints(repo_root: Path, errors: list[str]) -> dict[tuple[str, str, str], dict[str, str]]:
    """Recognize the repository's literal C# route forms; reject unsupported declarations.

    This is a bounded static check, not a C# compiler or a runtime security audit.
    Each discovered route retains its source identity and exposure category.
    """
    endpoints: dict[tuple[str, str, str], dict[str, str]] = {}
    permission_constants = _permission_constants(repo_root)
    declaration = re.compile(
        r'(?P<attributes>(?:\s*\[(?:[^\]"]|"(?:\\.|[^"\\])*")*\])+\s*)'
        r'public\s+(?P<signature>[^\n{;]+)'
    )
    route_attribute = re.compile(r'\[\s*Route\s*\(\s*"([^"]+)"\s*\)\s*\]')
    http_attribute = re.compile(
        r'\[\s*Http(Get|Post|Put|Patch|Delete|Head|Options)'
        r'(?:\s*\(\s*"([^"]*)"\s*\))?\s*\]'
    )
    for source in sorted((repo_root / "services").rglob("*.cs")):
        parts = source.relative_to(repo_root).parts
        if any(part.lower() in {"bin", "obj"} for part in parts):
            continue
        source_name = source.relative_to(repo_root).as_posix()
        section = (
            "testOnlyEndpoints" if "tests" in parts else
            "aiEndpoints" if len(parts) > 1 and parts[1] in {"ai", "ai-agents", "agents"} else
            "publicApiEndpoints"
        )
        text = _source_text(source.read_text(encoding="utf-8-sig"))
        if re.search(r'\b(?:FallbackPolicy|DefaultPolicy)\s*=', text):
            errors.append(f"global authorization policy requires validator support in {source_name}")
        supported_maps = {"Get", "Post", "Put", "Patch", "Delete", "Head", "Options",
                          "Methods", "Group", "Controllers", "ReverseProxy", "OpenApi"}
        for map_call in re.finditer(r'\bapp\.Map(\w*)\s*\(', text):
            if map_call.group(1) not in supported_maps:
                errors.append(f"unsupported endpoint mapping in {source_name}; extend source validation")
        owner = parts[1]
        class_route = ""
        class_attributes = ""
        seen_http = 0

        def record(method: str, path: str, auth: str, operation: str = "") -> None:
            key = (source_name, method, _normalise_path(path))
            if key in endpoints:
                errors.append(f"duplicate source route: {key}")
            endpoints[key] = {
                "section": section, "auth": auth, "ownerService": owner,
                "operationId": operation,
            }

        for match in declaration.finditer(text):
            attributes = match.group("attributes")
            signature = match.group("signature")
            routes = route_attribute.findall(attributes)
            if re.search(r"\bclass\s+\w+", signature):
                class_attributes = attributes
                class_route = routes[0] if len(routes) == 1 else ""
                if len(routes) > 1:
                    errors.append(f"multiple controller route prefixes unsupported in {source_name}")
                continue
            http_routes = list(http_attribute.finditer(attributes))
            seen_http += len(http_routes)
            if not http_routes:
                continue
            method_name = re.search(r"(\w+)\s*\(", signature)
            operation = method_name.group(1) if method_name else ""
            if routes or not class_route or "[" in class_route:
                errors.append(f"unsupported controller route form in {source_name}; use a literal class prefix and HTTP suffix")
                continue
            auth = _authorization(
                class_attributes + attributes,
                errors,
                source_name,
                permission_constants,
            )
            for route in http_routes:
                suffix = route.group(2) or ""
                path = suffix[1:] if suffix.startswith("~/") else (
                    suffix if suffix.startswith("/") else _join_route(class_route, suffix)
                )
                record(route.group(1).upper(), path, auth, operation)
        if seen_http != len(re.findall(r"\[\s*Http(?:Get|Post|Put|Patch|Delete|Head|Options)\b", text)):
            errors.append(f"unsupported HTTP attribute declaration in {source_name}; extend source validation")
        if re.search(r"\[\s*AcceptVerbs\b", text):
            errors.append(f"unsupported AcceptVerbs declaration in {source_name}; extend source validation")

        for match in re.finditer(r'\b(\w+)\.Map(Get|Post|Put|Patch|Delete|Head|Options|Methods|Group)\s*\(', text):
            receiver, verb = match.group(1, 2)
            opening = match.end() - 1
            literal = re.match(r'\s*"([^"]+)"\s*,', text[opening + 1:])
            closing = _closing_parenthesis(text, opening)
            if receiver != "app" or verb in {"Methods", "Group"} or literal is None or closing is None:
                errors.append(f"unsupported minimal API declaration in {source_name}; use literal app.MapVerb or extend source validation")
                continue
            tail = text[closing + 1:]
            # Only metadata immediately chained to this endpoint belongs to it.
            metadata = ""
            while True:
                chain = re.match(r'\s*\.(\w+)\s*\(', tail)
                if not chain:
                    break
                chain_end = _closing_parenthesis(tail, chain.end() - 1)
                if chain_end is None:
                    errors.append(f"unclosed endpoint metadata in {source_name}")
                    break
                metadata += tail[:chain_end + 1]
                tail = tail[chain_end + 1:]
            if re.search(r'\.RequireAuthorization\s*\(\s*[^)]', metadata):
                errors.append(f"unsupported minimal API authorization policy in {source_name}; extend source validation")
            auth = "authenticated" if ".RequireAuthorization" in metadata else "anonymous"
            if ".AllowAnonymous" in metadata:
                auth = "anonymous"
            named = re.search(r'\.WithName\s*\(\s*"([^"]+)"\s*\)', metadata)
            record(verb.upper(), literal.group(1), auth, named.group(1) if named else "")

        for pattern in (
            r'MapOpenApi\s*\(\s*"([^"]+)"\s*\)',
            r'RouteTemplate\s*=\s*"([^"]+)"',
            r'RoutePrefix\s*=\s*"([^"]+)"',
        ):
            for match in re.finditer(pattern, text):
                record("GET", match.group(1), "anonymous")
    return endpoints


def _validate_source_routes(catalog: dict[str, Any], errors: list[str]) -> None:
    actual = discover_endpoints(REPO_ROOT, errors)
    documented: set[tuple[str, str, str]] = set()
    groups = [
        ("publicApiEndpoints", catalog.get("publicApiEndpoints", [])),
        ("testOnlyEndpoints", catalog.get("testOnlyEndpoints", [])),
        ("aiEndpoints", catalog.get("aiEndpoints", {}).get("entries", [])),
    ]
    for section, entries in groups:
        for entry in entries:
            for source in _sources(entry.get("source")):
                key = (source, entry["method"], _normalise_path(entry["path"]))
                if key in documented:
                    errors.append(f"duplicate catalog route: {key}")
                documented.add(key)
                found = actual.get(key)
                if found is None:
                    errors.append(f"catalog route is absent from source: {entry['id']} -> {key}")
                    continue
                if found["section"] != section:
                    errors.append(f"wrong exposure section for {entry['id']}: expected {found['section']}")
                for field in ("ownerService", "auth", "operationId"):
                    # Test fixtures need not publish authorization/operation metadata.
                    if field not in entry or (field == "operationId" and not found[field]):
                        continue
                    if entry[field] != found[field]:
                        errors.append(f"source/catalog {field} mismatch for {entry['id']}: expected {found[field]!r}")
                if section == "publicApiEndpoints" and entry.get("publicBoundary") != "api":
                    errors.append(f"public endpoint {entry['id']} must use publicBoundary=api")
                if section == "testOnlyEndpoints" and entry.get("publicBoundary") != "test-host":
                    errors.append(f"test endpoint {entry['id']} must use publicBoundary=test-host")
    for key in sorted(actual.keys() - documented):
        errors.append(f"implemented route is missing from endpoint catalog: {key}")
    ai_found = any(value["section"] == "aiEndpoints" for value in actual.values())
    ai = catalog["aiEndpoints"]
    expected_status = "implemented" if ai_found else "none-implemented"
    if ai.get("status") != expected_status:
        errors.append(f"AI endpoint status must be {expected_status} according to discovered routes")


def _validate_gateway_routes(catalog: dict[str, Any], errors: list[str]) -> None:
    """Check that the documented gateway mappings still exist in config."""

    gateway_entries = catalog.get("gatewayRoutes", [])
    if not isinstance(gateway_entries, list):
        return
    source_routes: set[tuple[str, str]] = set()
    for source_name in (
        "infrastructure/docker/edge-nginx/nginx.conf",
        "infrastructure/docker/frontend/nginx.conf",
    ):
        source_path = REPO_ROOT / source_name
        if not source_path.is_file():
            errors.append(f"gateway source does not exist: {source_name}")
            continue
        source_text = source_path.read_text(encoding="utf-8-sig")
        actual_paths = re.findall(r"(?m)^\s*location\s+(?:=\s+)?([^\s{]+)", source_text)
        for actual_path in actual_paths:
            source_routes.add((source_name, _normalise_path(actual_path)))

    appsettings_path = REPO_ROOT / "services/api/appsettings.json"
    if appsettings_path.is_file():
        appsettings = _load_json(appsettings_path, errors)
        actual_yarp_paths: list[str] = []

        def collect_paths(value: Any) -> None:
            if isinstance(value, dict):
                path_value = value.get("Path")
                if isinstance(path_value, str):
                    actual_yarp_paths.append(path_value)
                for child in value.values():
                    collect_paths(child)
            elif isinstance(value, list):
                for child in value:
                    collect_paths(child)

        if appsettings is not None:
            collect_paths(appsettings)
        for actual_path in actual_yarp_paths:
            source_routes.add(("services/api/appsettings.json", _normalise_path(actual_path)))

    catalog_routes: set[tuple[str, str]] = set()
    for entry in gateway_entries:
        if not isinstance(entry, dict) or not isinstance(entry.get("path"), str):
            continue
        for source_name in _sources(entry.get("source")):
            if source_name.endswith("/Program.cs"):
                source_path = REPO_ROOT / source_name
                if source_path.is_file():
                    source_text = source_path.read_text(encoding="utf-8-sig")
                    route_prefixes = {
                        _normalise_path(value)
                        for value in re.findall(r'RoutePrefix\s*=\s*"([^"]+)"', source_text)
                    }
                    if _normalise_path(entry["path"]) not in route_prefixes:
                        errors.append(
                            f"catalog gateway route is absent from source: "
                            f"{source_name} -> {entry['path']}"
                        )
                continue
            catalog_routes.add((source_name, _normalise_path(entry["path"])))
    for source_name, path in sorted(source_routes - catalog_routes):
        errors.append(f"gateway route is missing from endpoint catalog: {source_name} -> {path}")
    for source_name, path in sorted(catalog_routes - source_routes):
        errors.append(f"catalog gateway route is absent from source: {source_name} -> {path}")


def _path_matches_literal(literal: str, catalog_path: str) -> bool:
    literal_value = _normalise_path(literal)
    template = _normalise_path(catalog_path)
    pattern = re.escape(template)
    pattern = re.sub(r"\\\{[^}]+\\\}", r"[^/]+", pattern)
    return re.fullmatch(pattern, literal_value) is not None


def _validate_client_literals(catalog: dict[str, Any], errors: list[str]) -> int:
    public_entries = [
        entry
        for entry in catalog.get("publicApiEndpoints", [])
        if isinstance(entry, dict) and isinstance(entry.get("path"), str)
    ]
    literal_count = 0
    for root in CLIENT_ROOTS:
        if not root.is_dir():
            continue
        for source in root.rglob("*"):
            if not source.is_file() or source.suffix.lower() not in {".ts", ".tsx", ".js", ".jsx", ".dart"}:
                continue
            try:
                text = source.read_text(encoding="utf-8-sig")
            except OSError:
                continue
            for match in CLIENT_API_PATTERN.finditer(text):
                literal_count += 1
                literal = match.group(1)
                if not any(_path_matches_literal(literal, entry["path"]) for entry in public_entries):
                    errors.append(
                        f"client API literal is missing from endpoint catalog: {_relative(source)} -> {literal}"
                    )
    return literal_count


def _markdown_cell(value: Any) -> str:
    return str(value).replace("|", "\\|").replace("\n", " ").strip()


def render_markdown(catalog: dict[str, Any]) -> str:
    """Render the human route index from the machine-checked catalog."""

    lines = [
        "# BLUEVERSE routes and API endpoints",
        "",
        "Generated from [endpoint-catalog.json](endpoint-catalog.json). Edit the JSON",
        "when routes change, then run",
        "`python .agents/scripts/validate_endpoint_catalog.py --write-markdown`.",
        "The source route declarations remain authoritative. This page is the",
        "quick index; [UI integration](../contracts/ui-integration.json) contains",
        "the smaller shared-client workflow contract.",
        "",
        "## Frontend routes",
        "",
        "| Client | Route | Workflow | Use |",
        "|---|---|---|---|",
    ]
    for entry in catalog.get("frontendRoutes", []):
        lines.append(
            f"| {_markdown_cell(entry['client'])} | `{_markdown_cell(entry['route'])}` | "
            f"`{_markdown_cell(entry['workflowId'])}` | {_markdown_cell(entry['usage'])} |"
        )

    lines.extend(["", "## Gateway and server routes", "", "| Method | Path | Destination | Use |", "|---|---|---|---|"])
    for entry in catalog.get("gatewayRoutes", []):
        lines.append(
            f"| `{_markdown_cell(entry['method'])}` | `{_markdown_cell(entry['path'])}` | "
            f"`{_markdown_cell(entry['upstream'])}` | {_markdown_cell(entry['usage'])} |"
        )

    lines.extend(["", "## Public API endpoints", ""])
    for owner in sorted({entry["ownerService"] for entry in catalog.get("publicApiEndpoints", [])}):
        lines.extend([
            f"### {owner}",
            "",
            "| Method | Path | Authorization | Purpose and use |",
            "|---|---|---|---|",
        ])
        for entry in catalog.get("publicApiEndpoints", []):
            if entry["ownerService"] != owner:
                continue
            lines.append(
                f"| `{_markdown_cell(entry['method'])}` | `{_markdown_cell(entry['path'])}` | "
                f"`{_markdown_cell(entry['auth'])}` | {_markdown_cell(entry['description'])} "
                f"{_markdown_cell(entry['usage'])} |"
            )
        lines.append("")

    lines.extend([
        "## Test host only",
        "",
        "These routes exist in test fixtures and are not production endpoints.",
        "",
        "| Method | Path | Use |",
        "|---|---|---|",
    ])
    for entry in catalog.get("testOnlyEndpoints", []):
        lines.append(
            f"| `{_markdown_cell(entry['method'])}` | `{_markdown_cell(entry['path'])}` | "
            f"{_markdown_cell(entry['usage'])} |"
        )

    ai = catalog.get("aiEndpoints", {})
    lines.extend(["", "## Agentic AI endpoints", ""])
    if ai.get("status") == "none-implemented":
        lines.append("None implemented. Clients must use the public API; target architecture does not create a live route.")
    else:
        lines.extend(["| Method | Path | Use |", "|---|---|---|"])
        for entry in ai.get("entries", []):
            lines.append(
                f"| `{_markdown_cell(entry['method'])}` | `{_markdown_cell(entry['path'])}` | "
                f"{_markdown_cell(entry['usage'])} |"
            )
    lines.append("")
    return "\n".join(lines)


def validate_catalog(repo_root: Path = REPO_ROOT, *, check_markdown: bool = True) -> list[str]:
    """Return validation errors; an empty list means the catalog is consistent."""

    global REPO_ROOT, CATALOG_PATH, MARKDOWN_PATH, UI_MANIFEST_PATH, CLIENT_ROOTS
    REPO_ROOT = repo_root.resolve()
    CATALOG_PATH = REPO_ROOT / "docs" / "api" / "endpoint-catalog.json"
    MARKDOWN_PATH = REPO_ROOT / "docs" / "api" / "endpoint-catalog.md"
    UI_MANIFEST_PATH = REPO_ROOT / "docs" / "contracts" / "ui-integration.json"
    CLIENT_ROOTS = (
        REPO_ROOT / "apps" / "web" / "src",
        REPO_ROOT / "apps" / "mobile" / "lib",
    )

    errors: list[str] = []
    catalog = _load_json(CATALOG_PATH, errors)
    if catalog is None:
        return errors

    endpoint_by_id, frontend_by_id = _validate_catalog_shape(catalog, errors)
    if errors:
        return errors
    if check_markdown:
        expected_markdown = render_markdown(catalog)
        if not MARKDOWN_PATH.is_file():
            errors.append(f"missing readable endpoint catalog: {_relative(MARKDOWN_PATH)}")
        elif MARKDOWN_PATH.read_text(encoding="utf-8-sig") != expected_markdown:
            errors.append(
                f"readable endpoint catalog is out of date: {_relative(MARKDOWN_PATH)}; "
                "run validate_endpoint_catalog.py --write-markdown"
            )
    _validate_ui_contract(catalog, endpoint_by_id, frontend_by_id, errors)
    _validate_source_routes(catalog, errors)
    _validate_gateway_routes(catalog, errors)
    _validate_client_literals(catalog, errors)
    return errors


def main() -> int:
    if len(sys.argv) > 1:
        if sys.argv[1:] != ["--write-markdown"]:
            print("usage: validate_endpoint_catalog.py [--write-markdown]")
            return 2
        load_errors: list[str] = []
        catalog = _load_json(CATALOG_PATH, load_errors)
        if catalog is None:
            for error in load_errors:
                print(f"- {error}")
            return 1
        load_errors.extend(validate_catalog(check_markdown=False))
        if load_errors:
            for error in load_errors:
                print(f"- {error}")
            return 1
        MARKDOWN_PATH.write_text(render_markdown(catalog), encoding="utf-8")
        print(f"Updated {_relative(MARKDOWN_PATH)}")

    errors = validate_catalog()
    if errors:
        print("Endpoint catalog: FAILED")
        for error in errors:
            print(f"- {error}")
        return 1

    try:
        catalog = json.loads(CATALOG_PATH.read_text(encoding="utf-8-sig"))
        public_count = len(catalog.get("publicApiEndpoints", []))
        frontend_count = len(catalog.get("frontendRoutes", []))
        ai_count = len(catalog.get("aiEndpoints", {}).get("entries", []))
    except (OSError, json.JSONDecodeError, AttributeError):
        public_count = frontend_count = ai_count = 0
    ai_status = "none implemented" if ai_count == 0 else f"{ai_count} documented"
    print(
        f"Endpoint catalog: OK ({public_count} public endpoints, "
        f"{frontend_count} frontend routes; AI endpoints: {ai_status})"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
