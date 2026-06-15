# Home Assistant Add-on

**Status: in progress.** Files in this folder are the Famick side of an HA
Supervisor add-on. The add-on manifest, Dockerfile, and s6 service layout
live in a separate wrapper repo (`Famick-com/famick-home-assistant-addon`)
which `FROM`s the public Famick image and adds Postgres + s6 on top.

## What lives here

- `bootstrap.sh` — Famick-app-level first-boot script the wrapper repo
  drops into `/etc/cont-init.d/`. Idempotent. Generates the RSA JWT key,
  per-install tenant UUID, starter `server-config.json`, and plugin
  config seed under `/data/`.

## Decisions (see plan for full context)

- **Single container** with bundled Postgres (s6-overlay supervises web +
  Postgres + scheduler).
- **/data** (Supervisor's persistent mount) holds everything: `postgres/`
  data dir, `keys/jwt-rsa.pem`, `config/`, `plugins/`, `uploads/`,
  `dataprotection/`.
- **HA Ingress + SSO** — Supervisor authenticates the HA user at the
  edge and forwards `X-Remote-User-*` headers, which Famick's
  `HaIngressAuthenticationHandler` trusts and resolves to a local user.
- **No app-level TLS** — Supervisor terminates TLS at the ingress edge.

## Until the wrapper repo lands

HA OS users with SSH access can install Docker via the community add-on
store and run the [docker-compose](../docker-compose/README.md) strategy
directly.
