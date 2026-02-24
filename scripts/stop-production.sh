#!/bin/bash
# Stop the full production stack

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCKER_DIR="$SCRIPT_DIR/../docker"

cd "$DOCKER_DIR"

echo "Stopping production stack..."
docker compose down

echo "Production stack stopped."
