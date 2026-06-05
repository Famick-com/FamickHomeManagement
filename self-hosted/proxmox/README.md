# Proxmox VE LXC Installer

Automated installer that deploys Famick Home Management as an LXC container on Proxmox VE with Docker and PostgreSQL.

## Prerequisites

- Proxmox VE 7.x or 8.x
- Internet access from the PVE host
- Root access to the PVE host shell

## Quick Start

Run on your Proxmox VE host shell:

```bash
bash -c "$(wget -qLO - https://raw.githubusercontent.com/Famick-com/FamickHomeManagement/main/scripts/proxmox/famick-homemanagement-lxc.sh)"
```

Or download and run:

```bash
wget https://raw.githubusercontent.com/Famick-com/FamickHomeManagement/main/scripts/proxmox/famick-homemanagement-lxc.sh
bash famick-homemanagement-lxc.sh
```

## What It Creates

- **Debian 12 LXC container** with Docker installed
- **PostgreSQL 16** (Alpine) running as a Docker container
- **Famick Home Management** web app pulled from Docker Hub (`famick/homemanagement:latest`)
- Auto-generated secrets (DB password, JWT key, HTTPS certificate)

## Configuration Prompts

The installer prompts for:

| Setting | Default |
|---------|---------|
| Container ID | Next available |
| Hostname | `famick-hm` |
| Disk size | 8 GB |
| RAM | 2048 MB |
| CPU cores | 2 |
| Storage pool | Auto-detected |
| Network | DHCP |
| HTTP port | 80 |
| HTTPS port | 443 |
| Email (SMTP) | Optional, skipped by default |
| Geoapify API key | Optional, skipped by default |

## Post-Install

### Access the Application

After installation, the app is available at:
- **HTTP**: `http://<container-ip>:<http-port>`
- **HTTPS**: `https://<container-ip>:<https-port>`
- **Swagger**: `http://<container-ip>:<http-port>/swagger`

### Update the Application

```bash
pct exec <CT_ID> -- bash -c "cd /opt/famick-hm && docker compose pull && docker compose up -d"
```

### View Logs

```bash
pct exec <CT_ID> -- docker compose -f /opt/famick-hm/docker-compose.yml logs -f
```

### Configure Email or Geoapify Later

Edit the `.env` file inside the container:

```bash
pct enter <CT_ID>
nano /opt/famick-hm/.env
cd /opt/famick-hm && docker compose up -d  # restart to apply changes
```

### Backup

Back up the container using Proxmox's built-in backup (Datacenter > Backup) or manually:

```bash
vzdump <CT_ID> --dumpdir /var/lib/vz/dump --mode snapshot
```

### Restore

```bash
pct restore <NEW_CT_ID> /var/lib/vz/dump/vzdump-lxc-<CT_ID>-*.tar.zst --storage local-lvm
```

## Uninstall

```bash
pct stop <CT_ID>
pct destroy <CT_ID>
```

## Troubleshooting

### Docker fails to start in unprivileged container

Add to `/etc/pve/lxc/<CT_ID>.conf`:

```
lxc.apparmor.profile: unconfined
```

Then reboot the container: `pct reboot <CT_ID>`

### Application fails to start

Check Docker logs:

```bash
pct exec <CT_ID> -- docker compose -f /opt/famick-hm/docker-compose.yml logs
```

### Storage pool not detected

Ensure your storage pool supports container rootfs. Specify the pool name manually when prompted.

### Template download fails

Check internet connectivity from the PVE host. You can manually download the template:

```bash
pveam download local debian-12-standard_12.7-1_amd64.tar.zst
```
