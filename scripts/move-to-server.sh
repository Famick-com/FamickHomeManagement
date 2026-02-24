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

echo "Building image for $PLATFORM..."
docker buildx build --platform "$PLATFORM" -t famick/homemanagement:latest --load .

echo "Saving image..."
docker save famick/homemanagement:latest | gzip > homemanagement.tar.gz

echo "Transferring to server..."
scp homemanagement.tar.gz "$SERVER:$REMOTE_PATH"

echo "Cleaning up local file..."
rm homemanagement.tar.gz

echo "Loading image on server..."
ssh "$SERVER" "cd $REMOTE_PATH && gunzip -f homemanagement.tar.gz && docker load -i homemanagement.tar && rm homemanagement.tar"

echo "Done!"
