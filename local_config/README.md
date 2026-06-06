# local_config

Per-developer overlay for config that should never land in git — plugin API keys, dev SMTP credentials, JWT issuer overrides, etc. Mirrors the on-disk shape of the production self-hosted install: `plugins/config.json` + `config/server-config.json` sit at the top of this folder, and the app reads them at startup just like a docker compose deployment would.

The whole folder content is `.gitignore`d (except this README). Each developer fills it in once with their own values.

## How it's wired up

`src/Famick.HomeManagement.Web/appsettings.Development.json` points two settings at this folder:

```jsonc
"Plugins":      { "Path": "../../local_config/plugins" }
"ServerConfig": { "Path": "../../local_config/config/server-config.json" }
```

Both paths resolve relative to `ContentRootPath` (the Web project dir at dev time), which puts them at `famick-home-management/local_config/...` regardless of where you launch `dotnet run` from.

`Plugins:Path` is consumed by the plugin loader and `IPluginConfigService`; `ServerConfig:Path` is consumed by `Program.cs` (as a JSON config overlay with `reloadOnChange: true`) and by `IServerConfigService` (the writer behind the wizard + admin "Server Settings" page). The wizard's first save and any admin edit land in this folder when running locally — no project-tree contamination.

## Setting it up

```bash
mkdir -p local_config/plugins local_config/config

# Plugins: start from the example and fill in your API keys.
cp src/Famick.HomeManagement.Web/plugins/config.example.json local_config/plugins/config.json
$EDITOR local_config/plugins/config.json

# Server config: the wizard creates this on first run, or seed it now from
# any existing server-config.json. The admin Server Settings page edits
# whatever lives here.
```

## What lives here

```
local_config/
├── README.md                       (this file — only thing tracked in git)
├── plugins/
│   └── config.json                 (plugin enable/disable + API keys)
└── config/
    └── server-config.json          (SMTP creds, JWT issuer/audience, public host name, plugin path)
```

## Docker / production parity

Nothing changes for docker — `appsettings.Development.json` is only loaded when `ASPNETCORE_ENVIRONMENT=Development`. In docker the env is `Production` and the paths fall back to the canonical `/app/plugins/` and `/app/config/server-config.json`, which docker compose maps to the host install dir via the `./plugins:/app/plugins` and `./config:/app/config:ro` volume mounts.

## Resetting

To start fresh in dev: `rm -rf local_config/plugins local_config/config` and re-run setup. The plugin loader falls back to "auto-load built-ins with defaults" when `plugins/config.json` is missing, and the wizard rewrites `config/server-config.json` from scratch on first save.
