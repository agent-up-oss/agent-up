# Agent-Up Helm chart

This chart deploys the Agent-Up Server image from Docker Hub.

## Requirements

- ClusterIssuer: `letsencrypt-prod`
- IngressClass: `nginx`

The Server image and this chart are public on Docker Hub. Cluster workloads do not need an image pull secret.

## Install with a generated admin secret

```bash
helm upgrade --install agent-up ./chart \
  --set ingress.host=agent-up.massivecreationlab.com \
  --set server.adminPassword=YOUR_ADMIN_PASSWORD
```

## Install with an existing secret

```bash
helm upgrade --install agent-up ./chart \
  --set ingress.host=agent-up.massivecreationlab.com \
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

The Server image includes `git` plus the Codex, Cursor, and Claude ACP CLIs
under `/opt/agent-up/bin`. Source clones and workspace agent chat need those
binaries; they are not present in the stock ASP.NET runtime image.

## Capabilities

Every first-party capability is listed as an object with `disabled` and
`versions`. ACP capabilities also declare `command` and `arguments` so the
Server launches the binaries baked into the image. Set `disabled: false` to
write that capability into Agent-Up inventory at
`/etc/agent-up/capabilities.json`. Disabled capabilities are omitted from the
inventory file.

```yaml
capabilities:
  docker:
    disabled: false
    versions:
      - "27.x"
  dotnet:
    disabled: false
    versions:
      - "10.0.x"
  cursor:
    disabled: true
```

```bash
helm upgrade --install agent-up ./chart \
  --set ingress.host=agent-up.massivecreationlab.com \
  --set server.existingSecret=agent-up-server-secrets \
  --set capabilities.dotnet.disabled=false \
  --set capabilities.dotnet.versions="{10.0.x}" \
  --set capabilities.docker.disabled=false \
  --set capabilities.docker.versions="{27.x}"
```

The chart defaults enable `codex`, `cursor`, and `claude` against the bundled
ACP commands. `docker` and `dotnet` stay `disabled: true` until the operator
turns them on.
