export type AuditOutcome = 'success' | 'failure' | 'info';
export interface AgentUpAuditOptions {
    endpoint?: string;
    workspaceId?: string;
    application?: string;
    fetch?: typeof fetch;
}
export interface FrontendAuditEvent {
    action: string;
    outcome?: AuditOutcome;
    details?: Record<string, string | number | boolean | null | undefined>;
}
export interface AgentUpAuditClient {
    record(event: FrontendAuditEvent): Promise<void>;
}
export declare function createAgentUpAudit(options?: AgentUpAuditOptions): AgentUpAuditClient;
