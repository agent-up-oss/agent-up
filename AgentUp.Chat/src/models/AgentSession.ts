/**
 * The sign-in challenge shape comes from the shared module so mobile and the installable web
 * client agree on it, and so the transport stays the only thing either of them branches on.
 */
import type { AgentLoginChallenge, AgentLoginTransport } from '@agent-up/agent-auth';

export type AgentKind = 'Codex' | 'Cursor' | 'Claude';
export type AgentDescriptor = { agent: AgentKind; available: boolean; displayName: string };
export type AgentAuthMethod = { id: string; name: string; description: string | null };
export type AgentSessionSummary = { sessionId: string; agent: AgentKind; description: string; branch: string; lastUsedAt: string };
export type { AgentLoginChallenge, AgentLoginTransport };
export type AgentSession = { workspaceId: string; agent: AgentKind | null; state: string; sessionId: string | null; error: string | null; agents: AgentDescriptor[]; authMethods?: AgentAuthMethod[]; loginChallenge?: AgentLoginChallenge | null; sessions?: AgentSessionSummary[] };
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
