#!/usr/bin/env python3
"""Increment Pace Atlas's revision after each completed change."""

from pathlib import Path
import json
import re


props = Path(__file__).resolve().parent / "Directory.Build.props"
source = props.read_text(encoding="utf-8")
match = re.search(r"<Version>(\d+)\.(\d+)\.(\d+)(?:\.(\d+))?</Version>", source)
if match is None:
    raise SystemExit("Version in Directory.Build.props fehlt oder ist ungültig.")

major, minor, revision = map(int, match.groups()[:3])
build = match.group(4)
if build is None:
    if revision > 10:
        raise SystemExit("Die dritte Versionsstelle darf höchstens 10 sein.")
    revision += 1
    if revision > 10:
        minor += 1
        revision = 0
    version = f"{major}.{minor}.{revision}"
else:
    version = f"{major}.{minor}.{revision}.{int(build) + 1}"
# Prepare all version updates before writing any project file.
updates = []
raycast = props.parent / "PaceAtlas.Raycast"
for name in ("package.json", "package-lock.json"):
    path = raycast / name
    if name == "package-lock.json" and not path.exists():
        continue
    text = path.read_text(encoding="utf-8")
    data = json.loads(text)
    # Only the package's own versions precede the dependency entries.
    count = 1
    if name == "package-lock.json" and "" in data.get("packages", {}):
        count = 2
    updated, replaced = re.subn(
        r'("version"\s*:\s*")[^"]*(")',
        lambda m: m.group(1) + version + m.group(2),
        text,
        count=count,
    )
    if replaced != count:
        raise SystemExit(f"Versionsangabe fehlt in {path}.")
    parsed = json.loads(updated)
    if parsed.get("version") != version or (
        count == 2 and parsed["packages"][""].get("version") != version
    ):
        raise SystemExit(f"Versionsstruktur in {path} ist ungültig.")
    updates.append((path, updated))

props.write_text(source[:match.start()] + f"<Version>{version}</Version>" + source[match.end():], encoding="utf-8")
for path, text in updates:
    path.write_text(text, encoding="utf-8")
print(version)
