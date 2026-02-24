#!/bin/bash
# Famick Home Management - Docker Setup Script
# This script prepares the environment for running the Docker containers

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "=== Famick Home Management Docker Setup ==="
echo

# Create .env file if it doesn't exist
if [ ! -f .env ]; then
    echo "Creating .env file from .env.example..."
    cp .env.example .env

    # Generate a random JWT secret key
    JWT_SECRET=$(openssl rand -base64 32 | tr -d '/+=' | head -c 48)
    sed -i.bak "s/your-secret-key-change-this-min-32-characters-long/$JWT_SECRET/" .env
    rm -f .env.bak

    # Generate a random DB password
    DB_PASS=$(openssl rand -base64 16 | tr -d '/+=' | head -c 20)
    sed -i.bak "s/changeme123/$DB_PASS/" .env
    rm -f .env.bak

    echo "Generated random secrets in .env file"
else
    echo ".env file already exists, skipping..."
fi

# Create certs directory
mkdir -p certs

# Generate self-signed certificate if it doesn't exist
if [ ! -f certs/aspnetapp.pfx ]; then
    echo "Generating self-signed HTTPS certificate..."

    # Get password from .env or use default
    CERT_PASSWORD=$(grep CERT_PASSWORD .env | cut -d'=' -f2 || echo "password")

    # Generate certificate
    openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
        -keyout certs/aspnetapp.key \
        -out certs/aspnetapp.crt \
        -subj "/C=US/ST=Local/L=Local/O=Famick/CN=localhost" \
        -addext "subjectAltName=DNS:localhost,DNS:*.localhost,IP:127.0.0.1"

    # Convert to PFX
    openssl pkcs12 -export \
        -out certs/aspnetapp.pfx \
        -inkey certs/aspnetapp.key \
        -in certs/aspnetapp.crt \
        -password pass:$CERT_PASSWORD

    # Clean up intermediate files
    rm -f certs/aspnetapp.key certs/aspnetapp.crt

    echo "HTTPS certificate generated successfully"
else
    echo "HTTPS certificate already exists, skipping..."
fi

echo
echo "=== Setup Complete ==="
echo
echo "To start the application:"
echo "  cd $SCRIPT_DIR"
echo "  docker compose up -d"
echo
echo "Services will be available at:"
echo "  - Web App:    http://localhost:5000 or https://localhost:5001"
echo "  - Swagger:    http://localhost:5000/swagger"
echo "  - PostgreSQL: localhost:5432"
echo
echo "To include pgAdmin (database management UI):"
echo "  docker compose --profile tools up -d"
echo "  pgAdmin:      http://localhost:5050"
echo
