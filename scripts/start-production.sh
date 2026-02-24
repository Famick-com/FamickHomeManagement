#!/bin/bash
# Start the full production stack (database + web server)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCKER_DIR="$SCRIPT_DIR/../docker"
DB_CONTAINER="homemanagement-db"
WEB_CONTAINER="homemanagement-web"

cd "$DOCKER_DIR"

# Run setup if .env doesn't exist
if [ ! -f .env ]; then
    echo "Running initial setup..."
    ./setup.sh
fi

# Check if containers are already running
DB_RUNNING=$(docker ps --format '{{.Names}}' | grep -c "^${DB_CONTAINER}$" || true)
WEB_RUNNING=$(docker ps --format '{{.Names}}' | grep -c "^${WEB_CONTAINER}$" || true)

if [ "$DB_RUNNING" -eq 1 ] && [ "$WEB_RUNNING" -eq 1 ]; then
    echo "Production stack is already running."
    echo "  - Database: http://localhost:5432"
    echo "  - Web App:  http://localhost:5000 / https://localhost:5001"
    exit 0
fi

echo "Starting production stack..."
docker compose up -d --build

# Wait for services to be healthy
echo "Waiting for services to be ready..."
MAX_ATTEMPTS=60
ATTEMPT=0

while [ $ATTEMPT -lt $MAX_ATTEMPTS ]; do
    DB_HEALTHY=$(docker inspect --format='{{.State.Health.Status}}' "$DB_CONTAINER" 2>/dev/null || echo "not_found")

    if [ "$DB_HEALTHY" = "healthy" ]; then
        # Check if web container is running
        if docker ps --format '{{.Names}}' | grep -q "^${WEB_CONTAINER}$"; then
            echo ""
            echo "Production stack is ready!"
            echo "  - Database: localhost:5432"
            echo "  - Web App:  http://localhost:5000"
            echo "  - Web App:  https://localhost:5001"
            echo "  - Swagger:  http://localhost:5000/swagger"
            exit 0
        fi
    fi

    ATTEMPT=$((ATTEMPT + 1))
    echo "Waiting for services... (attempt $ATTEMPT/$MAX_ATTEMPTS)"
    sleep 2
done

echo "Error: Services did not become ready in time."
docker compose logs --tail=50
exit 1
