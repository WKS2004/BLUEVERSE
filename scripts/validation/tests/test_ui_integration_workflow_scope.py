from __future__ import annotations

import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
WORKFLOW_PATH = REPOSITORY_ROOT / ".github" / "workflows" / "ui-integration.yml"


class UiIntegrationWorkflowScopeTests(unittest.TestCase):
    def test_contract_workflow_covers_client_api_and_gateway_changes(self) -> None:
        workflow = WORKFLOW_PATH.read_text(encoding="utf-8")

        for required_path in (
            '      - "apps/web/**"',
            '      - "apps/mobile/**"',
            '      - "services/**"',
            '      - "docs/contracts/**"',
            '      - "docs/api/**"',
            '      - "docs/architecture/**"',
            '      - "infrastructure/docker/**"',
            '      - "compose.yaml"',
            '      - ".github/workflows/**"',
        ):
            self.assertIn(required_path, workflow)


if __name__ == "__main__":
    unittest.main()
