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

The default existing secret key is `AGENTUP_ADMIN_PASSWORD`.

## Capabilities

Every first-party capability is listed as an object with `disabled` and
`versions`. Set `disabled: false` to write that capability into Agent-Up
inventory at `/etc/agent-up/capabilities.json`. Disabled capabilities are
omitted from the inventory file.

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

The chart defaults list `docker` and `dotnet` with `disabled: true`.
