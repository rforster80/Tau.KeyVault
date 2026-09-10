#!/usr/bin/env bash
set -euo pipefail

# Copy this to publish-pypi.sh (gitignored) and fill in your repository and token.

# -----------------------------
# HARD-CODED CONFIG
# -----------------------------
REPO_URL_DEFAULT="https://pypi.yoursite.com/simple/"

# PyPI-style token auth: username is literally __token__.
# Prefer exporting PYPI_TOKEN instead of committing one here.
TOKEN_DEFAULT="${PYPI_TOKEN:-your-pypi-token}"

DRY_RUN=false   # set true to build and check without uploading

REPO_URL="$REPO_URL_DEFAULT"
TOKEN="$TOKEN_DEFAULT"

usage() {
  cat <<USAGE
Usage: $(basename "$0") [-r <repository-url>] [-t <token>] [-n]

Defaults:
  -r  $REPO_URL_DEFAULT
  -t  \$PYPI_TOKEN, else the placeholder in this file
  -n  dry run — build and twine check only, no upload

Example:
  PYPI_TOKEN=pypi-xxxx ./publish-pypi.sh -r "https://upload.pypi.org/legacy/"
USAGE
}

while getopts ":r:t:nh" opt; do
  case "$opt" in
    r) REPO_URL="$OPTARG" ;;
    t) TOKEN="$OPTARG" ;;
    n) DRY_RUN=true ;;
    h) usage; exit 0 ;;
    \?) echo "Invalid option: -$OPTARG" >&2; usage; exit 1 ;;
    :)  echo "Option -$OPTARG requires an argument." >&2; usage; exit 1 ;;
  esac
done

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VERSION_FILE="$PROJECT_ROOT/pypi_version.txt"
PYPROJECT="$PROJECT_ROOT/pyproject.toml"
PYTHON="${PYTHON:-python3}"

command -v "$PYTHON" >/dev/null 2>&1 || { echo "$PYTHON not found on PATH" >&2; exit 1; }

if [[ ! -f "$VERSION_FILE" ]]; then
  echo "pypi_version.txt not found in project root: $PROJECT_ROOT" >&2
  echo "Copy sample_pypi_version.txt to pypi_version.txt to start." >&2
  exit 1
fi

echo "=== Reading version file ==="
VERSION="$(tr -d ' \t\r\n' < "$VERSION_FILE")"

if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Version format invalid: '$VERSION' (expected Major.Minor.Patch e.g. 1.0.0)" >&2
  exit 1
fi

IFS='.' read -r MAJOR MINOR PATCH <<< "$VERSION"
PATCH=$((PATCH + 1))
NEW_VERSION="${MAJOR}.${MINOR}.${PATCH}"

echo "Old version: $VERSION"
echo "New version: $NEW_VERSION"

# A dry run must leave no trace, but should still build the artefact it would publish.
# Stage the new version, then put both files back on exit.
if [[ "$DRY_RUN" == "true" ]]; then
  # Snapshot to a file, not a variable: $(cat ...) strips trailing newlines, which
  # would restore pyproject.toml one byte short of how it started.
  ORIGINAL_PYPROJECT="$(mktemp)"
  cp "$PYPROJECT" "$ORIGINAL_PYPROJECT"
  restore() {
    cp "$ORIGINAL_PYPROJECT" "$PYPROJECT"
    rm -f "$ORIGINAL_PYPROJECT"
    echo "$VERSION" > "$VERSION_FILE"
    echo "(dry run: version file and pyproject.toml restored)"
  }
  trap restore EXIT
fi

echo "$NEW_VERSION" > "$VERSION_FILE"

echo "=== Syncing pyproject.toml to $NEW_VERSION ==="
# Only the [project] version line, anchored so a dependency pin is never touched.
"$PYTHON" - "$PYPROJECT" "$NEW_VERSION" <<'PYEOF'
import re, sys
path, new = sys.argv[1], sys.argv[2]
text = open(path, encoding="utf-8").read()
patched, count = re.subn(r'(?m)^version\s*=\s*"[^"]+"', f'version = "{new}"', text, count=1)
if count != 1:
    sys.exit(f"Expected exactly one top-level version line in {path}, found {count}")
open(path, "w", encoding="utf-8").write(patched)
PYEOF

echo "=== Checking build tooling ==="
if ! "$PYTHON" -c "import build, twine" >/dev/null 2>&1; then
  echo "  installing build + twine"
  if ! "$PYTHON" -m pip install --quiet --upgrade build twine; then
    echo >&2
    echo "Could not install 'build' and 'twine' with $PYTHON." >&2
    echo "Install them yourself, or point PYTHON at an interpreter that has them:" >&2
    echo "  PYTHON=/path/to/python ./$(basename "$0")" >&2
    exit 1
  fi
fi

echo "=== Cleaning old artefacts ==="
rm -rf "$PROJECT_ROOT/dist" "$PROJECT_ROOT/build" "$PROJECT_ROOT"/*.egg-info

echo "=== Building $NEW_VERSION ==="
( cd "$PROJECT_ROOT" && "$PYTHON" -m build )

echo "=== Checking artefacts ==="
"$PYTHON" -m twine check "$PROJECT_ROOT"/dist/*

if [[ "$DRY_RUN" == "true" ]]; then
  echo "Done. Artefacts are in dist/; nothing was uploaded."
  echo "Version used: $NEW_VERSION"
  exit 0
fi

echo "=== Uploading to $REPO_URL ==="
TWINE_USERNAME="__token__" TWINE_PASSWORD="$TOKEN" \
  "$PYTHON" -m twine upload --repository-url "$REPO_URL" "$PROJECT_ROOT"/dist/*

echo "=== Publish complete ==="
echo "Version used: $NEW_VERSION"
