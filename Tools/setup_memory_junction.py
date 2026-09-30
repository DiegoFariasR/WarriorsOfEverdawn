#!/usr/bin/env python3
"""
Wire Claude Code's auto-memory directory to the in-repo .claude/memory/ folder.

Claude Code stores per-project memory under
  ~/.claude/projects/<encoded-repo-path>/memory/

This script replaces that directory with a junction (Windows) or symlink (Unix)
pointing at <repo>/.claude/memory/ so:
  - /close-chat memory writes land in the repo (git-trackable)
  - reads on every session pull from the same place
  - every session also gets the memories through CLAUDE.md's @-include, junction or not

Idempotent: re-running is safe. Pre-existing local memory files are backed up
to <auto-memory-dir>.bak.<timestamp>/ before the junction is created.

Usage:
  python Tools/setup_memory_junction.py              # uses CWD as repo root
  python Tools/setup_memory_junction.py --dry-run    # print what would happen
"""

from __future__ import annotations

import argparse
import os
import shutil
import sys
import time
from pathlib import Path


def encode_repo_path(repo_root: Path) -> str:
    """C:\\Users\\Diego\\Projects\\WarriorsOfEverdawn -> c--Users-Diego-Projects-WarriorsOfEverdawn"""
    s = str(repo_root.resolve())
    if os.name == "nt":
        drive, rest = os.path.splitdrive(s)
        drive = drive.rstrip(":").lower()
        rest = rest.replace("\\", "-").replace("/", "-")
        return f"{drive}-{rest}".rstrip("-")
    return s.replace("/", "-").lstrip("-")


def auto_memory_dir(repo_root: Path) -> Path:
    home = Path.home()
    return home / ".claude" / "projects" / encode_repo_path(repo_root) / "memory"


def is_link_or_junction(path: Path) -> bool:
    if path.is_symlink():
        return True
    if os.name == "nt" and path.exists():
        try:
            attrs = os.lstat(str(path)).st_file_attributes  # type: ignore[attr-defined]
        except AttributeError:
            return False
        return bool(attrs & 0x400)
    return False


def link_target(path: Path) -> Path | None:
    try:
        target = os.readlink(str(path))
    except OSError:
        return None
    # Windows reports junction targets in extended-length form (\\?\C:\...), which never equals a plain path.
    return Path(target.removeprefix("\\\\?\\")).resolve()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--repo-root",
        type=Path,
        default=Path.cwd(),
        help="repo root (defaults to current working directory)",
    )
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    repo_root: Path = args.repo_root.resolve()
    repo_mem = repo_root / ".claude" / "memory"
    if not repo_mem.is_dir():
        print(f"error: {repo_mem} does not exist", file=sys.stderr)
        return 2

    target = auto_memory_dir(repo_root)
    print(f"repo memory   : {repo_mem}")
    print(f"auto-memory   : {target}")

    if is_link_or_junction(target):
        existing = link_target(target)
        if existing and existing == repo_mem.resolve():
            print("already a junction pointing at repo memory -- nothing to do.")
            return 0
        print(
            f"existing link/junction points elsewhere: {existing} -- "
            f"refusing to overwrite. Remove it manually if intended.",
            file=sys.stderr,
        )
        return 1

    if target.exists():
        files = list(target.iterdir())
        if files:
            stamp = time.strftime("%Y%m%d-%H%M%S")
            backup = target.parent / f"{target.name}.bak.{stamp}"
            print(f"backing up {len(files)} existing file(s) -> {backup}")
            if not args.dry_run:
                shutil.move(str(target), str(backup))
        else:
            print("removing empty auto-memory directory")
            if not args.dry_run:
                target.rmdir()
    else:
        target.parent.mkdir(parents=True, exist_ok=True)

    if args.dry_run:
        print("dry-run -- would create junction now")
        return 0

    if os.name == "nt":
        rc = os.system(f'mklink /J "{target}" "{repo_mem}" >NUL')
        if rc != 0:
            print(f"error: mklink failed with rc={rc}", file=sys.stderr)
            return 1
    else:
        os.symlink(repo_mem, target)

    print("junction created. /close-chat writes will now land in <repo>/.claude/memory/.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
