#!/bin/bash
# Stop the development database

CONTAINER_NAME="homemanagement-db-dev"

if docker ps --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    echo "Stopping database container '$CONTAINER_NAME'..."
    docker stop "$CONTAINER_NAME"
    echo "Database stopped."
else
    echo "Database container '$CONTAINER_NAME' is not running."
fi
