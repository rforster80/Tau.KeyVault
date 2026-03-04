#!/usr/bin/env bash
set -euo pipefail

# Defaults (override via env or flags)
SOURCE_DEFAULT="https://nuget.yoursite.com/v3/index.json"

# Uncomment the line below for a secondary nuget server and scroll to the bottom of this file to perform the publish to the secondary server
#SOURCE_DEFAULT2="https://nuget2.yoursite2.com/v3/index.json"

APIKEY_DEFAULT="your-key"
CONFIG_DEFAULT="Release"

SOURCE="$SOURCE_DEFAULT"
SOURCE2="$SOURCE_DEFAULT2"
API_KEY="$APIKEY_DEFAULT"
CONFIGURATION="$CONFIG_DEFAULT"

usage() {
  cat <<EOF
Usage: $(basename "$0") [-s <source>] [-k <apiKey>] [-c <configuration>]

Defaults:
  -s  $SOURCE_DEFAULT
  -k  $APIKEY_DEFAULT
  -c  $CONFIG_DEFAULT

Example:
  ./publish-nuget.sh -s "https://nuget2.yoursite2.com/v3/index.json" -k "your-key"
  ./publish-nuget.sh -s "https://nuget.yoursite.com/v3/index.json" -k "your-key"
EOF
}

while getopts ":s:k:c:h" opt; do
  case "$opt" in
    s) SOURCE="$OPTARG" ;;
    k) API_KEY="$OPTARG" ;;
    c) CONFIGURATION="$OPTARG" ;;
    h) usage; exit 0 ;;
    \?) echo "Invalid option: -$OPTARG" >&2; usage; exit 1 ;;
    :)  echo "Option -$OPTARG requires an argument." >&2; usage; exit 1 ;;
  esac
done

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VERSION_FILE="$PROJECT_ROOT/nuget_version.txt"
RELEASE_DIR="$PROJECT_ROOT/bin/$CONFIGURATION"

if [[ ! -f "$VERSION_FILE" ]]; then
  echo "version.txt not found in project root: $PROJECT_ROOT" >&2
  exit 1
fi

echo "=== Reading version file ==="
VERSION="$(tr -d ' \t\r\n' < "$VERSION_FILE")"

if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Version format invalid: '$VERSION' (expected Major.Minor.Patch e.g. 1.0.0)" >&2
  exit 1
fi

IFS='.' read -r MAJOR MINOR PATCH <<< "$VERSION"

# Increment patch
PATCH=$((PATCH + 1))
NEW_VERSION="${MAJOR}.${MINOR}.${PATCH}"

echo "Old version: $VERSION"
echo "New version: $NEW_VERSION"

# Write new version back to file
echo "$NEW_VERSION" > "$VERSION_FILE"

echo "=== Cleaning old packages ==="
if [[ -d "$RELEASE_DIR" ]]; then
  rm -f "$RELEASE_DIR"/*.nupkg || true
else
  echo "Release dir not found yet: $RELEASE_DIR"
fi

echo "=== Packing version $NEW_VERSION ==="
dotnet pack -c "$CONFIGURATION" -p:PackageVersion="$NEW_VERSION"

echo "=== Locating newest .nupkg ==="
if [[ ! -d "$RELEASE_DIR" ]]; then
  echo "Release dir not found after pack: $RELEASE_DIR" >&2
  exit 1
fi

# Newest by modified time (portable enough for Linux/macOS with ls -t)
NUPKG_PATH="$(ls -t "$RELEASE_DIR"/*.nupkg 2>/dev/null | head -n 1 || true)"
if [[ -z "$NUPKG_PATH" ]]; then
  echo "No .nupkg found in $RELEASE_DIR after packing." >&2
  exit 1
fi

echo "Publishing: $(basename "$NUPKG_PATH")"
dotnet nuget push "$NUPKG_PATH" --source "$SOURCE" --api-key "$API_KEY"

# NB
# Uncomment here if you are using a secondary server
#dotnet nuget push "$NUPKG_PATH" --source "$SOURCE2" --api-key "$API_KEY"

echo "=== Publish complete ==="
