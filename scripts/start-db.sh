#!/bin/bash
# Start the development database if not already running

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCKER_DIR="$SCRIPT_DIR/../docker"
CONTAINER_NAME="homemanagement-db-dev"

# Check if container is already running
if docker ps --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    echo "Database container '$CONTAINER_NAME' is already running."
    exit 0
fi

# Check if container exists but is stopped
if docker ps -a --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    echo "Starting existing database container '$CONTAINER_NAME'..."
    docker start "$CONTAINER_NAME"
else
    echo "Creating and starting database container..."
    cd "$DOCKER_DIR"
    docker compose -f docker-compose.dev.yml up -d
fi

# Wait for database to be healthy
echo "Waiting for database to be ready..."
MAX_ATTEMPTS=30
ATTEMPT=0

while [ $ATTEMPT -lt $MAX_ATTEMPTS ]; do
    if docker exec "$CONTAINER_NAME" pg_isready -U homemanagement > /dev/null 2>&1; then
        echo "Database is ready!"
        exit 0
    fi
    ATTEMPT=$((ATTEMPT + 1))
    echo "Waiting for database... (attempt $ATTEMPT/$MAX_ATTEMPTS)"
    sleep 1
done

echo "Error: Database did not become ready in time."
exit 1
