#!/usr/bin/env python3
"""Validate BLUEVERSE repository agent rules, skills, and relative links."""

from __future__ import annotations

import re
import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
AGENTS_ROOT = REPO_ROOT / ".agents"
SKILLS_ROOT = AGENTS_ROOT / "skills"

REQUIRED_FILES = (
    REPO_ROOT / "AGENTS.md",
    AGENTS_ROOT / "README.md",
    AGENTS_ROOT / "repository-map.md",
    AGENTS_ROOT / "routing.md",
    AGENTS_ROOT / "rules" / "change-safety.md",
    AGENTS_ROOT / "rules" / "validation.md",
)

NAME_PATTERN = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
LINK_PATTERN = re.compile(r"\[[^\]]+\]\(([^)]+)\)")
FRONTMATTER_PATTERN = re.compile(r"\A---\s*\n(.*?)\n---\s*\n", re.DOTALL)


def frontmatter_value(frontmatter: str, key: str) -> str | None:
    match = re.search(rf"(?m)^{re.escape(key)}:\s*(.+?)\s*$", frontmatter)
    if not match:
        return None
    return match.group(1).strip().strip('"\'')


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
        if re.search(r"(?im)\b(?:TODO|TBD|FIXME)\b", content):
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
