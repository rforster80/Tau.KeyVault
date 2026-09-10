#!/usr/bin/env bash
set -euo pipefail

# Copy this to publish-npm.sh (gitignored) and fill in your registry and token.

# -----------------------------
# HARD-CODED CONFIG
# -----------------------------
REGISTRY_DEFAULT="https://npm.yoursite.com"

# Auth token. Prefer exporting NPM_TOKEN instead of committing one here.
TOKEN_DEFAULT="${NPM_TOKEN:-your-npm-token}"

DRY_RUN=false      # set true to pack and inspect without publishing
ACCESS="restricted" # "public" for a public scoped package

REGISTRY="$REGISTRY_DEFAULT"
TOKEN="$TOKEN_DEFAULT"

usage() {
  cat <<USAGE
Usage: $(basename "$0") [-r <registry>] [-t <token>] [-a <access>] [-n]

Defaults:
  -r  $REGISTRY_DEFAULT
  -t  \$NPM_TOKEN, else the placeholder in this file
  -a  $ACCESS          (public | restricted)
  -n  dry run — npm pack only, no publish

Example:
  NPM_TOKEN=xxxx ./publish-npm.sh -r "https://registry.npmjs.org"
USAGE
}

while getopts ":r:t:a:nh" opt; do
  case "$opt" in
    r) REGISTRY="$OPTARG" ;;
    t) TOKEN="$OPTARG" ;;
    a) ACCESS="$OPTARG" ;;
    n) DRY_RUN=true ;;
    h) usage; exit 0 ;;
    \?) echo "Invalid option: -$OPTARG" >&2; usage; exit 1 ;;
    :)  echo "Option -$OPTARG requires an argument." >&2; usage; exit 1 ;;
  esac
done

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VERSION_FILE="$PROJECT_ROOT/npm_version.txt"
PKG_JSON="$PROJECT_ROOT/package.json"

command -v npm >/dev/null 2>&1 || { echo "npm not found on PATH" >&2; exit 1; }

if [[ ! -f "$VERSION_FILE" ]]; then
  echo "npm_version.txt not found in project root: $PROJECT_ROOT" >&2
  echo "Copy sample_npm_version.txt to npm_version.txt to start." >&2
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

# A dry run must leave no trace, but should still pack the tarball it would publish.
# Stage the new version, then put both files back on exit.
if [[ "$DRY_RUN" == "true" ]]; then
  # Snapshot to a file, not a variable: $(cat ...) strips trailing newlines, which
  # would restore package.json one byte short of how it started.
  ORIGINAL_PKG_JSON="$(mktemp)"
  cp "$PKG_JSON" "$ORIGINAL_PKG_JSON"
  restore() {
    cp "$ORIGINAL_PKG_JSON" "$PKG_JSON"
    rm -f "$ORIGINAL_PKG_JSON"
    echo "$VERSION" > "$VERSION_FILE"
    echo "(dry run: version file and package.json restored)"
  }
  trap restore EXIT
fi

echo "$NEW_VERSION" > "$VERSION_FILE"

echo "=== Syncing package.json to $NEW_VERSION ==="
# --no-git-tag-version: this repo tags releases separately, if at all.
( cd "$PROJECT_ROOT" && npm version "$NEW_VERSION" --no-git-tag-version --allow-same-version >/dev/null )

echo "=== Cleaning old tarballs ==="
rm -f "$PROJECT_ROOT"/*.tgz || true

if [[ "$DRY_RUN" == "true" ]]; then
  echo "=== Dry run: packing only ==="
  ( cd "$PROJECT_ROOT" && npm pack )
  echo "Done. Inspect the .tgz above; nothing was published."
  echo "Version used: $NEW_VERSION"
  exit 0
fi

# npm reads auth from .npmrc. Write a throwaway one so the token never has to live
# in a committed file, and always remove it — even if publish fails.
NPMRC="$PROJECT_ROOT/.npmrc"
REGISTRY_HOST="${REGISTRY#*://}"
cleanup() { rm -f "$NPMRC"; }
trap cleanup EXIT

cat > "$NPMRC" <<NPMRC_EOF
registry=$REGISTRY
//$REGISTRY_HOST/:_authToken=$TOKEN
NPMRC_EOF
chmod 600 "$NPMRC"

echo "=== Publishing $NEW_VERSION to $REGISTRY ==="
( cd "$PROJECT_ROOT" && npm publish --registry "$REGISTRY" --access "$ACCESS" )

echo "=== Publish complete ==="
echo "Version used: $NEW_VERSION"
