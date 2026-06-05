#!/bin/bash
# Famick Home Management — one-line installer.
#
# Usage:
#   curl -fsSL https://raw.githubusercontent.com/Famick-com/FamickHomeManagement/main/install.sh | bash
#
# Non-interactive (skips the directory prompt):
#   curl -fsSL .../install.sh | FAMICK_HOME=/opt/famick bash
#
# What it does:
#   1. Clones (or updates) the public repo into a directory you pick.
#   2. Runs self-hosted/docker-compose/setup.sh to generate .env, RSA key, HTTPS cert.
#   3. Prints the docker compose commands to start the stack — does NOT auto-start.

set -e

DEFAULT_DIR="$HOME/famick-home-management"
INSTALL_DIR="${FAMICK_HOME:-}"
REPO="https://github.com/Famick-com/FamickHomeManagement.git"

log()  { printf '\033[0;32m[INFO]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[WARN]\033[0m %s\n' "$*"; }
fail() { printf '\033[0;31m[ERROR]\033[0m %s\n' "$*" >&2; exit 1; }

# Preflight: required commands
for cmd in docker git openssl; do
    command -v "$cmd" >/dev/null 2>&1 || fail "Required command not found: $cmd"
done
docker compose version >/dev/null 2>&1 || fail "docker compose (v2) is required (try: docker compose version)"

# Pick install directory. When piped from curl, stdin is the script — read from /dev/tty
# for the interactive prompt so the heredoc-reads-input pattern still works.
if [ -z "$INSTALL_DIR" ]; then
    if [ -r /dev/tty ]; then
        printf 'Install directory [%s]: ' "$DEFAULT_DIR" > /dev/tty
        read -r input < /dev/tty || input=""
        INSTALL_DIR="${input:-$DEFAULT_DIR}"
    else
        warn "No TTY available — using default directory"
        INSTALL_DIR="$DEFAULT_DIR"
    fi
fi
# Expand a leading ~ if the user typed one
INSTALL_DIR="${INSTALL_DIR/#\~/$HOME}"
log "Installing to: $INSTALL_DIR"

# Fetch / update
if [ -d "$INSTALL_DIR/.git" ]; then
    log "Repository already exists — pulling latest..."
    git -C "$INSTALL_DIR" pull --ff-only
elif [ -e "$INSTALL_DIR" ]; then
    fail "$INSTALL_DIR exists but isn't a git checkout. Remove it or pick another path."
else
    log "Cloning $REPO..."
    git clone --depth 1 "$REPO" "$INSTALL_DIR"
fi

# Run setup (generates .env, RSA key, HTTPS cert)
log "Running setup..."
cd "$INSTALL_DIR/self-hosted/docker-compose"
./setup.sh

cat <<EOF

Setup complete. Next steps:

  cd $INSTALL_DIR/self-hosted/docker-compose
  docker compose up -d

Then open:
  http://localhost:88
  https://localhost:4431

To stop later:
  cd $INSTALL_DIR/self-hosted/docker-compose
  docker compose down

EOF
