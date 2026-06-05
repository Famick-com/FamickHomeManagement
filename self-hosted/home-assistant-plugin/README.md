# Home Assistant Add-on

**Status: planned.** This folder is a placeholder for a Home Assistant Supervisor add-on that runs Famick Home Management alongside Home Assistant OS / Supervised installs.

## Intended layout (not yet implemented)

```
home-assistant-plugin/
├── config.yaml                Add-on metadata (name, version, slug, options schema, ingress, ports)
├── Dockerfile                 Built on top of the canonical self-hosted image
├── run.sh                     Entry script the Supervisor invokes
├── translations/
│   └── en.yaml
├── icon.png
├── logo.png
└── README.md
```

## Open design questions

- Ingress integration: expose via HA's reverse-proxy ingress (preferred — single sign-on with HA's auth) vs standalone port.
- Persistent data: HA's `/data` volume vs separate add-on volumes for `config/`, `plugins/`, `uploads/`.
- Postgres: bundle alongside (single-container add-on) vs require the user's existing postgres add-on.
- Auth: trust HA's user identity vs require a separate Famick admin user.

## Until the add-on lands

HA OS users with SSH access can install Docker via the community add-on store and run the [docker-compose](../docker-compose/README.md) strategy directly.
