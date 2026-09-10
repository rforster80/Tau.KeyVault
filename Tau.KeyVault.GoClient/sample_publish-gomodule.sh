#!/usr/bin/env bash
set -euo pipefail

# Copy this to publish-gomodule.sh (gitignored) if you need to change the defaults.
#
# Go has no registry upload: a module is "published" by pushing a semver git tag that
# the module proxy can resolve. This script therefore validates, bumps, tags and
# (optionally) pushes — it never uploads an artefact.

# -----------------------------
# HARD-CODED CONFIG
# -----------------------------
PUSH=false          # tag locally by default; pass -p to push the tag
WARM_PROXY=false    # pass -w to ask proxy.golang.org to fetch the new version
REMOTE="origin"

usage() {
  cat <<USAGE
Usage: $(basename "$0") [-p] [-w] [-r <remote>] [-n]

  -p  push the tag to <remote> (default: tag locally only)
  -w  warm the public module proxy after pushing
  -r  git remote to push to (default: $REMOTE)
  -n  dry run — validate and show the tag, change nothing

Publishing a Go module is just a tag, so this script is deliberately
conservative: without -p nothing leaves your machine.
USAGE
}

DRY_RUN=false
while getopts ":pwr:nh" opt; do
  case "$opt" in
    p) PUSH=true ;;
    w) WARM_PROXY=true ;;
    r) REMOTE="$OPTARG" ;;
    n) DRY_RUN=true ;;
    h) usage; exit 0 ;;
    \?) echo "Invalid option: -$OPTARG" >&2; usage; exit 1 ;;
    :)  echo "Option -$OPTARG requires an argument." >&2; usage; exit 1 ;;
  esac
done

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VERSION_FILE="$PROJECT_ROOT/go_version.txt"
REPO_ROOT="$(git -C "$PROJECT_ROOT" rev-parse --show-toplevel)"

command -v go >/dev/null 2>&1 || { echo "go not found on PATH" >&2; exit 1; }

if [[ ! -f "$VERSION_FILE" ]]; then
  echo "go_version.txt not found in project root: $PROJECT_ROOT" >&2
  echo "Copy sample_go_version.txt to go_version.txt to start." >&2
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

# -----------------------------
# Validate before tagging — a pushed tag is immutable in the proxy.
# -----------------------------
echo "=== gofmt ==="
UNFORMATTED="$(gofmt -l "$PROJECT_ROOT")"
if [[ -n "$UNFORMATTED" ]]; then
  echo "These files are not gofmt-clean:" >&2
  echo "$UNFORMATTED" >&2
  exit 1
fi

echo "=== go vet ==="
( cd "$PROJECT_ROOT" && go vet ./... )

echo "=== go build ==="
( cd "$PROJECT_ROOT" && go build ./... )

echo "=== go test ==="
( cd "$PROJECT_ROOT" && go test ./... )

# -----------------------------
# Work out the tag. Go derives it from where the module sits in the repo:
# a module in a subdirectory is tagged "<subdir>/vX.Y.Z", not "vX.Y.Z".
# -----------------------------
MODULE_PATH="$(cd "$PROJECT_ROOT" && go list -m)"
SUBDIR="${PROJECT_ROOT#"$REPO_ROOT"/}"

if [[ "$SUBDIR" == "$PROJECT_ROOT" ]]; then
  TAG="v${NEW_VERSION}"                 # module is at the repo root
else
  TAG="${SUBDIR}/v${NEW_VERSION}"       # module is in a subdirectory
fi

echo "=== Module ==="
echo "  module path : $MODULE_PATH"
echo "  repo subdir : ${SUBDIR:-<root>}"
echo "  tag         : $TAG"

# The module path has to match where the code actually lives, or `go get` cannot
# resolve it. Warn rather than rewrite go.mod: changing the module path breaks
# every existing consumer, so it is a deliberate decision, not a script's to make.
REPO_URL="$(git -C "$REPO_ROOT" remote get-url "$REMOTE" 2>/dev/null || echo "")"
if [[ -n "$REPO_URL" ]]; then
  EXPECTED_HOSTPATH="$(sed -E 's#^git@([^:]+):#\1/#; s#^https?://#|#; s#^\|##; s#\.git$##' <<< "$REPO_URL")"
  EXPECTED_MODULE="${EXPECTED_HOSTPATH}${SUBDIR:+/$SUBDIR}"
  if [[ "$MODULE_PATH" != "$EXPECTED_MODULE" ]]; then
    echo
    echo "WARNING: module path does not match this repository."
    echo "  go.mod says : $MODULE_PATH"
    echo "  resolves as : $EXPECTED_MODULE"
    echo "  Until these agree, 'go get $MODULE_PATH' will not resolve from $REMOTE."
    echo "  Either publish this client from its own repository, or change the module"
    echo "  path in go.mod (a breaking change for existing consumers)."
    echo
  fi
fi

if [[ "$DRY_RUN" == "true" ]]; then
  echo "Dry run: validated only. Would tag '$TAG'. Version file unchanged."
  exit 0
fi

echo "$NEW_VERSION" > "$VERSION_FILE"

if git -C "$REPO_ROOT" rev-parse -q --verify "refs/tags/$TAG" >/dev/null; then
  echo "Tag '$TAG' already exists — bump the version file or delete the tag." >&2
  exit 1
fi

echo "=== Tagging $TAG ==="
git -C "$REPO_ROOT" tag -a "$TAG" -m "Tau Key Vault Go client $NEW_VERSION"

if [[ "$PUSH" != "true" ]]; then
  echo
  echo "Tag created locally. Nothing was pushed."
  echo "  push it with: git push $REMOTE $TAG"
  echo "Version used: $NEW_VERSION"
  exit 0
fi

echo "=== Pushing $TAG to $REMOTE ==="
git -C "$REPO_ROOT" push "$REMOTE" "$TAG"

if [[ "$WARM_PROXY" == "true" ]]; then
  echo "=== Warming module proxy ==="
  GOPROXY=proxy.golang.org GOFLAGS=-mod=mod \
    go list -m "${MODULE_PATH}@v${NEW_VERSION}" || \
    echo "  (proxy fetch failed — normal for a private repository)"
fi

echo "=== Publish complete ==="
echo "Version used: $NEW_VERSION"
