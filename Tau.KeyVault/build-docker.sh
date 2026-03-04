#!/usr/bin/env bash
set -euo pipefail

# -----------------------------
# HARD-CODED CONFIG
# -----------------------------
IMAGE_NAME="$(basename "$(pwd)" | tr '[:upper:]' '[:lower:]' | tr ' ' '-' | tr -cd 'a-z0-9._-')"
CONTEXT_DIR="."
DOCKERFILE="Dockerfile"
VERSION_FILE="VERSION"
HARBOR="your-registry-url"
PROJECT="tau"

PUSH=false         # set true if you want auto-push
BUMP=true         # set false if version managed elsewhere
NO_CACHE=false    # set true to force a full rebuild (debug)
PRINT_DIGEST=true # print local + remote digests (debug)

# -----------------------------
# Helpers
# -----------------------------
die() { echo "ERROR: $*" >&2; exit 1; }

validate_version() {
  [[ "$1" =~ ^[0-9]+(\.[0-9]+){3}$ ]] || die "Version must be in format A.B.C.D (got '$1')"
}

read_version() {
  if [[ -f "$VERSION_FILE" ]]; then
    tr -d ' \t\r\n' < "$VERSION_FILE"
  else
    echo "1.0.0.0"
  fi
}

bump_last_segment() {
  local v="$1"
  validate_version "$v"
  IFS='.' read -r a b c d <<< "$v"
  d=$((d + 1))
  echo "${a}.${b}.${c}.${d}"
}

# -----------------------------
# Main
# -----------------------------
current_version="$(read_version)"
validate_version "$current_version"

if [[ "$BUMP" == "true" ]]; then
  new_version="$(bump_last_segment "$current_version")"
  echo "$new_version" > "$VERSION_FILE"
else
  new_version="$current_version"
fi

echo "Building image: ${IMAGE_NAME}:${new_version} and ${IMAGE_NAME}:latest"
echo "Context: $CONTEXT_DIR | Dockerfile: $DOCKERFILE | VERSION_FILE: $VERSION_FILE"

build_args=(
  -f "$DOCKERFILE"
  --build-arg "APP_VERSION=${new_version}"
  -t "${IMAGE_NAME}:${new_version}"
  -t "${IMAGE_NAME}:latest"
)

if [[ "$NO_CACHE" == "true" ]]; then
  build_args+=(--no-cache)
fi

docker build "${build_args[@]}" "$CONTEXT_DIR"

if [[ "$PRINT_DIGEST" == "true" ]]; then
  echo "Local image IDs:"
  docker image inspect "${IMAGE_NAME}:${new_version}" --format "  ${IMAGE_NAME}:${new_version} -> {{.Id}}"
  docker image inspect "${IMAGE_NAME}:latest"         --format "  ${IMAGE_NAME}:latest -> {{.Id}}"
fi

if [[ "$PUSH" == "true" ]]; then
  echo "Tagging for registry..."
  docker tag "${IMAGE_NAME}:${new_version}" "${HARBOR}/${PROJECT}/${IMAGE_NAME}:${new_version}"
  docker tag "${IMAGE_NAME}:latest"         "${HARBOR}/${PROJECT}/${IMAGE_NAME}:latest"

  echo "Pushing..."
  docker push "${HARBOR}/${PROJECT}/${IMAGE_NAME}:${new_version}"
  docker push "${HARBOR}/${PROJECT}/${IMAGE_NAME}:latest"

  if [[ "$PRINT_DIGEST" == "true" ]]; then
    echo "Remote digest:"
    if command -v docker >/dev/null 2>&1 && docker buildx version >/dev/null 2>&1; then
      docker buildx imagetools inspect "${HARBOR}/${PROJECT}/${IMAGE_NAME}:latest" || true
    else
      echo "  (buildx not available, skipping imagetools inspect)"
    fi
  fi
fi

echo "Done."
echo "Version used: $new_version"
