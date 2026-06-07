#!/bin/bash
# Famick Home Management - Docker Setup Script
# Prepares the install directory: generates .env, RSA JWT key, and (if
# app-level TLS is on) a self-signed HTTPS cert. All operator-mutable data
# lives under ./data so docker-compose can bind-mount one folder.
#
# FAMICK_TLS_MODE env var controls cert generation:
#   FAMICK_TLS_MODE=proxy  → skip cert (reverse proxy in front handles TLS)
#   FAMICK_TLS_MODE=app    → generate cert (Kestrel terminates TLS itself)
# install-docker.sh sets this before invoking. Defaults to "app" when run
# standalone so an operator who runs ./setup.sh by hand still gets a cert.

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

TLS_MODE="${FAMICK_TLS_MODE:-app}"

echo "=== Famick Home Management Docker Setup ==="
echo

# Create the data/ subtree the docker mount will populate. App code creates
# files lazily, but pre-creating the dirs avoids docker creating them as root.
mkdir -p data/keys data/certs data/config data/plugins data/uploads data/dataprotection

# Create .env file if it doesn't exist
if [ ! -f .env ]; then
    echo "Creating .env file from .env.example..."
    cp .env.example .env

    # Generate a random JWT secret key (legacy, kept for backwards compatibility)
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

# Generate RSA private key for JWT signing if it doesn't exist
if [ ! -f data/keys/jwt-rsa.pem ]; then
    echo "Generating RSA private key for JWT signing..."
    openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out data/keys/jwt-rsa.pem 2>/dev/null
    chmod 600 data/keys/jwt-rsa.pem
    echo "RSA key generated at data/keys/jwt-rsa.pem"
else
    echo "RSA key already exists, skipping..."
fi

# Generate self-signed HTTPS certificate only if app-level TLS is enabled
if [ "$TLS_MODE" = "app" ]; then
    if [ ! -f data/certs/aspnetapp.pfx ]; then
        echo "Generating self-signed HTTPS certificate..."

        # Get password from .env or use default
        CERT_PASSWORD=$(grep CERT_PASSWORD .env | cut -d'=' -f2 || echo "password")

        # Generate certificate
        openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
            -keyout data/certs/aspnetapp.key \
            -out data/certs/aspnetapp.crt \
            -subj "/C=US/ST=Local/L=Local/O=Famick/CN=localhost" \
            -addext "subjectAltName=DNS:localhost,DNS:*.localhost,IP:127.0.0.1"

        # Convert to PFX
        openssl pkcs12 -export \
            -out data/certs/aspnetapp.pfx \
            -inkey data/certs/aspnetapp.key \
            -in data/certs/aspnetapp.crt \
            -password pass:$CERT_PASSWORD

        # Clean up intermediate files
        rm -f data/certs/aspnetapp.key data/certs/aspnetapp.crt

        echo "HTTPS certificate generated at data/certs/aspnetapp.pfx"
    else
        echo "HTTPS certificate already exists, skipping..."
    fi
else
    echo "TLS mode: proxy — skipping HTTPS cert generation (reverse proxy in front handles TLS)"
fi

echo
echo "=== Setup Complete ==="
echo
echo "To start the application:"
echo "  cd $SCRIPT_DIR"
echo "  docker compose up -d"
echo
HTTP_PORT_DISPLAY="${HTTP_PORT:-8088}"
HTTPS_PORT_DISPLAY="${HTTPS_PORT:-4431}"
echo "Services will be available at:"
echo "  - Web App:    http://localhost:$HTTP_PORT_DISPLAY"
if [ "$TLS_MODE" = "app" ]; then
    echo "  - Web App:    https://localhost:$HTTPS_PORT_DISPLAY (app-level TLS)"
fi
echo "  - Swagger:    http://localhost:$HTTP_PORT_DISPLAY/swagger"
echo "  - PostgreSQL: localhost:5432"
echo
echo "To include pgAdmin (database management UI):"
echo "  docker compose --profile tools up -d"
echo "  pgAdmin:      http://localhost:5050"
echo
