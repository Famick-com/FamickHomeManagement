#!/bin/bash
set -e

if [ $# -lt 2 ]; then
    echo "Usage: $0 <user@server> <remote-path> [platform]"
    echo "Example: $0 k6shm@homebot.therien.family /home/k6shm"
    echo "Example: $0 k6shm@homebot.therien.family /home/k6shm linux/arm64"
    echo ""
    echo "Platforms: linux/amd64 (default), linux/arm64"
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$PROJECT_ROOT"
git pull origin main

SERVER="$1"
REMOTE_PATH="$2"
PLATFORM="${3:-linux/amd64}"

IMAGE_REPO="mtherienfamick/homemanagement"
SHA="$(git rev-parse --short HEAD)"
TARBALL="homemanagement.tar.gz"

# Ensure the local tarball is removed even if a later step (scp / ssh) fails.
trap 'rm -f "$TARBALL"' EXIT

echo "Building image for $PLATFORM ($IMAGE_REPO:latest + $IMAGE_REPO:$SHA)..."
docker buildx build \
    --platform "$PLATFORM" \
    -f self-hosted/docker-compose/Dockerfile \
    -t "$IMAGE_REPO:latest" \
    -t "$IMAGE_REPO:$SHA" \
    --load .

echo "Saving image..."
docker save "$IMAGE_REPO:latest" "$IMAGE_REPO:$SHA" | gzip > "$TARBALL"

echo "Transferring to server..."
scp "$TARBALL" "$SERVER:$REMOTE_PATH"

echo "Loading image on server..."
ssh "$SERVER" "cd $REMOTE_PATH && gunzip -f $TARBALL && docker load -i homemanagement.tar && rm homemanagement.tar"

echo "Done!"
echo "Deployed: $IMAGE_REPO:latest (also tagged $IMAGE_REPO:$SHA for rollback)"
echo "Next: redeploy the stack in Portainer to pick up the new :latest image."
echo "Rollback: docker tag $IMAGE_REPO:<old-sha> $IMAGE_REPO:latest && redeploy"
