# Agent-Up Helm chart

This chart deploys the Agent-Up Server image from Docker Hub.

## Requirements

- ClusterIssuer: `letsencrypt-prod`
- IngressClass: `nginx`

The Server image and this chart are public on Docker Hub. Cluster workloads do not need an image pull secret.

## Install with a generated admin secret

```bash
helm upgrade --install agent-up ./chart \
  --set ingress.host=agent-up.example.com \
  --set server.adminPassword=YOUR_ADMIN_PASSWORD
```

## Install with an existing secret

```bash
helm upgrade --install agent-up ./chart \
  --set ingress.host=agent-up.example.com \
  --set server.existingSecret=agent-up-server-secrets
```

```bash
kubectl create secret generic agent-up-server-secrets -n agent-up \
  --from-literal=AGENTUP_ADMIN_PASSWORD=YOUR_ADMIN_PASSWORD
```

GitOps can keep the DSN in a second generated Secret and set
`server.existingSentrySecret`. The default existing secret key is
`AGENTUP_ADMIN_PASSWORD`. The optional `SENTRY_DSN` key is injected when
present (`optional: true`). Do not put a DSN in Helm values. The chart also
sets `SENTRY_ENVIRONMENT=production` and `SENTRY_RELEASE` from the Server
image tag, or `Chart.AppVersion` when the tag is empty.

The Server image includes Nix (non-root) with a persistent store, plus `git`. Capability modules are enabled through Helm `capabilities.enabled` seeds and Server APIs.

## Capabilities

`capabilities.enabled` is a list of `{ id, version }` package refs written to `/etc/agent-up/enabled.json`. That file seeds Server enablement; later enable/disable calls update the Server data copy.

```yaml
capabilities:
  enabled:
    - id: dotnet
      version: "1.0.0"
    - id: docker
      version: "1.0.0"
    - id: codex
      version: "1.0.0"
```

```bash
helm upgrade --install agent-up ./chart \
  --set ingress.host=agent-up.example.com \
  --set server.existingSecret=agent-up-server-secrets \
  --set capabilities.enabled[0].id=dotnet \
  --set capabilities.enabled[0].version=1.0.0
```

The chart defaults enable first-party `dotnet`, `docker`, `codex`, `cursor`, and `claude` packages. The Server volume should stay large enough for the Nix store (default `server.storage` is 40Gi).
