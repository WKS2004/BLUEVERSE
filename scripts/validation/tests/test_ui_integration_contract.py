from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from scripts.validation.validate_ui_integrations import validate_repository


FOUNDATION_MANIFEST = {
    "schema": 1,
    "status": "foundation",
    "publicApi": {
        "gatewayPath": "/api",
        "entryService": "api",
        "forbiddenClientTargets": ["auth", "agentic-ai", "postgres", "database"],
        "allowedAbsoluteHosts": ["localhost", "127.0.0.1", "::1"],
    },
    "workflows": [
        {
            "id": "foundation-home",
            "status": "starter",
            "web": {"route": "/", "source": "apps/web/src/App.tsx"},
            "mobile": {"route": "/", "source": "apps/mobile/lib/main.dart"},
            "apiRefs": [],
        }
    ],
    "endpoints": [],
}


def write_checkout(
    root: Path,
    web_source: str = "",
    mobile_source: str = "",
    manifest: dict | None = None,
) -> None:
    (root / "docs/contracts").mkdir(parents=True)
    (root / "apps/web/src").mkdir(parents=True)
    (root / "apps/mobile/lib").mkdir(parents=True)
    (root / "docs/contracts/ui-integration.json").write_text(
        json.dumps(manifest or FOUNDATION_MANIFEST),
        encoding="utf-8",
    )
    (root / "apps/web/src/App.tsx").write_text(web_source, encoding="utf-8")
    (root / "apps/mobile/lib/main.dart").write_text(mobile_source, encoding="utf-8")


class UiIntegrationContractTests(unittest.TestCase):
    def test_foundation_starter_is_valid(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            write_checkout(root)
            self.assertEqual(validate_repository(root), [])

    def test_unregistered_frontend_route_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            write_checkout(root, web_source='<Link to="/wrong-route">Wrong</Link>')
            errors = validate_repository(root)
            self.assertTrue(any("frontend route '/wrong-route'" in error for error in errors))

    def test_unregistered_route_configuration_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            write_checkout(root, web_source='createBrowserRouter([{ path: "/wrong-route" }])')
            errors = validate_repository(root)
            self.assertTrue(any("frontend route '/wrong-route'" in error for error in errors))

    def test_unregistered_flutter_navigation_route_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            write_checkout(root, mobile_source="context.go('/wrong-route');")
            errors = validate_repository(root)
            self.assertTrue(any("frontend route '/wrong-route'" in error for error in errors))

    def test_declared_dynamic_frontend_route_is_accepted(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            manifest = json.loads(json.dumps(FOUNDATION_MANIFEST))
            manifest["workflows"][0]["web"]["route"] = "/reports/:reportId"
            write_checkout(root, web_source="navigate('/reports/' + reportId)", manifest=manifest)
            self.assertEqual(validate_repository(root), [])

    def test_direct_internal_api_target_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            write_checkout(root, web_source="fetch('http://auth:8080/login')")
            errors = validate_repository(root)
            self.assertTrue(any("direct internal-service target" in error for error in errors))

    def test_public_api_service_host_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            manifest = json.loads(json.dumps(FOUNDATION_MANIFEST))
            manifest["workflows"][0]["apiRefs"] = ["health.read"]
            manifest["endpoints"] = [
                {
                    "id": "health.read",
                    "method": "GET",
                    "path": "/api/health",
                    "publicBoundary": "api",
                    "ownerService": "api",
                    "operationId": "Health_Read",
                }
            ]
            write_checkout(root, web_source="fetch('http://api:8080/api/health')", manifest=manifest)
            (root / "services/api").mkdir(parents=True)
            (root / "services/api/Program.cs").write_text(
                'app.MapGet("/api/health", Health_Read);',
                encoding="utf-8",
            )
            errors = validate_repository(root)
            self.assertTrue(any("unapproved public API host" in error for error in errors))

    def test_unregistered_public_api_path_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            write_checkout(root, web_source="fetch('/api/not-registered')")
            errors = validate_repository(root)
            self.assertTrue(any("undeclared public API" in error for error in errors))

    def test_dynamic_public_api_target_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            manifest = json.loads(json.dumps(FOUNDATION_MANIFEST))
            manifest["workflows"][0]["apiRefs"] = ["health.read"]
            manifest["endpoints"] = [
                {
                    "id": "health.read",
                    "method": "GET",
                    "path": "/api/health",
                    "publicBoundary": "api",
                    "ownerService": "api",
                    "operationId": "Health_Read",
                }
            ]
            write_checkout(root, web_source="fetch(apiBase + '/api/health')", manifest=manifest)
            (root / "services/api").mkdir(parents=True)
            (root / "services/api/Program.cs").write_text(
                'app.MapGet("/api/health", Health_Read);',
                encoding="utf-8",
            )
            errors = validate_repository(root)
            self.assertTrue(any("dynamic network target" in error for error in errors))

    def test_versioned_api_path_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            write_checkout(root, web_source="fetch('/api/v1/health')")
            errors = validate_repository(root)
            self.assertTrue(any("versioned API" in error for error in errors))

    def test_registered_endpoint_requires_backend_path_evidence(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            manifest = json.loads(json.dumps(FOUNDATION_MANIFEST))
            manifest["workflows"][0]["apiRefs"] = ["health.read"]
            manifest["endpoints"] = [
                {
                    "id": "health.read",
                    "method": "GET",
                    "path": "/api/health",
                    "publicBoundary": "api",
                    "ownerService": "api",
                    "operationId": "Health_Read",
                }
            ]
            write_checkout(root, manifest=manifest)
            (root / "services/api").mkdir(parents=True)
            (root / "services/api/Program.cs").write_text(
                'app.MapGet("/api/not-health", Health_Read);',
                encoding="utf-8",
            )
            errors = validate_repository(root)
            self.assertTrue(any("public path '/api/health'" in error for error in errors))


if __name__ == "__main__":
    unittest.main()
