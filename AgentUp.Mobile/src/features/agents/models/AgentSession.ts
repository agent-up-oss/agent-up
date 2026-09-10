export type AgentKind = 'Codex' | 'Cursor' | 'Claude';
export type AgentDescriptor = { agent: AgentKind; available: boolean; displayName: string };
export type AgentAuthMethod = { id: string; name: string; description: string | null };
export type AgentSession = { workspaceId: string; agent: AgentKind | null; state: string; sessionId: string | null; error: string | null; agents: AgentDescriptor[]; authMethods?: AgentAuthMethod[] };
export type AgentEvent = { sequence: number; type: string; payload: unknown; timestamp: string };
export type AgentPermission = { requestId: string; request: { options?: { optionId: string; name?: string; kind?: string }[]; toolCall?: { title?: string } } };
