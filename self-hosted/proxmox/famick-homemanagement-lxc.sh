#!/usr/bin/env bash

# Copyright (c) 2024-2026 Famick Services LLC
# License: Elastic License 2.0
# https://github.com/Famick-com/FamickHomeManagement
#
# Proxmox VE LXC installer for Famick Home Management
#
# Usage (run on the Proxmox VE host shell):
#   bash -c "$(wget -qLO - https://raw.githubusercontent.com/Famick-com/FamickHomeManagement/main/scripts/proxmox/famick-homemanagement-lxc.sh)"

set -Eeuo pipefail

# ── Colors & Formatting ─────────────────────────────────────────────

YW=$(echo "\033[33m")
BL=$(echo "\033[36m")
GN=$(echo "\033[1;92m")
RD=$(echo "\033[01;31m")
CL=$(echo "\033[m")
BFR="\\r\\033[K"
HOLD=" "
CM="${GN}✓${CL}"
CROSS="${RD}✗${CL}"

# ── Utility Functions ────────────────────────────────────────────────

msg_info() { printf " ${HOLD} ${YW}%s...${CL}" "$1"; }
msg_ok()   { printf "${BFR} ${CM} ${GN}%s${CL}\n" "$1"; }
msg_error(){ printf "${BFR} ${CROSS} ${RD}%s${CL}\n" "$1"; }

header() {
  clear
  cat <<'EOF'

   ___           _    _      _  _
  | __| _ _ _ _ (_)__| |__  | || |___ _ __  ___
  | _| / _` | ' \| / _| / / | __ / _ \ '  \/ -_)
  |_|  \__,_|_|_|_\__|_\_\ |_||_\___/_|_|_\___|
   __  __                                        _
  |  \/  |__ _ _ _  __ _ __ _ ___ _ __  ___ _ _ | |_
  | |\/| / _` | ' \/ _` / _` / -_) '  \/ -_) ' \|  _|
  |_|  |_\__,_|_||_\__,_\__, \___|_|_|_\___|_||_|\__|
                         |___/
  Proxmox VE LXC Installer

EOF
}

# ── Cleanup Trap ─────────────────────────────────────────────────────

CONTAINER_CREATED=false
CT_ID=""

cleanup() {
  local exit_code=$?
  if [[ $exit_code -ne 0 && "$CONTAINER_CREATED" == "true" && -n "$CT_ID" ]]; then
    echo
    msg_error "Installation failed (exit code $exit_code)"
    read -rp "  Remove partially created container $CT_ID? [y/N]: " remove
    if [[ "${remove,,}" == "y" ]]; then
      pct stop "$CT_ID" 2>/dev/null || true
      pct destroy "$CT_ID" 2>/dev/null || true
      msg_ok "Container $CT_ID removed"
    else
      echo "  Container $CT_ID left in place for debugging."
      echo "  Clean up manually: pct stop $CT_ID && pct destroy $CT_ID"
    fi
  fi
}
trap cleanup EXIT

# ── Environment Checks ──────────────────────────────────────────────

header

if [[ $EUID -ne 0 ]]; then
  msg_error "This script must be run as root on the Proxmox VE host"
  exit 1
fi

if ! command -v pveversion &>/dev/null; then
  msg_error "Proxmox VE not detected. Run this script on your PVE host."
  exit 1
fi

PVE_VERSION=$(pveversion | grep -oP 'pve-manager/\K[0-9]+\.[0-9]+')
msg_ok "Proxmox VE $PVE_VERSION detected"
echo

# ── Phase 1: User Prompts ───────────────────────────────────────────

echo -e "${BL}── Container Settings ──${CL}"
echo

# Container ID
DEFAULT_CT_ID=$(pvesh get /cluster/nextid 2>/dev/null || echo "100")
read -rp "  Container ID [$DEFAULT_CT_ID]: " CT_ID
CT_ID="${CT_ID:-$DEFAULT_CT_ID}"

