#!/usr/bin/env python3
"""Increment Pace Atlas's revision after each completed change."""

from pathlib import Path
import re


props = Path(__file__).resolve().parent / "Directory.Build.props"
source = props.read_text(encoding="utf-8")
match = re.search(r"<Version>(\d+)\.(\d+)\.(\d+)</Version>", source)
if match is None:
    raise SystemExit("Version in Directory.Build.props fehlt oder ist ungültig.")

major, minor, revision = map(int, match.groups())
if revision > 10:
    raise SystemExit("Die dritte Versionsstelle darf höchstens 10 sein.")
revision += 1
if revision > 10:
    minor += 1
    revision = 0
version = f"{major}.{minor}.{revision}"
props.write_text(source[:match.start()] + f"<Version>{version}</Version>" + source[match.end():], encoding="utf-8")
print(version)
