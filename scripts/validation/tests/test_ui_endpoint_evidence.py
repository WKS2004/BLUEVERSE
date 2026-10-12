import copy
import json
import tempfile
import unittest
from pathlib import Path

from scripts.validation.tests.test_ui_integration_contract import FOUNDATION_MANIFEST, write_checkout
from scripts.validation.validate_ui_integrations import validate_repository


class EndpointEvidenceTests(unittest.TestCase):
    def check_source(self, source: str, *, relative='services/api/Program.cs') -> list[str]:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            manifest = copy.deepcopy(FOUNDATION_MANIFEST)
            manifest['workflows'][0]['apiRefs'] = ['health']
            manifest['endpoints'] = [{'id': 'health', 'method': 'GET', 'path': '/api/health',
                'publicBoundary': 'api', 'ownerService': 'api', 'operationId': 'Health_Read'}]
            write_checkout(root, manifest=manifest)
            file = root / relative
            file.parent.mkdir(parents=True, exist_ok=True)
            file.write_text(source, encoding='utf-8')
            return validate_repository(root)

    def test_same_suffix_under_wrong_prefix_is_rejected(self):
        self.assertTrue(any('public path' in e for e in self.check_source(
            'app.MapGet("/internal/health", Health_Read);')))

    def test_wrong_http_method_is_rejected(self):
        self.assertTrue(any('public path' in e for e in self.check_source(
            'app.MapPost("/api/health", Health_Read);')))

    def test_comment_and_wrong_handler_do_not_bind_operation(self):
        self.assertTrue(any('public path' in e for e in self.check_source(
            '// Health_Read\napp.MapGet("/api/health", OtherHandler);')))

    def test_test_only_mapping_is_rejected(self):
        self.assertTrue(any('public path' in e for e in self.check_source(
            'app.MapGet("/api/health", Health_Read);', relative='services/api/tests/Fixture.cs')))

    def test_exact_minimal_handler_is_accepted(self):
        self.assertEqual(self.check_source('app.MapGet("/api/health", Health_Read);'), [])

    def test_openapi_exact_operation_is_accepted(self):
        self.assertEqual(self.check_source(json.dumps({'openapi': '3.0.3', 'paths': {
            '/api/health': {'get': {'operationId': 'Health_Read'}}}}),
            relative='services/api/openapi.json'), [])

    def test_openapi_wrong_method_is_rejected(self):
        errors = self.check_source(json.dumps({'openapi': '3.0.3', 'paths': {
            '/api/health': {'post': {'operationId': 'Health_Read'}}}}),
            relative='services/api/openapi.json')
        self.assertTrue(any('public path' in e for e in errors))
