export type AgentKind = 'Codex' | 'Cursor' | 'Claude';
export type AgentDescriptor = { agent: AgentKind; available: boolean; displayName: string };
export type AgentSession = { workspaceId: string; agent: AgentKind | null; state: string; sessionId: string | null; error: string | null; agents: AgentDescriptor[] };
export type AgentEvent = { sequence: number; type: string; payload: unknown; timestamp: string };
export type AgentPermission = { requestId: string; request: { options?: { optionId: string; name?: string; kind?: string }[]; toolCall?: { title?: string } } };