if pct status "$CT_ID" &>/dev/null; then
  msg_error "Container $CT_ID already exists"
  read -rp "  Destroy and recreate? [y/N]: " recreate
  if [[ "${recreate,,}" == "y" ]]; then
    pct stop "$CT_ID" 2>/dev/null || true
    pct destroy "$CT_ID"
    msg_ok "Container $CT_ID destroyed"
  else
    msg_error "Aborted. Choose a different container ID."
    exit 1
  fi
fi

# Hostname
read -rp "  Hostname [famick-hm]: " HN
HN="${HN:-famick-hm}"

# Disk size
read -rp "  Disk size in GB [8]: " DISK_SIZE
DISK_SIZE="${DISK_SIZE:-8}"

# RAM
read -rp "  RAM in MB [2048]: " RAM_SIZE
RAM_SIZE="${RAM_SIZE:-2048}"

# CPU cores
read -rp "  CPU cores [2]: " CPU_CORES
CPU_CORES="${CPU_CORES:-2}"

# Storage pool
mapfile -t POOLS < <(pvesm status --content rootdir 2>/dev/null | awk 'NR>1 && $2=="active" {print $1}')
if [[ ${#POOLS[@]} -eq 0 ]]; then
  # Fallback: try listing all storage
  mapfile -t POOLS < <(pvesm status 2>/dev/null | awk 'NR>1 && $2=="active" {print $1}')
fi

if [[ ${#POOLS[@]} -eq 1 ]]; then
  STORAGE="${POOLS[0]}"
  echo "  Storage pool: $STORAGE (auto-detected)"
elif [[ ${#POOLS[@]} -gt 1 ]]; then
  echo "  Available storage pools:"
  for i in "${!POOLS[@]}"; do
    echo "    $((i+1))) ${POOLS[$i]}"
  done
  read -rp "  Select storage pool [1]: " pool_choice
  pool_choice="${pool_choice:-1}"
  STORAGE="${POOLS[$((pool_choice-1))]}"
else
  read -rp "  Storage pool [local-lvm]: " STORAGE
  STORAGE="${STORAGE:-local-lvm}"
fi

# Network
echo
read -rp "  Use DHCP? [Y/n]: " use_dhcp
if [[ "${use_dhcp,,}" == "n" ]]; then
  DEFAULT_GW=$(ip route | awk '/default/ {print $3; exit}')
  read -rp "  Static IP (CIDR, e.g. 192.168.1.50/24): " STATIC_IP
  read -rp "  Gateway [$DEFAULT_GW]: " NET_GW
  NET_GW="${NET_GW:-$DEFAULT_GW}"
  NET_CONFIG="ip=${STATIC_IP},gw=${NET_GW}"
else
  NET_CONFIG="ip=dhcp"
fi

# Ports
echo
read -rp "  HTTP port [80]: " HTTP_PORT
HTTP_PORT="${HTTP_PORT:-80}"
read -rp "  HTTPS port [443]: " HTTPS_PORT
HTTPS_PORT="${HTTPS_PORT:-443}"

# ── Email Settings ───────────────────────────────────────────────────

echo
echo -e "${BL}── Email Settings (optional) ──${CL}"
echo "  Required for password reset emails. Can be configured later."
echo
read -rp "  Configure email now? [y/N]: " configure_email

SMTP_HOST=""
SMTP_PORT="587"
SMTP_USERNAME=""
SMTP_PASSWORD=""
SMTP_ENABLE_SSL="true"
SMTP_FROM_EMAIL="noreply@localhost"
SMTP_FROM_NAME="Famick Home Management"

if [[ "${configure_email,,}" == "y" ]]; then
  read -rp "  SMTP Host: " SMTP_HOST
  read -rp "  SMTP Port [587]: " input; SMTP_PORT="${input:-587}"
  read -rp "  SMTP Username: " SMTP_USERNAME
  read -rsp "  SMTP Password: " SMTP_PASSWORD; echo
  read -rp "  Enable SSL? [Y/n]: " input
  [[ "${input,,}" == "n" ]] && SMTP_ENABLE_SSL="false"
  read -rp "  From Email [noreply@localhost]: " input; SMTP_FROM_EMAIL="${input:-noreply@localhost}"
  read -rp "  From Name [Famick Home Management]: " input; SMTP_FROM_NAME="${input:-Famick Home Management}"
fi

# ── Geoapify API Key ────────────────────────────────────────────────

echo
echo -e "${BL}── Geoapify (optional) ──${CL}"
echo "  Enables address autocomplete. Get a free API key at:"
echo "  https://www.geoapify.com"
echo
read -rp "  Geoapify API key [skip]: " GEOAPIFY_API_KEY
GEOAPIFY_API_KEY="${GEOAPIFY_API_KEY:-}"

# ── Generate Secrets ─────────────────────────────────────────────────

DB_PASSWORD=$(openssl rand -base64 16 | tr -d '/+=' | head -c 20)
JWT_SECRET=$(openssl rand -base64 32 | tr -d '/+=' | head -c 48)
CERT_PASSWORD=$(openssl rand -base64 12 | tr -d '/+=' | head -c 16)

# ── Summary ──────────────────────────────────────────────────────────

echo
echo -e "${BL}── Installation Summary ──${CL}"
echo
echo "  Container ID:   $CT_ID"
echo "  Hostname:       $HN"
echo "  Storage:        $STORAGE"
echo "  Disk:           ${DISK_SIZE}GB"
echo "  RAM:            ${RAM_SIZE}MB"
echo "  CPU:            ${CPU_CORES} cores"
echo "  Network:        ${NET_CONFIG}"
echo "  HTTP port:      $HTTP_PORT"
echo "  HTTPS port:     $HTTPS_PORT"
echo "  Email:          $([ -n "$SMTP_HOST" ] && echo "configured ($SMTP_HOST)" || echo "not configured")"
echo "  Geoapify:       $([ -n "$GEOAPIFY_API_KEY" ] && echo "configured" || echo "not configured")"
echo
read -rp "  Proceed with installation? [Y/n]: " confirm
if [[ "${confirm,,}" == "n" ]]; then
  echo "  Aborted."
  exit 0
fi

echo

# ── Phase 2: Download LXC Template ──────────────────────────────────

msg_info "Checking for Debian 12 LXC template"

TEMPLATE_STORAGE="local"
# Check if the template storage has 'vztmpl' content type
if pvesm status --content vztmpl 2>/dev/null | awk 'NR>1 {print $1}' | grep -q "^${STORAGE}$"; then
  TEMPLATE_STORAGE="$STORAGE"
fi

TEMPLATE_NAME=$(pveam available --section system 2>/dev/null | grep 'debian-12-standard' | sort -t_ -k2 -V | tail -1 | awk '{print $2}')
if [[ -z "$TEMPLATE_NAME" ]]; then
  msg_error "Could not find Debian 12 template. Check your internet connection."
  exit 1
fi

TEMPLATE_PATH="${TEMPLATE_STORAGE}:vztmpl/${TEMPLATE_NAME}"

if ! pveam list "$TEMPLATE_STORAGE" 2>/dev/null | grep -q "$TEMPLATE_NAME"; then
  msg_info "Downloading $TEMPLATE_NAME"
  pveam download "$TEMPLATE_STORAGE" "$TEMPLATE_NAME" >/dev/null 2>&1
  msg_ok "Downloaded $TEMPLATE_NAME"
else
  msg_ok "Template $TEMPLATE_NAME already available"
fi

# ── Phase 3: Create LXC Container ───────────────────────────────────

msg_info "Creating LXC container $CT_ID"

pct create "$CT_ID" "$TEMPLATE_PATH" \
  --hostname "$HN" \
  --storage "$STORAGE" \
  --rootfs "$STORAGE:$DISK_SIZE" \
  --memory "$RAM_SIZE" \
  --cores "$CPU_CORES" \
  --net0 "name=eth0,bridge=vmbr0,$NET_CONFIG" \
  --ostype debian \
  --features nesting=1,keyctl=1 \
  --unprivileged 1 \
  --onboot 1 \
  --start 0 \
  >/dev/null 2>&1

CONTAINER_CREATED=true
msg_ok "Created LXC container $CT_ID"

# ── Phase 4: Start Container ────────────────────────────────────────

msg_info "Starting container"
pct start "$CT_ID"

# Wait for container to be running
for i in $(seq 1 30); do
  if pct status "$CT_ID" 2>/dev/null | grep -q "running"; then
    break
  fi
  sleep 1
done
msg_ok "Container started"

# Wait for network
msg_info "Waiting for network"
for i in $(seq 1 30); do
  if pct exec "$CT_ID" -- ping -c1 -W1 8.8.8.8 &>/dev/null; then
    break
  fi
  sleep 2
done
msg_ok "Network ready"

# ── Phase 5: Setup Inside Container ─────────────────────────────────

# Step 5a: System update and prerequisites
msg_info "Updating system packages"
pct exec "$CT_ID" -- bash -c "
  apt-get update -qq &&
  apt-get -y -qq upgrade &&
  apt-get -y -qq install curl ca-certificates gnupg openssl >/dev/null 2>&1
" >/dev/null 2>&1
msg_ok "System packages updated"

# Step 5b: Install Docker
msg_info "Installing Docker"
pct exec "$CT_ID" -- bash -c '
  install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://download.docker.com/linux/debian/gpg -o /etc/apt/keyrings/docker.asc
  chmod a+r /etc/apt/keyrings/docker.asc
  echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/debian $(. /etc/os-release && echo "$VERSION_CODENAME") stable" > /etc/apt/sources.list.d/docker.list
  apt-get update -qq
  apt-get -y -qq install docker-ce docker-ce-cli containerd.io docker-compose-plugin >/dev/null 2>&1
  systemctl enable docker >/dev/null 2>&1
  systemctl start docker
' >/dev/null 2>&1
msg_ok "Docker installed"

# Verify Docker is working
if ! pct exec "$CT_ID" -- docker info &>/dev/null; then
  msg_error "Docker failed to start in unprivileged container"
  echo
  echo "  This can happen due to AppArmor restrictions."
  echo "  Try adding to /etc/pve/lxc/${CT_ID}.conf:"
  echo "    lxc.apparmor.profile: unconfined"
  echo "  Then restart the container: pct reboot $CT_ID"
  echo
  echo "  Alternatively, recreate as a privileged container"
  echo "  (remove --unprivileged 1 from the create command)."
  exit 1
fi

# Step 5c: Create application directory structure
msg_info "Setting up application directories"
pct exec "$CT_ID" -- bash -c "
  mkdir -p /opt/famick-hm/{certs,keys,config,plugins,logs,uploads}
"
msg_ok "Application directories created"

# Step 5d: Generate RSA key and self-signed certificate
msg_info "Generating encryption keys and certificates"
pct exec "$CT_ID" -- bash -c "
  # RSA private key for JWT signing
  openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 \
    -out /opt/famick-hm/keys/jwt-rsa.pem 2>/dev/null
  chmod 600 /opt/famick-hm/keys/jwt-rsa.pem

  # Self-signed HTTPS certificate
  openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
    -keyout /tmp/aspnetapp.key \
    -out /tmp/aspnetapp.crt \
    -subj '/C=US/ST=Local/L=Local/O=Famick/CN=localhost' \
    -addext 'subjectAltName=DNS:localhost,DNS:*.localhost,IP:127.0.0.1' \
    2>/dev/null

  openssl pkcs12 -export \
    -out /opt/famick-hm/certs/aspnetapp.pfx \
    -inkey /tmp/aspnetapp.key \
    -in /tmp/aspnetapp.crt \
    -password pass:${CERT_PASSWORD} \
    2>/dev/null

  rm -f /tmp/aspnetapp.key /tmp/aspnetapp.crt
"
msg_ok "Keys and certificates generated"

# Step 5e: Write .env file
msg_info "Writing configuration"
pct exec "$CT_ID" -- bash -c "cat > /opt/famick-hm/.env << ENVEOF
# Famick Home Management - Environment Configuration
# Generated by Proxmox LXC installer

# Database
DB_PASSWORD=${DB_PASSWORD}

# JWT Authentication
JWT_SECRET_KEY=${JWT_SECRET}

# HTTPS Certificate
CERT_PASSWORD=${CERT_PASSWORD}

# Ports
HTTP_PORT=${HTTP_PORT}
HTTPS_PORT=${HTTPS_PORT}

# Email Settings
SMTP_HOST=${SMTP_HOST}
SMTP_PORT=${SMTP_PORT}
SMTP_USERNAME=${SMTP_USERNAME}
SMTP_PASSWORD=${SMTP_PASSWORD}
SMTP_ENABLE_SSL=${SMTP_ENABLE_SSL}
SMTP_FROM_EMAIL=${SMTP_FROM_EMAIL}
SMTP_FROM_NAME=${SMTP_FROM_NAME}

# Geoapify - Address Autocomplete
# Get a free API key at https://www.geoapify.com
GEOAPIFY_API_KEY=${GEOAPIFY_API_KEY}
ENVEOF
chmod 600 /opt/famick-hm/.env
"

# Step 5f: Write init-db.sql
pct exec "$CT_ID" -- bash -c 'cat > /opt/famick-hm/init-db.sql << '"'"'SQLEOF'"'"'
-- Initialize database for self-hosted deployment
-- EF Core migrations run automatically on app startup
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
SQLEOF
'

# Step 5g: Write docker-compose.yml
pct exec "$CT_ID" -- bash -c 'cat > /opt/famick-hm/docker-compose.yml << '"'"'COMPEOF'"'"'
services:
  postgres:
    image: postgres:16-alpine
    container_name: homemanagement-db
    environment:
      POSTGRES_DB: homemanagement
      POSTGRES_USER: homemanagement
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./init-db.sql:/docker-entrypoint-initdb.d/init.sql
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U homemanagement"]
      interval: 10s
      timeout: 5s
      retries: 5
    restart: unless-stopped
    networks:
      - homemanagement-network

  web:
    image: famick/homemanagement:latest
    container_name: homemanagement-web
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:80;https://+:443
      - ASPNETCORE_Kestrel__Certificates__Default__Path=/https/aspnetapp.pfx
      - ASPNETCORE_Kestrel__Certificates__Default__Password=${CERT_PASSWORD}
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=homemanagement;Username=homemanagement;Password=${DB_PASSWORD}
      - SelfHosted__TenantId=00000000-0000-0000-0000-000000000001
      - SelfHosted__ApplicationName=Home Management
      - JwtSettings__SecretKey=${JWT_SECRET_KEY}
      - JwtSettings__RsaPrivateKeyPemFile=/app/keys/jwt-rsa.pem
      - JwtSettings__Issuer=https://localhost
      - JwtSettings__Audience=https://localhost
      - JwtSettings__AccessTokenExpirationMinutes=15
      - JwtSettings__RefreshTokenExpirationDays=7
      - EmailSettings__Smtp__Host=${SMTP_HOST:-}
      - EmailSettings__Smtp__Port=${SMTP_PORT:-587}
      - EmailSettings__Smtp__Username=${SMTP_USERNAME:-}
      - EmailSettings__Smtp__Password=${SMTP_PASSWORD:-}
      - EmailSettings__Smtp__EnableSsl=${SMTP_ENABLE_SSL:-true}
      - EmailSettings__FromEmail=${SMTP_FROM_EMAIL:-noreply@localhost}
      - EmailSettings__FromName=${SMTP_FROM_NAME:-Famick Home Management}
      - Geoapify__ApiKey=${GEOAPIFY_API_KEY:-}
      - Geoapify__BaseUrl=https://api.geoapify.com/v1/geocode
    ports:
      - "${HTTP_PORT:-80}:80"
      - "${HTTPS_PORT:-443}:443"
    volumes:
      - ./certs:/https:ro
      - ./config:/app/config:ro
      - ./plugins:/app/plugins
      - ./logs:/app/logs
      - ./uploads:/app/wwwroot/uploads
      - ./keys:/app/keys:ro
      - dataprotection_keys:/root/.aspnet/DataProtection-Keys
    depends_on:
      postgres:
        condition: service_healthy
    restart: unless-stopped
    networks:
      - homemanagement-network

networks:
  homemanagement-network:
    driver: bridge

volumes:
  postgres_data:
    driver: local
  dataprotection_keys:
    driver: local
COMPEOF
'

msg_ok "Configuration files written"

# Step 5h: Pull images and start
msg_info "Pulling Docker images (this may take a few minutes)"
pct exec "$CT_ID" -- bash -c "cd /opt/famick-hm && docker compose pull" >/dev/null 2>&1
msg_ok "Docker images pulled"

msg_info "Starting Famick Home Management"
pct exec "$CT_ID" -- bash -c "cd /opt/famick-hm && docker compose up -d" >/dev/null 2>&1

# Step 5i: Wait for healthy
msg_info "Waiting for application to start"
HEALTHY=false
for i in $(seq 1 90); do
  if pct exec "$CT_ID" -- docker inspect --format='{{.State.Health.Status}}' homemanagement-web 2>/dev/null | grep -q "healthy"; then
    HEALTHY=true
    break
  fi
  sleep 2
done

if [[ "$HEALTHY" == "true" ]]; then
  msg_ok "Application is healthy"
else
  # Check if container is at least running
  if pct exec "$CT_ID" -- docker ps --filter name=homemanagement-web --format '{{.Status}}' 2>/dev/null | grep -q "Up"; then
    msg_ok "Application is running (health check still pending)"
  else
    msg_error "Application failed to start"
    echo "  Check logs: pct exec $CT_ID -- docker compose -f /opt/famick-hm/docker-compose.yml logs"
    exit 1
  fi
fi

# ── Phase 6: Post-Install Output ────────────────────────────────────

# Get container IP
IP_ADDR=$(pct exec "$CT_ID" -- hostname -I 2>/dev/null | awk '{print $1}')
if [[ -z "$IP_ADDR" ]]; then
  IP_ADDR="<container-ip>"
fi

echo
echo -e "${GN}══════════════════════════════════════════════════════════════${CL}"
echo -e "${GN}  Famick Home Management - Installation Complete${CL}"
echo -e "${GN}══════════════════════════════════════════════════════════════${CL}"
echo
echo -e "  ${BL}Container${CL}"
echo "    ID:          $CT_ID"
echo "    Hostname:    $HN"
echo "    IP Address:  $IP_ADDR"
echo
echo -e "  ${BL}Application URLs${CL}"
echo "    HTTP:    http://${IP_ADDR}:${HTTP_PORT}"
echo "    HTTPS:   https://${IP_ADDR}:${HTTPS_PORT}"
echo "    Swagger: http://${IP_ADDR}:${HTTP_PORT}/swagger"
echo
echo -e "  ${BL}Database${CL}"
echo "    Host:     postgres (internal Docker network)"
echo "    Database: homemanagement"
echo "    User:     homemanagement"
echo "    Password: $DB_PASSWORD"
echo
echo -e "  ${BL}Services${CL}"
echo "    Email:    $([ -n "$SMTP_HOST" ] && echo "${GN}configured${CL} ($SMTP_HOST)" || echo "${YW}not configured${CL}")"
echo "    Geoapify: $([ -n "$GEOAPIFY_API_KEY" ] && echo "${GN}configured${CL}" || echo "${YW}not configured${CL}")"
echo
echo -e "  ${BL}Configuration${CL}"
echo "    File: /opt/famick-hm/.env (inside container)"
echo "    Edit to configure email, Geoapify, or other settings"
echo
echo -e "  ${BL}Useful Commands${CL}"
echo "    Enter container:  pct enter $CT_ID"
echo "    View logs:        pct exec $CT_ID -- docker compose -f /opt/famick-hm/docker-compose.yml logs -f"
echo "    Update app:       pct exec $CT_ID -- bash -c 'cd /opt/famick-hm && docker compose pull && docker compose up -d'"
echo "    Stop:             pct exec $CT_ID -- docker compose -f /opt/famick-hm/docker-compose.yml down"
echo "    Start:            pct exec $CT_ID -- docker compose -f /opt/famick-hm/docker-compose.yml up -d"
echo
echo -e "${GN}══════════════════════════════════════════════════════════════${CL}"
echo
