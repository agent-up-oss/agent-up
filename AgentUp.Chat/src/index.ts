/**
 * The agent chat, as a module.
 *
 * It reaches a Server through @agent-up/server-client and signs agents in through
 * @agent-up/agent-auth, and it takes everything else - which workspace, which Server, what to put
 * behind the Changes tab - from the host that mounts it. That is what lets the real client and the
 * end-to-end harness app run the same code rather than two versions of it.
 */
export { AgentChatScreen } from './components/AgentChatScreen';
export type { AgentChatScreenProps, ChatWorkspace } from './components/AgentChatScreen';
export { AgentSignIn } from './components/AgentSignIn';
export { createAgentLoginPort } from './providers/AgentLoginPortProvider';
export type { AgentLoginPortOptions, PendingRedirect } from './providers/AgentLoginPortProvider';
export * from './models/AgentSession';
