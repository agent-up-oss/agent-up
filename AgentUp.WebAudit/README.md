# @agent-up/audit

Records browser-side audit events in the Agent-Up Server that owns the managed
workspace. Agent-Up injects `AGENT_UP_AUDIT_ENDPOINT`, `AGENT_UP_WORKSPACE_ID`,
and `AGENT_UP_APPLICATION` into managed application processes; expose those as
the corresponding `agent-up-*` HTML meta tags or pass them to
`createAgentUpAudit` when producing the frontend shell.

```ts
import { createAgentUpAudit } from '@agent-up/audit';

const audit = createAgentUpAudit({ workspaceId, application });
await audit.record({ action: 'server_connection_failed', outcome: 'failure', details: { message } });
```
