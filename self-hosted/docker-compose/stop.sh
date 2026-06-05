#!/bin/bash
# Stop the self-hosted production stack

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "Stopping production stack..."
docker compose down

echo "Production stack stopped."
