"""Requirements for Marine's shared compilation and public health wiring."""
import json
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[3]


class MarineIntegrationWiringTests(unittest.TestCase):
    def test_shared_contract_changes_trigger_both_events_and_scope_checks(self):
        for name in ("backend-ci", "backend-tests", "ui-integration"):
            with self.subTest(workflow=name):
                text = (ROOT / f".github/workflows/{name}.yml").read_text(encoding="utf-8")
                self.assertEqual(2, text.count('- "infrastructure/contracts/**"'))
                self.assertRegex(text, r"grep -Eq '[^']*infrastructure/contracts/")

    def test_shared_contract_changes_trigger_docker_build_events(self):
        text = (ROOT / ".github/workflows/docker-backend-build.yml").read_text(encoding="utf-8")
        self.assertEqual(2, text.count('- "infrastructure/contracts/**"'))
        for event in ("push", "pull_request"):
            block = re.search(rf"^  {event}:\n(.*?)(?=^  [a-z_]+:|^permissions:)", text, re.M | re.S)
            self.assertIsNotNone(block)
            self.assertIn('- "infrastructure/contracts/**"', block.group(1))

    def test_component_health_route_preserves_existing_service_routes(self):
        text = (ROOT / ".github/workflows/docker-stack-health.yml").read_text(encoding="utf-8")
        mapping = re.search(r'public_route="\$service_name".*?\n\s*fi', text, re.S)
        self.assertIsNotNone(mapping)
        git_bash = Path("C:/Program Files/Git/bin/bash.exe")
        bash = str(git_bash) if git_bash.is_file() else shutil.which("bash")
        self.assertIsNotNone(bash, "Bash is required to validate the actual health mapping.")
        for service, route in (("marine-safety", "marine"), ("api", "api"), ("auth", "auth"), ("experience-biodiversity", "experiences")):
            with self.subTest(service=service):
                result = subprocess.run([bash, "--noprofile", "--norc", "-c",
                    f'service_name="{service}"\n{mapping.group()}\nprintf "%s" "$public_route"'],
                    capture_output=True, text=True, check=True)
                self.assertEqual(route, result.stdout)
        catalog = json.loads((ROOT / "docs/api/endpoint-catalog.json").read_text(encoding="utf-8"))
        self.assertTrue(any(row["method"] == "GET" and row["path"] == "/api/marine/health"
                            for row in catalog["publicApiEndpoints"]))

    def test_swagger_checks_include_only_implemented_member_services(self):
        text = (ROOT / ".github/workflows/docker-stack-health.yml").read_text(encoding="utf-8")
        block = re.search(r"swagger_paths=\(.*?\n\s*for swagger_path", text, re.S)
        self.assertIsNotNone(block)
        script = block.group().rsplit("for swagger_path", 1)[0]
        bash = "C:/Program Files/Git/bin/bash.exe" if Path("C:/Program Files/Git/bin/bash.exe").is_file() else shutil.which("bash")
        for members, expected in (((), ["/api/swagger", "/api/swagger/v1.json", "/api/auth/swagger/v1/swagger.json"]),
                (("marine-safety",), ["/api/swagger", "/api/swagger/v1.json", "/api/auth/swagger/v1/swagger.json", "/api/marine/swagger/v1/swagger.json"]),
                (("experience-biodiversity", "marine-safety"), ["/api/swagger", "/api/swagger/v1.json", "/api/auth/swagger/v1/swagger.json", "/api/experiences/swagger/v1/swagger.json", "/api/marine/swagger/v1/swagger.json"])):
            with self.subTest(member_sources_present=members):
                with tempfile.TemporaryDirectory() as temporary:
                    root = Path(temporary)
                    if members:
                        for service in members:
                            target = root / "services" / service / "Program.cs"
                            target.parent.mkdir(parents=True)
                            target.write_text("// Fixture source marker", encoding="utf-8")
                    result = subprocess.run([bash, "--noprofile", "--norc", "-c", script + '\nprintf "%s\\n" "${swagger_paths[@]}"'], cwd=root, capture_output=True, text=True, check=True)
                    self.assertEqual(expected, result.stdout.splitlines())

    def test_stack_keys_are_ephemeral_masked_and_exported_before_compose(self):
        text = (ROOT / ".github/workflows/docker-stack-health.yml").read_text(encoding="utf-8")
        for key in ("JWT_SIGNING_KEY", "MARINE_ACTOR_SIGNING_KEY"):
            self.assertIn(f'{key}="$(openssl rand -hex 32)"', text)
            self.assertIn(f'echo "::add-mask::${key}"', text)
            self.assertLess(text.index(f'{key}="$(openssl'), text.index('config --quiet'))
        self.assertIn("export JWT_SIGNING_KEY MARINE_ACTOR_SIGNING_KEY", text)
        self.assertIn("infrastructure/contracts/*", text)

    def test_both_docker_builds_include_the_linked_contract_at_its_project_path(self):
        for service, project in (("api", "Blueverse.Api"), ("marine-safety", "Blueverse.MarineSafety")):
            with self.subTest(service=service):
                dockerfile = (ROOT / f"infrastructure/docker/{service}/Dockerfile").read_text(encoding="utf-8")
                self.assertIn(f"WORKDIR /src/services/{service}", dockerfile)
                self.assertIn("COPY infrastructure/contracts/ /src/infrastructure/contracts/", dockerfile)
                project_text = (ROOT / f"services/{service}/{project}.csproj").read_text(encoding="utf-8")
                self.assertIn('../../infrastructure/contracts/MarineActorContext.cs', project_text)
                self.assertTrue((ROOT / "infrastructure/contracts/MarineActorContext.cs").is_file())


if __name__ == "__main__":
    unittest.main()
