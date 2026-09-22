from __future__ import annotations

import importlib.util
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[3]
VALIDATOR_PATH = REPO_ROOT / ".agents/scripts/validate_endpoint_catalog.py"
SPEC = importlib.util.spec_from_file_location("blueverse_endpoint_catalog", VALIDATOR_PATH)
assert SPEC is not None and SPEC.loader is not None
VALIDATOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VALIDATOR)


class EndpointCatalogTests(unittest.TestCase):
    def test_current_catalog_matches_repository_sources(self) -> None:
        self.assertEqual(VALIDATOR.validate_catalog(REPO_ROOT), [])

    def test_client_literal_matches_route_template(self) -> None:
        self.assertTrue(
            VALIDATOR._path_matches_literal(
                "/api/auth/00000000-0000-0000-0000-000000000000",
                "/api/auth/{id:guid}",
            )
        )


if __name__ == "__main__":
    unittest.main()
