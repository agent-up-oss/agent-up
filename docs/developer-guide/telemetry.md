---
title: Product telemetry
---

# Product telemetry

Agent-Up reports **its own** crashes and errors to the self-hosted Sentry at
`https://sentry.massivecreationlab.com`. Workspace and application diagnostics
stay in Server audit. See [Diagnostics](./diagnostics.md).

The cluster Sentry chart uses the errors-only profile. SDKs send exceptions,
`ILogger` Error events, and Warning breadcrumbs. Traces, spans, session replay,
and Sentry metrics stay off (`TracesSampleRate = 0`).

## Projects

These four projects are created by the GitOps `sentry-configurator` Job, not
by the Sentry UI and not by Agent-Up at runtime.

| Project | Process |
|---|---|
| `agent-up-server` | Helm Server and packaged `agent-up-server` |
| `agent-up-desktop` | Desktop |
| `agent-up-cli` | CLI |
| `agent-up-mobile` | Mobile web/PWA |

## Identity tags

Every event carries:

- **release**: informational version, image tag, or Mobile app version
- **environment**: `development` or `production`
- **tags**: `agentup.component` (`server` / `desktop` / `cli` / `mobile`),
  `agentup.deployment` (`helm` when `KUBERNETES_SERVICE_HOST` is set, otherwise
  `development` or `packaged`), and `agentup.rid` when known

Unset DSN is a no-op. Do not put a DSN in Helm values or in the Server image.
Do not mint Sentry auth tokens at runtime.

## Injection

| Audience | Source |
|---|---|
| Helm Server | Generated Secret `agent-up-sentry-dsn` key `SENTRY_DSN` (`optional: true`) via `server.existingSentrySecret` |
| Packaged Server | GitHub `SENTRY_DSN_SERVER` copied to service env at package time |
| Desktop / CLI | GitHub `SENTRY_DSN_DESKTOP` / `SENTRY_DSN_CLI` compiled with `/p:SentryDsn=...`; runtime `SENTRY_DSN` wins |
| Mobile | Cloudflare Pages `SENTRY_DSN_MOBILE` forwarded as `EXPO_PUBLIC_SENTRY_DSN` at web export |

Local `.env` may set `SENTRY_DSN` or `EXPO_PUBLIC_SENTRY_DSN` to opt in.

CI secret names are listed in [CI Configuration](./ci-configuration.md).
Cluster project creation lives in the GitOps `sentry-configurator` app.
