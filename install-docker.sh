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
# Non-interactive (skips the directory prompt):
#   curl -fsSL .../install-docker.sh | FAMICK_HOME=/opt/famick bash
#
# Branch override (advanced — pulls files from a non-main branch):
#   curl -fsSL .../install-docker.sh | FAMICK_BRANCH=phase-5 bash

set -e

DEFAULT_DIR="$HOME/famick-home-management"
INSTALL_DIR="${FAMICK_HOME:-}"
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
download "config/server-config.example.json"
download "plugins/README.md"
download "plugins/config.example.json"

# Run setup — generates .env (with random secrets), keys/jwt-rsa.pem, certs/aspnetapp.pfx
# Idempotent: each step skips itself if its output already exists.
log "Running setup..."
./setup.sh

cat <<EOF

Setup complete. Next steps:

  cd $INSTALL_DIR
  docker compose up -d

Then open:
  http://localhost:88
  https://localhost:4431

To stop later:
  cd $INSTALL_DIR
  docker compose down

To refresh a file from upstream, delete it and re-run the installer:
  rm $INSTALL_DIR/docker-compose.yml
  curl -fsSL https://raw.githubusercontent.com/Famick-com/FamickHomeManagement/main/install-docker.sh | bash

EOF
