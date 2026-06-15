#!/usr/bin/env bash
# Famick Home Management — Home Assistant add-on first-boot bootstrap.
#
# Runs inside the add-on container before the Famick service starts,
# typically as /etc/cont-init.d/02-famick-bootstrap under s6-overlay (a
# Postgres initdb step is expected to run earlier as 01-postgres in the
# wrapper repo). Idempotent — every step must be safe to re-run on every
# add-on start.
#
# Responsibilities:
#   - Ensure the persistent /data subtree exists
#   - Generate the RSA JWT signing key if missing
#   - Generate a per-install tenant UUID if missing
#   - Seed /data/config/server-config.json if missing
#   - Seed /data/plugins/config.json from the bundled example if missing
#
# Out of scope (handled elsewhere):
#   - Postgres initdb / data-dir permissions — wrapper repo, base-image-specific
#   - TLS cert generation — HA Supervisor terminates TLS at the ingress edge
#   - Env-var preparation — Supervisor injects options.json values directly

set -euo pipefail

DATA="${FAMICK_DATA_DIR:-/data}"
PLUGINS_EXAMPLE="${FAMICK_PLUGINS_EXAMPLE:-/app/plugins/config.example.json}"

mkdir -p \
    "$DATA/keys" \
    "$DATA/config" \
    "$DATA/plugins" \
    "$DATA/uploads" \
    "$DATA/dataprotection"

if [ ! -f "$DATA/keys/jwt-rsa.pem" ]; then
    echo "[famick-bootstrap] generating RSA JWT signing key"
    openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 \
        -out "$DATA/keys/jwt-rsa.pem" 2>/dev/null
    chmod 600 "$DATA/keys/jwt-rsa.pem"
fi

if [ ! -f "$DATA/config/tenant-id" ]; then
    # Per-install random UUID — same value feeds FixedTenantId and
    # SelfHosted__TenantId in the app env so per-request query scope and
    # auth-service writes agree on identity. The wrapper's run script
    # reads this file and exports both env vars before launching the
    # Famick service.
    hex=$(openssl rand -hex 16)
    tenant="${hex:0:8}-${hex:8:4}-${hex:12:4}-${hex:16:4}-${hex:20:12}"
    printf '%s\n' "$tenant" > "$DATA/config/tenant-id"
    echo "[famick-bootstrap] generated tenant UUID $tenant"
fi

if [ ! -f "$DATA/config/server-config.json" ]; then
    cat > "$DATA/config/server-config.json" <<'JSON'
{
  "ServerName": "Famick (Home Assistant)"
}
JSON
    echo "[famick-bootstrap] seeded server-config.json"
fi

if [ ! -f "$DATA/plugins/config.json" ] && [ -f "$PLUGINS_EXAMPLE" ]; then
    cp "$PLUGINS_EXAMPLE" "$DATA/plugins/config.json"
    echo "[famick-bootstrap] seeded plugin config from example"
fi

echo "[famick-bootstrap] complete"
