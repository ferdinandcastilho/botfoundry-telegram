#!/usr/bin/env bash
# Computes the next stable version from the latest stable tag (vX.Y.Z) and the
# bump kind (major | minor | patch). Prints "X.Y.Z" on stdout.
#
# Usage: next-version.sh <bump> [tag-list-file]
#   tag-list-file: optional file with one tag per line (used by tests);
#                  defaults to `git tag --list 'v*'`.
set -euo pipefail

bump="${1:-patch}"
tags_file="${2:-}"

if [ -n "$tags_file" ]; then
  tags="$(cat "$tags_file")"
else
  tags="$(git tag --list 'v*')"
fi

# Stable tags only (vX.Y.Z), highest first.
latest="$(printf '%s\n' "$tags" | grep -E '^v[0-9]+\.[0-9]+\.[0-9]+$' | sort -V | tail -n 1 || true)"

if [ -z "$latest" ]; then
  latest="v0.0.0"
fi

IFS='.' read -r major minor patch <<< "${latest#v}"

case "$bump" in
  major) major=$((major + 1)); minor=0; patch=0 ;;
  minor) minor=$((minor + 1)); patch=0 ;;
  patch) patch=$((patch + 1)) ;;
  *) echo "Unknown bump kind: $bump" >&2; exit 1 ;;
esac

echo "${major}.${minor}.${patch}"
