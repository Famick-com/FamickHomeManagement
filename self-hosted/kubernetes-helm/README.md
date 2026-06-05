# Kubernetes / Helm Deployment

**Status: planned.** This folder is a placeholder for a Helm chart that will deploy Famick Home Management onto an existing Kubernetes cluster.

## Intended layout (not yet implemented)

```
kubernetes-helm/
├── Chart.yaml
├── values.yaml                    Default values (image tag, ingress host, resource requests)
├── values.production.example.yaml Example production override
├── templates/
│   ├── deployment-web.yaml        Web app + scheduler sidecar
│   ├── service-web.yaml
│   ├── ingress.yaml
│   ├── configmap-server-config.yaml   Wraps server-config.json
│   ├── secret-env.yaml            Database password, JWT key, cert password
│   ├── pvc-plugins.yaml           Persistent volume claim for plugins/
│   ├── pvc-uploads.yaml
│   └── statefulset-postgres.yaml  Optional bundled postgres (default: external)
└── README.md
```

## Open design questions

- Bundle postgres as a StatefulSet vs require external (default: external).
- Plugins delivery: PVC bind mount vs init-container that fetches from a registry.
- Server-config: ConfigMap (edits via `kubectl edit`) vs mounted file the admin UI can write back to (requires a writable PVC).
- Ingress controller: assume nginx by default, document Traefik/Caddy.

## Until the chart lands

K8s users can run the docker-compose stack inside a single pod via a hand-rolled Deployment, or use [`proxmox/`](../proxmox/README.md)-style provisioning on a worker node.
