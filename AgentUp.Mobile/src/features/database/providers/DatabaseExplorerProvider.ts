import type { WorkspaceApplication } from '@/features/workspaces/models/Workspace';

/** True when the Server declared this application as a database explorer surface. */
export function isDatabaseApplication(application: WorkspaceApplication | undefined): boolean {
  return application?.database === true;
}

/** Default preview query used when a table is selected. */
export function buildDefaultQuery(table: string): string {
  if (table.includes('.')) {
    return `SELECT * FROM ${table.split('.').map(quoteIdentifier).join('.')} LIMIT 50`;
  }
  return `SELECT * FROM ${quoteIdentifier(table)} LIMIT 50`;
}

export function quoteIdentifier(identifier: string): string {
  return `"${identifier.replaceAll('"', '""')}"`;
}
