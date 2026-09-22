"""Regression cases for the catalog audit findings; mutations never touch checkout files."""

from __future__ import annotations

import copy
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch


ROOT = Path(__file__).resolve().parents[3]
SPEC = importlib.util.spec_from_file_location(
    "catalog_rejections", ROOT / ".agents/scripts/validate_endpoint_catalog.py"
)
assert SPEC and SPEC.loader
VALIDATOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VALIDATOR)


class EndpointCatalogRejectionTests(unittest.TestCase):
    def setUp(self) -> None:
        self.catalog = json.loads((ROOT / "docs/api/endpoint-catalog.json").read_text())
        self.source_changes: dict[str, str] = {}

    def check_catalog(self, stale_markdown: bool = False) -> list[str]:
        original = Path.read_text

        def read(path: Path, *args, **kwargs):
            relative = path.relative_to(ROOT).as_posix()
            if relative == "docs/api/endpoint-catalog.json":
                return json.dumps(self.catalog)
            if relative == "docs/api/endpoint-catalog.md" and not stale_markdown:
                return VALIDATOR.render_markdown(self.catalog)
            if relative in self.source_changes:
                return self.source_changes[relative]
            return original(path, *args, **kwargs)

        with patch.object(Path, "read_text", read):
            return VALIDATOR.validate_catalog(ROOT)

    def endpoint(self, endpoint_id: str) -> dict:
        return next(item for item in self.catalog["publicApiEndpoints"] if item["id"] == endpoint_id)

    def append_api(self, text: str) -> None:
        source = "services/api/Program.cs"
        self.source_changes[source] = (ROOT / source).read_text() + "\n" + text

    def test_undocumented_minimal_api_is_rejected(self) -> None:
        self.append_api('app.MapGet("/api/audit-probe", () => "ok");')
        self.assertTrue(any("missing from endpoint catalog" in error for error in self.check_catalog()))

    def test_documented_literal_minimal_api_is_accepted(self) -> None:
        self.append_api('app.MapGet("/api/audit-probe", () => "ok").RequireAuthorization().WithName("Probe");')
        entry = copy.deepcopy(self.endpoint("api-health"))
        entry.update(id="probe", path="/api/audit-probe", auth="authenticated",
                     operationId="Probe", source="services/api/Program.cs")
        self.catalog["publicApiEndpoints"].append(entry)
        self.assertEqual(self.check_catalog(), [])

    def test_dynamic_and_grouped_minimal_apis_fail_explicitly(self) -> None:
        for declaration in ('app.MapGet(routeName, () => "ok");', 'var group = app.MapGroup("/api/group");'):
            with self.subTest(declaration=declaration):
                self.append_api(declaration)
                self.assertTrue(any("unsupported minimal API" in error for error in self.check_catalog()))

    def test_route_text_inside_comments_is_not_an_endpoint(self) -> None:
        self.append_api('// app.MapGet("/api/comment", () => "ok");')
        self.assertEqual(self.check_catalog(), [])

    def test_unknown_mapping_requires_validator_support(self) -> None:
        self.append_api('app.MapHealthChecks("/api/other-health");')
        self.assertTrue(any("unsupported endpoint mapping" in error for error in self.check_catalog()))

    def test_removed_controller_route_is_rejected(self) -> None:
        source = "services/api/Controllers/HealthController.cs"
        self.source_changes[source] = "namespace Blueverse.Api;"
        self.assertTrue(any("absent from source" in error for error in self.check_catalog()))

    def test_global_authorization_change_requires_validator_support(self) -> None:
        self.append_api('options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();')
        self.assertTrue(any("global authorization policy" in error for error in self.check_catalog()))

    def test_wrong_authorization_is_rejected(self) -> None:
        self.endpoint("auth-logout")["auth"] = "anonymous"
        self.assertTrue(any("auth mismatch" in error for error in self.check_catalog()))

    def test_wrong_permission_is_rejected(self) -> None:
        self.endpoint("auth-roles")["auth"] = "permission:auth.role.manage"
        self.assertTrue(any("auth mismatch" in error for error in self.check_catalog()))

    def test_source_permission_change_is_rejected(self) -> None:
        source = "services/auth/Controllers/RolesController.cs"
        self.source_changes[source] = (ROOT / source).read_text().replace(
            'HasPermission("auth.role.read")', 'HasPermission("auth.role.manage")'
        )
        self.assertTrue(any("auth mismatch" in error for error in self.check_catalog()))

    def test_wrong_owner_is_rejected(self) -> None:
        self.endpoint("api-health")["ownerService"] = "auth"
        self.assertTrue(any("ownerService mismatch" in error for error in self.check_catalog()))

    def test_existing_but_wrong_source_is_rejected(self) -> None:
        self.endpoint("api-health")["source"] = "services/auth/Controllers/HealthControllers.cs"
        self.assertTrue(any("absent from source" in error for error in self.check_catalog()))

    def test_invented_test_route_is_rejected(self) -> None:
        self.catalog["testOnlyEndpoints"][0]["path"] = "/api/test-only/invented"
        self.assertTrue(any("absent from source" in error for error in self.check_catalog()))

    def test_invented_ai_endpoint_is_rejected(self) -> None:
        entry = copy.deepcopy(self.endpoint("api-health"))
        entry.update(id="invented-ai", path="/api/ai/invented", ownerService="ai")
        self.catalog["aiEndpoints"].update(status="implemented", entries=[entry])
        errors = self.check_catalog()
        self.assertTrue(any("absent from source" in error for error in errors))
        self.assertTrue(any("AI endpoint status" in error for error in errors))

    def test_production_endpoint_cannot_be_reclassified_as_test_only(self) -> None:
        entry = self.endpoint("api-health")
        self.catalog["publicApiEndpoints"].remove(entry)
        entry["publicBoundary"] = "test-host"
        self.catalog["testOnlyEndpoints"].append(entry)
        self.assertTrue(any("wrong exposure section" in error for error in self.check_catalog()))

    def test_stale_readable_document_is_rejected(self) -> None:
        self.endpoint("api-health")["usage"] = "Updated operational usage."
        self.assertTrue(any("out of date" in error for error in self.check_catalog(stale_markdown=True)))

    def test_malformed_endpoint_entry_is_rejected_without_crashing(self) -> None:
        self.catalog["publicApiEndpoints"].append(None)
        self.assertTrue(any("entries must be objects" in error for error in self.check_catalog()))

    def test_discovery_classifies_ai_and_test_routes_from_sources(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for relative in ("services/ai/worker/Controller.cs", "services/api/tests/Fixture.cs"):
                source = root / relative
                source.parent.mkdir(parents=True, exist_ok=True)
                source.write_text('[Route("api/probe")] public class Probe : ControllerBase {\n'
                                  '[HttpGet] public IActionResult Get() => Ok();\n}')
            errors: list[str] = []
            routes = VALIDATOR.discover_endpoints(root, errors)
            self.assertEqual(errors, [])
            self.assertEqual({entry["section"] for entry in routes.values()},
                             {"aiEndpoints", "testOnlyEndpoints"})


if __name__ == "__main__":
    unittest.main()
