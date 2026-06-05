# Self-Hosted Deployment

Famick Home Management is designed to run on hardware you own. This folder holds one subfolder per deployment strategy so you can pick the one that fits your environment.

For the docker-compose strategy, the fastest path is the repo-root one-liner:

```bash
curl -fsSL https://raw.githubusercontent.com/Famick-com/FamickHomeManagement/main/install.sh | bash
```

| Strategy | Status | Best for |
|---|---|---|
| [`docker-compose/`](docker-compose/README.md) | Working | Linux/macOS hosts with Docker installed; small homelabs |
| [`proxmox/`](proxmox/README.md) | Working (script-only) | Proxmox VE clusters; auto-provisions a Debian LXC running the docker-compose stack |
| [`kubernetes-helm/`](kubernetes-helm/README.md) | Planned | Existing K8s clusters; declarative deploys |
| [`home-assistant-plugin/`](home-assistant-plugin/README.md) | Planned | Home Assistant OS / Supervised installs |

Every strategy reads the same server-level configuration from a single overlay file (`config/server-config.json`) and mounts a `plugins/` directory for runtime plugins. The path conventions differ per strategy — see each subfolder's README for the specifics.

## Server config (`server-config.json`)

This overlay extends the baked-in `appsettings.json` with operator-tunable settings: SMTP, public hostname, time zone, JWT issuer/audience, and plugin path. It can be edited two ways:

- **Admin UI** — Sign in as an admin, open Settings → Server Settings.
- **Host filesystem** — Edit the file directly. The running app picks up changes without a restart.

On first startup, the setup wizard populates this file before any household data is captured.

## Plugins

Each strategy mounts a `plugins/` directory. Plugin configuration is currently manual: drop external plugin DLLs into the directory and edit `plugins/config.json`. See [docker-compose/plugins/README.md](docker-compose/plugins/README.md) for the per-plugin schema.
