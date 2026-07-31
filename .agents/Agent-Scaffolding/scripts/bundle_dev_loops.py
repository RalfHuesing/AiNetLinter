#!/usr/bin/env python3
"""
bundle_dev_loops.py

Fasst alle Markdown-Dateien pro dev-loop Unterverzeichnis (z.B. drift-loop, planning)
in eine einzelne Sammel-Markdown-Datei unter temp/<unterverzeichnis>.md zusammen.
"""

import re
from pathlib import Path


def get_sort_key(path: Path, base_dir: Path) -> tuple:
    rel_str = str(path.relative_to(base_dir)).replace("\\", "/")
    name = path.name.lower()
    
    if name == "readme.md":
        priority = 0
    elif name == "spec.md":
        priority = 1
    elif name == "orchestrator.md":
        priority = 2
    elif "skills/" in rel_str:
        priority = 3
    elif "templates/" in rel_str:
        priority = 4
    else:
        priority = 5
        
    return (priority, rel_str)


def generate_fence(content: str) -> str:
    matches = re.findall(r"`+", content)
    max_ticks = max((len(m) for m in matches), default=0)
    num_ticks = max(4, max_ticks + 1)
    return "`" * num_ticks


def get_skill_name_label(rel_path_str: str) -> str:
    match = re.search(r"skills/([^/]+)", rel_path_str, re.IGNORECASE)
    if match:
        return f" (Skill: {match.group(1)})"
    return ""


def slugify(text: str) -> str:
    slug = text.lower()
    slug = re.sub(r"[^\w\s-]", "", slug)
    slug = re.sub(r"[\s]+", "-", slug).strip("-")
    return slug


def main():
    repo_root = Path(__file__).resolve().parent.parent
    dev_loop_dir = repo_root / "dev-loop"
    temp_dir = repo_root / "temp"

    if not dev_loop_dir.is_dir():
        print(f"Fehler: Verzeichnis {dev_loop_dir} nicht gefunden.")
        return

    temp_dir.mkdir(parents=True, exist_ok=True)

    subdirs = [d for d in dev_loop_dir.iterdir() if d.is_dir() and not d.name.startswith(".")]

    if not subdirs:
        print(f"Keine Unterverzeichnisse in {dev_loop_dir} gefunden.")
        return

    print(f"Gefundene Flow-Verzeichnisse: {[d.name for d in subdirs]}")

    for sub_dir in sorted(subdirs, key=lambda d: d.name):
        md_files = list(sub_dir.rglob("*.md"))
        if not md_files:
            continue

        md_files.sort(key=lambda p: get_sort_key(p, sub_dir))

        bundle_path = temp_dir / f"{sub_dir.name}.md"
        rel_flow_dir = f"dev-loop/{sub_dir.name}"

        lines = []
        lines.append(f"# Sammel-Dokumentation: {rel_flow_dir}\n")
        lines.append(f"> Automatisch generiert aus `{rel_flow_dir}/` ({len(md_files)} Markdown-Dateien).\n")
        lines.append("## Inhaltsverzeichnis\n")

        for p in md_files:
            rel_file = str(p.relative_to(repo_root)).replace("\\", "/")
            skill_label = get_skill_name_label(rel_file)
            title = f"{rel_file}{skill_label}"
            anchor = slugify(title)
            lines.append(f"- [{title}](#{anchor})")

        lines.append("\n---\n")

        for p in md_files:
            rel_file = str(p.relative_to(repo_root)).replace("\\", "/")
            skill_label = get_skill_name_label(rel_file)

            try:
                content = p.read_text(encoding="utf-8")
            except Exception as e:
                content = f"[Fehler beim Lesen der Datei: {e}]"

            fence = generate_fence(content)

            lines.append(f"## {rel_file}{skill_label}\n")
            lines.append(f"{fence}markdown")
            lines.append(content.rstrip())
            lines.append(f"{fence}\n")
            lines.append("---\n")

        bundle_path.write_text("\n".join(lines), encoding="utf-8")
        print(f"Erstellt: {bundle_path.relative_to(repo_root)} ({len(md_files)} Dateien gekapselt)")

    print("Fertig!")


if __name__ == "__main__":
    main()
