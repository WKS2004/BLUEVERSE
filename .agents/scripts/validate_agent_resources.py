#!/usr/bin/env python3
"""Validate BLUEVERSE repository agent rules, skills, and relative links."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
AGENTS_ROOT = REPO_ROOT / ".agents"
SKILLS_ROOT = AGENTS_ROOT / "skills"
REGISTRY_PATH = AGENTS_ROOT / "registry" / "skills.json"
NOTICE_PATH = AGENTS_ROOT / "registry" / "THIRD-PARTY-NOTICES.md"
OVERLAYS_ROOT = AGENTS_ROOT / "skill-overlays"
EVALS_ROOT = AGENTS_ROOT / "evals"

REQUIRED_FILES = (
    REPO_ROOT / "AGENTS.md",
    AGENTS_ROOT / "README.md",
    AGENTS_ROOT / "repository-map.md",
    AGENTS_ROOT / "routing.md",
    AGENTS_ROOT / "rules" / "change-safety.md",
    AGENTS_ROOT / "rules" / "data-access.md",
    AGENTS_ROOT / "rules" / "validation.md",
    REGISTRY_PATH,
    NOTICE_PATH,
    EVALS_ROOT / "routing-cases.json",
)

NAME_PATTERN = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
LINK_PATTERN = re.compile(r"\[[^\]]+\]\(([^)]+)\)")
FRONTMATTER_PATTERN = re.compile(r"\A---\s*\n(.*?)\n---\s*\n", re.DOTALL)
SECRET_ASSIGNMENT_PATTERN = re.compile(
    r"(?i)\b(?:api[_-]?key|access[_-]?token|jwt[_-]?secret)\b\s*[:=]\s*['\"][^'\"]{16,}"
)
GENERATED_DIRECTORY_NAMES = {
    ".git",
    "bin",
    "obj",
    "node_modules",
    ".dart_tool",
    "build",
    "dist",
    "coverage",
    ".gradle",
    "__pycache__",
    "TestResults",
}


def frontmatter_value(frontmatter: str, key: str) -> str | None:
    match = re.search(rf"(?m)^{re.escape(key)}:\s*(.+?)\s*$", frontmatter)
    if not match:
        return None
    return match.group(1).strip().strip('"\'')


def relative_path(path: Path) -> str:
    return str(path.relative_to(REPO_ROOT)).replace("\\", "/")


def is_within(path: Path, parent: Path) -> bool:
    try:
        path.relative_to(parent)
    except ValueError:
        return False
    return True


def is_generated_path(path: Path) -> bool:
    try:
        parts = path.relative_to(REPO_ROOT).parts
    except ValueError:
        return False
    return any(part in GENERATED_DIRECTORY_NAMES for part in parts)


def read_json(path: Path, errors: list[str]) -> object | None:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        errors.append(f"invalid JSON in {relative_path(path)}: {error}")
        return None


def validate_registry(errors: list[str], skill_names: set[str]) -> None:
    if not REGISTRY_PATH.is_file():
        return
    registry = read_json(REGISTRY_PATH, errors)
    if not isinstance(registry, dict):
        errors.append("skill registry must contain a JSON object")
        return
    if registry.get("schema") != 1:
        errors.append("skill registry schema must be 1")
    policy = registry.get("policy")
    if not isinstance(policy, dict) or policy.get("projectRulesWin") is not True:
        errors.append("skill registry must declare projectRulesWin=true")

    entries = registry.get("skills")
    if not isinstance(entries, list):
        errors.append("skill registry 'skills' must be a list")
        return

    registered_names: set[str] = set()
    for entry in entries:
        if not isinstance(entry, dict):
            errors.append("skill registry entries must be objects")
            continue
        skill_id = entry.get("id")
        if not isinstance(skill_id, str) or not skill_id:
            errors.append("skill registry entry is missing a string id")
            continue
        if skill_id in registered_names:
            errors.append(f"duplicate skill registry id: {skill_id}")
        registered_names.add(skill_id)

        status = entry.get("status")
        if status not in {"project", "imported"}:
            errors.append(f"invalid status for registry skill {skill_id}: {status!r}")
        local_path = entry.get("localPath")
        if not isinstance(local_path, str) or not local_path:
            errors.append(f"missing localPath for registry skill {skill_id}")
            continue
        resolved = (REPO_ROOT / local_path).resolve()
        if not is_within(resolved, SKILLS_ROOT.resolve()):
            errors.append(f"registry path escapes .agents/skills for {skill_id}: {local_path}")
        if not (resolved / "SKILL.md").is_file():
            errors.append(f"registry path has no SKILL.md for {skill_id}: {local_path}")

        if status == "imported":
            source = entry.get("source")
            if not isinstance(source, dict):
                errors.append(f"imported skill lacks source metadata: {skill_id}")
                continue
            for required in ("repository", "path", "revision", "license"):
                if not source.get(required):
                    errors.append(f"imported skill {skill_id} lacks source.{required}")
            revision = source.get("revision")
            if not isinstance(revision, str) or not re.fullmatch(r"[0-9a-f]{40}", revision):
                errors.append(f"imported skill {skill_id} must use a 40-character pinned revision")
            if not entry.get("compatibility"):
                errors.append(f"imported skill lacks compatibility notes: {skill_id}")

    for name in sorted(skill_names - registered_names):
        errors.append(f"skill is missing from registry: {name}")
    for name in sorted(registered_names - skill_names):
        errors.append(f"registry points to undiscovered skill: {name}")

    deferred = registry.get("deferred", [])
    if not isinstance(deferred, list):
        errors.append("skill registry 'deferred' must be a list")
        return
    for entry in deferred:
        if not isinstance(entry, dict) or not isinstance(entry.get("id"), str):
            errors.append("deferred skill entries must contain a string id")
            continue
        skill_id = entry["id"]
        if skill_id in registered_names:
            errors.append(f"deferred skill is already imported: {skill_id}")
        if not isinstance(entry.get("reason"), str) or not entry["reason"].strip():
            errors.append(f"deferred skill lacks a reason: {skill_id}")
        source = entry.get("source")
        if not isinstance(source, dict):
            errors.append(f"deferred skill lacks source metadata: {skill_id}")
            continue
        for required in ("repository", "path", "license"):
            if not source.get(required):
                errors.append(f"deferred skill {skill_id} lacks source.{required}")
        revision = source.get("revision")
        if not isinstance(revision, str) or not re.fullmatch(r"[0-9a-f]{40}", revision):
            errors.append(f"deferred skill must use a 40-character pinned revision: {skill_id}")


def validate_overlays(errors: list[str]) -> None:
    if not OVERLAYS_ROOT.is_dir():
        errors.append("missing skill overlay directory: .agents/skill-overlays")
        return
    for overlay in sorted(OVERLAYS_ROOT.rglob("*.md")):
        content = overlay.read_text(encoding="utf-8")
        match = FRONTMATTER_PATTERN.match(content)
        if not match:
            errors.append(f"overlay is missing YAML frontmatter: {relative_path(overlay)}")
            continue
        frontmatter = match.group(1)
        if frontmatter_value(frontmatter, "core") != "dotnet-test/run-tests":
            errors.append(f"overlay core is invalid: {relative_path(overlay)}")
        if frontmatter_value(frontmatter, "binding-revision") != "1":
            errors.append(f"overlay binding revision is invalid: {relative_path(overlay)}")
        if frontmatter_value(frontmatter, "mode") != "extend":
            errors.append(f"overlay mode must be extend: {relative_path(overlay)}")


def validate_evals(errors: list[str], skill_names: set[str]) -> None:
    routing_cases = EVALS_ROOT / "routing-cases.json"
    if not routing_cases.is_file():
        return
    data = read_json(routing_cases, errors)
    if not isinstance(data, dict) or data.get("schema") != 1:
        errors.append("routing evaluation schema must be 1")
        return
    cases = data.get("cases")
    if not isinstance(cases, list) or not cases:
        errors.append("routing evaluation must contain a non-empty cases list")
        return
    case_ids: set[str] = set()
    for case in cases:
        if not isinstance(case, dict):
            errors.append("routing evaluation cases must be objects")
            continue
        case_id = case.get("id")
        if not isinstance(case_id, str) or not case_id:
            errors.append("routing evaluation case lacks an id")
        elif case_id in case_ids:
            errors.append(f"duplicate routing evaluation case: {case_id}")
        else:
            case_ids.add(case_id)
        if not isinstance(case.get("task"), str) or not case["task"].strip():
            errors.append(f"routing evaluation case lacks task text: {case_id}")
        expected = case.get("expectedSkills")
        if not isinstance(expected, list) or not expected:
            errors.append(f"routing evaluation case lacks expectedSkills: {case_id}")
        else:
            unknown = sorted(set(expected) - skill_names)
            if unknown:
                errors.append(f"routing evaluation case has unknown skills {unknown}: {case_id}")
        if not isinstance(case.get("expectedRules"), list) or not case["expectedRules"]:
            errors.append(f"routing evaluation case lacks expectedRules: {case_id}")
        if not isinstance(case.get("mustExclude"), list) or not case["mustExclude"]:
            errors.append(f"routing evaluation case lacks mustExclude: {case_id}")


def validate_agent_files(errors: list[str]) -> None:
    for path in AGENTS_ROOT.rglob("*"):
        if not path.is_file():
            continue
        if is_generated_path(path):
            errors.append(f"generated/cache file must not be under .agents: {relative_path(path)}")
        if path.suffix.lower() == ".md":
            content = path.read_text(encoding="utf-8")
            if SECRET_ASSIGNMENT_PATTERN.search(content):
                errors.append(f"possible secret assignment in {relative_path(path)}")


def validate() -> list[str]:
    errors: list[str] = []

    for path in REQUIRED_FILES:
        if not path.is_file():
            errors.append(f"missing required agent file: {path.relative_to(REPO_ROOT)}")

    root_instructions_path = REPO_ROOT / "AGENTS.md"
    if root_instructions_path.is_file():
        root_instructions = root_instructions_path.read_text(encoding="utf-8")
        if ".agents/routing.md" not in root_instructions:
            errors.append("AGENTS.md must route tasks through .agents/routing.md")

    skill_names: set[str] = set()
    if not SKILLS_ROOT.is_dir():
        errors.append("missing repository skill directory: .agents/skills")
        skill_directories: list[Path] = []
    else:
        skill_directories = sorted(path for path in SKILLS_ROOT.iterdir() if path.is_dir())
    if not skill_directories:
        errors.append("no repository skills found under .agents/skills")

    for skill_directory in skill_directories:
        skill_file = skill_directory / "SKILL.md"
        if not skill_file.is_file():
            errors.append(f"missing SKILL.md: {skill_directory.relative_to(REPO_ROOT)}")
            continue

        folder_name = skill_directory.name
        if len(folder_name) > 64 or not NAME_PATTERN.fullmatch(folder_name):
            errors.append(f"invalid skill folder name: {folder_name}")

        content = skill_file.read_text(encoding="utf-8")
        frontmatter_match = FRONTMATTER_PATTERN.match(content)
        if not frontmatter_match:
            errors.append(f"missing YAML frontmatter: {skill_file.relative_to(REPO_ROOT)}")
            continue

        frontmatter = frontmatter_match.group(1)
        keys = set(re.findall(r"(?m)^([A-Za-z0-9_-]+):", frontmatter))
        unexpected_keys = keys - {"name", "description", "license", "allowed-tools", "metadata"}
        if unexpected_keys:
            errors.append(
                f"unexpected frontmatter keys in {folder_name}: {', '.join(sorted(unexpected_keys))}"
            )
        name = frontmatter_value(frontmatter, "name")
        description = frontmatter_value(frontmatter, "description")
        if name != folder_name:
            errors.append(f"skill name must match folder {folder_name}: {name!r}")
        if not description:
            errors.append(f"missing skill description: {skill_file.relative_to(REPO_ROOT)}")
        elif len(description) > 300:
            errors.append(f"skill description exceeds 300 characters: {folder_name}")
        elif "<" in description or ">" in description:
            errors.append(f"skill description contains angle brackets: {folder_name}")
        if name in skill_names:
            errors.append(f"duplicate skill name: {name}")
        if name:
            skill_names.add(name)
        if re.search(r"(?m)^\s*(?:TODO|TBD|FIXME)(?::|\s)", content):
            errors.append(f"unfinished placeholder in skill: {folder_name}")

    markdown_files = [
        path
        for path in [REPO_ROOT / "AGENTS.md", *sorted(AGENTS_ROOT.rglob("*.md"))]
        if path.is_file()
    ]
    for markdown_file in markdown_files:
        content = markdown_file.read_text(encoding="utf-8")
        for match in LINK_PATTERN.finditer(content):
            target = match.group(1).strip().strip("<>")
            if target.startswith(("http://", "https://", "mailto:", "#")):
                continue
            path_text = target.split("#", 1)[0]
            if not path_text:
                continue
            linked_path = (markdown_file.parent / path_text).resolve()
            if not linked_path.exists():
                errors.append(
                    f"broken relative link in {markdown_file.relative_to(REPO_ROOT)}: {target}"
                )

    validate_registry(errors, skill_names)
    validate_overlays(errors)
    validate_evals(errors, skill_names)
    validate_agent_files(errors)

    return errors


def main() -> int:
    errors = validate()
    if errors:
        print("Agent resource validation FAILED")
        for error in errors:
            print(f"- {error}")
        return 1

    skill_count = sum(1 for path in SKILLS_ROOT.iterdir() if path.is_dir())
    print(f"Agent resources: OK ({skill_count} repository skills validated)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
