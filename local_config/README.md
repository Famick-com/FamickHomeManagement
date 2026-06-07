# local_config

Per-developer overlay for the operator-mutable data that should never land in git — plugin API keys, dev SMTP credentials, JWT issuer overrides, ASP.NET Data Protection keys, uploaded images. Mirrors the on-disk shape of a production self-hosted install (which uses a `data/` folder), just renamed to `local_config/` here to make the dev-only intent obvious.

The whole folder content is `.gitignore`d (except this README). Each developer fills it in once with their own values.

## How it's wired up

`src/Famick.HomeManagement.Web/appsettings.Development.json` points one setting at this folder:

```jsonc
"Storage": { "Path": "../../local_config" }
```

That single root drives the defaults for everything that lives under it:

| Derived setting | Default | Resolved in dev |
|---|---|---|
| `Plugins:Path` | `{Storage:Path}/plugins` | `local_config/plugins` |
| `ServerConfig:Path` | `{Storage:Path}/config/server-config.json` | `local_config/config/server-config.json` |
| `DataProtection:Path` | `{Storage:Path}/dataprotection` | `local_config/dataprotection` |
| `Uploads:Path` | `{Storage:Path}/uploads` | `local_config/uploads` |

`Storage:Path` resolves relative to `ContentRootPath` (the Web project dir at dev time), so `../../local_config` lands at `famick-home-management/local_config/` regardless of where you launched `dotnet run` from. Any single derived path can still be overridden by setting it explicitly.

Production docker uses the same model with `Storage:Path = data` and a single `./data:/app/data` volume mount.

## Setting it up

```bash
mkdir -p local_config/plugins local_config/config local_config/dataprotection local_config/uploads

# Plugins: start from the example and fill in your API keys.
cp src/Famick.HomeManagement.Web/plugins/config.example.json local_config/plugins/config.json
$EDITOR local_config/plugins/config.json

# Everything else is created on demand by the first save / first upload.
```

## What lives here

```text
local_config/
├── README.md                       (this file — only thing tracked in git)
├── plugins/
│   └── config.json                 (plugin enable/disable + API keys)
├── config/
│   └── server-config.json          (SMTP creds, JWT issuer/audience, public host name)
├── dataprotection/
│   └── key-*.xml                   (ASP.NET Data Protection keys — antiforgery, cookies)
└── uploads/
    └── ...                         (user-uploaded product images, recipe images, etc.)
```

## Docker / production parity

Nothing changes for docker — `appsettings.Development.json` is only loaded when `ASPNETCORE_ENVIRONMENT=Development`. In docker the env is `Production` and `Storage:Path` falls back to `data` under `ContentRootPath`, which docker-compose maps to the host install dir via `./data:/app/data`.

## Resetting

To start fresh in dev: `rm -rf local_config/{plugins,config,dataprotection,uploads}` and re-run setup. The plugin loader falls back to "auto-load built-ins with defaults" when `plugins/config.json` is missing, the wizard rewrites `config/server-config.json` from scratch on first save, and the framework regenerates Data Protection keys on next start (everyone gets logged out once).
