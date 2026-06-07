#!/bin/bash
# Famick Home Management — one-line installer for the docker-compose strategy.
#
# Downloads only the files needed to run the self-hosted stack — no git clone
# of the repo, no source code on disk. The image itself is pulled by `docker
# compose up`. A sibling install-<strategy>.sh script will land later for
# proxmox / kubernetes-helm / home-assistant-plugin.
#
# Usage:
#   curl -fsSL https://raw.githubusercontent.com/Famick-com/FamickHomeManagement/main/install-docker.sh | bash
#
# Non-interactive (skips both prompts):
#   curl -fsSL .../install-docker.sh | FAMICK_HOME=/opt/famick FAMICK_TLS_MODE=proxy bash
#
# FAMICK_TLS_MODE accepts "proxy" (reverse proxy in front handles TLS — the
# default) or "app" (Kestrel terminates TLS itself; setup.sh generates a
# self-signed cert and a docker-compose.app-tls.yml overlay is wired up).
#
# Branch override (advanced — pulls files from a non-main branch):
#   curl -fsSL .../install-docker.sh | FAMICK_BRANCH=phase-5 bash

set -e

DEFAULT_DIR="$HOME/famick-home-management"
INSTALL_DIR="${FAMICK_HOME:-}"
TLS_MODE="${FAMICK_TLS_MODE:-}"
BRANCH="${FAMICK_BRANCH:-main}"
RAW_BASE="https://raw.githubusercontent.com/Famick-com/FamickHomeManagement/$BRANCH/self-hosted/docker-compose"

log()  { printf '\033[0;32m[INFO]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[WARN]\033[0m %s\n' "$*"; }
fail() { printf '\033[0;31m[ERROR]\033[0m %s\n' "$*" >&2; exit 1; }

# Preflight: docker, docker compose v2, curl, openssl (last one needed by setup.sh)
for cmd in docker curl openssl; do
    command -v "$cmd" >/dev/null 2>&1 || fail "Required command not found: $cmd"
done
docker compose version >/dev/null 2>&1 || fail "docker compose (v2) is required (try: docker compose version)"

# Pick install directory. /dev/tty is required because stdin is the script
# itself when piped from curl.
if [ -z "$INSTALL_DIR" ]; then
    if [ -r /dev/tty ]; then
        printf 'Install directory [%s]: ' "$DEFAULT_DIR" > /dev/tty
        read -r input < /dev/tty || input=""
        INSTALL_DIR="${input:-$DEFAULT_DIR}"
    else
        warn "No TTY available — using default"
        INSTALL_DIR="$DEFAULT_DIR"
    fi
fi
INSTALL_DIR="${INSTALL_DIR/#\~/$HOME}"
log "Installing to: $INSTALL_DIR"

# TLS mode: reverse proxy in front (default) or app-level TLS (Kestrel).
if [ -z "$TLS_MODE" ]; then
    if [ -r /dev/tty ]; then
        cat > /dev/tty <<'EOF'

How will TLS be terminated?
  1) Reverse proxy in front of this stack (Caddy, Traefik, Tailscale Serve, etc.) — recommended
  2) Kestrel inside this stack (a self-signed cert will be generated)

EOF
        printf 'Choice [1]: ' > /dev/tty
        read -r tls_choice < /dev/tty || tls_choice=""
        case "$tls_choice" in
            2|app|APP) TLS_MODE="app" ;;
            *)         TLS_MODE="proxy" ;;
        esac
    else
        warn "No TTY available — defaulting to reverse-proxy TLS mode"
        TLS_MODE="proxy"
    fi
fi
log "TLS mode: $TLS_MODE"

mkdir -p "$INSTALL_DIR"
cd "$INSTALL_DIR"

# download <remote-rel-path> [<local-rel-path>] [+x]
# Skips files that already exist so re-running the installer doesn't overwrite
# customizations. Delete a file first if you want to refresh it from upstream.
download() {
    local src="$1"
    local dst="${2:-$src}"
    local exec_flag="$3"

    if [ -f "$dst" ]; then
        log "Already present, skipping: $dst"
        return
    fi

    mkdir -p "$(dirname "$dst")"
    if ! curl -fsSL "$RAW_BASE/$src" -o "$dst"; then
        fail "Failed to download $src"
    fi
    [ "$exec_flag" = "+x" ] && chmod +x "$dst"
    log "Downloaded: $dst"
}

log "Downloading files from branch '$BRANCH'..."
download "docker-compose.yml"
download "setup.sh"     ""  +x
download "start.sh"     ""  +x
download "stop.sh"      ""  +x
download ".env.example"
download "init-db.sql"
download "data/config/server-config.example.json"
download "data/plugins/README.md"
download "data/plugins/config.example.json"

if [ "$TLS_MODE" = "app" ]; then
    download "docker-compose.app-tls.yml"
    # docker compose reads COMPOSE_FILE from .env automatically. Pin both files
    # so plain `docker compose up -d` picks the overlay without extra flags.
    # Append only if .env doesn't already have a COMPOSE_FILE line.
    if [ -f .env ] && ! grep -q "^COMPOSE_FILE=" .env; then
        echo "COMPOSE_FILE=docker-compose.yml:docker-compose.app-tls.yml" >> .env
        log "Wrote COMPOSE_FILE to .env so the app-tls overlay applies automatically"
    fi
fi

# Run setup — generates .env (with random secrets), data/keys/jwt-rsa.pem, and
# (when TLS_MODE=app) data/certs/aspnetapp.pfx. Idempotent: each step skips
# itself if its output already exists.
log "Running setup..."
FAMICK_TLS_MODE="$TLS_MODE" ./setup.sh

cat <<EOF

Setup complete. Next steps:

  cd $INSTALL_DIR
  docker compose up -d

Then open:
  http://localhost:8088
EOF
if [ "$TLS_MODE" = "app" ]; then
    cat <<EOF
  https://localhost:4431

(Defaults; HTTP_PORT and HTTPS_PORT in .env override.)
EOF
else
    cat <<EOF

(Default HTTP_PORT 8088 in .env. Front this with your reverse proxy for HTTPS.)
EOF
fi
cat <<EOF

To stop later:
  cd $INSTALL_DIR
  docker compose down

To refresh a file from upstream, delete it and re-run the installer:
  rm $INSTALL_DIR/docker-compose.yml
  curl -fsSL https://raw.githubusercontent.com/Famick-com/FamickHomeManagement/main/install-docker.sh | bash

EOF
