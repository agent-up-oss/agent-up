export type AgentKind = 'Codex' | 'Cursor' | 'Claude';
export type AgentDescriptor = { agent: AgentKind; available: boolean; displayName: string };
export type AgentAuthMethod = { id: string; name: string; description: string | null };
export type AgentSession = { workspaceId: string; agent: AgentKind | null; state: string; sessionId: string | null; error: string | null; agents: AgentDescriptor[]; authMethods?: AgentAuthMethod[] };
export type AgentEvent = { sequence: number; type: string; payload: unknown; timestamp: string };
export type AgentPermissionOption = { optionId: string; name: string; kind?: string };
export type AgentPermission = {
  requestId: string;
  title: string;
  detail?: string;
  toolKind?: string;
  locations: string[];
  options: AgentPermissionOption[];
};
export type TranscriptRole = 'user' | 'agent' | 'thought' | 'tool' | 'plan';
export type TranscriptItem = {
  id: string;
  role: TranscriptRole;
  text: string;
  title?: string;
  toolCallId?: string;
  status?: string;
  locations?: string[];
  entries?: { content: string; status: string }[];
};
export type SessionContext = { title?: string; mode?: string; usage?: string; compacting?: boolean };
export type AgentActivityKind = 'idle' | 'ready' | 'thinking' | 'writing' | 'tool' | 'plan' | 'permission' | 'auth' | 'running' | 'compacting' | 'stopped' | 'error';
export type AgentActivity = { kind: AgentActivityKind; label: string };
