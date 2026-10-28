import { useCallback, useMemo, useState } from 'react';
import { AgentChatScreen } from '@agent-up/chat';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { useWorkspaceInnerShell } from '@/features/shell/hooks/useWorkspaceInnerShell';
import { useWorkspaceHost } from '@/features/workspaces/controllers/WorkspaceHostContext';
import type { ServerSession } from '@/features/servers/providers/ServerRequestProvider';

/**
 * The transcript of one workspace's agent, with the policy it works under a press away. The chat
 * itself is the shared screen; this is the workspace's way into it.
 */
export function WorkspaceAgentChatScreen({
  workspaceId,
  displayName,
  server,
}: {
  workspaceId: string;
  displayName: string;
  server: ServerSession | null;
}) {
  const host = useWorkspaceHost(workspaceId);
  const [title, setTitle] = useState('Workspace agent');
  const present = useCallback((value: { title: string }) => setTitle(value.title), []);
  const inner = useWorkspaceInnerShell(workspaceId, title, AGENTS);
  const config = useMemo(() => ({ ...inner, title }), [inner, title]);
  useShellConfig(config);

  return (
    <AgentChatScreen
      workspace={{ id: workspaceId, displayName }}
      server={server}
      onPresent={present}
      onConfigurePolicy={() => host.open({ kind: 'agentPolicy' })}
    />
  );
}

const AGENTS = { kind: 'agents' } as const;
