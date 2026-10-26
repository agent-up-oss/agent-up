import { jsonBody, requestServerJson, type ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { DatabaseNames, DatabaseQueryResult, DatabaseTables } from '../models/DatabaseExplorer';

function databasePath(workspaceId: string, applicationName: string, rest: string): string {
  return `/api/workspaces/${encodeURIComponent(workspaceId)}/applications/${encodeURIComponent(applicationName)}/database/${rest}`;
}

/** Lists database names the Server can open for this application. */
export async function listDatabases(
  server: ServerSession,
  workspaceId: string,
  applicationName: string,
  request: typeof fetch = fetch,
): Promise<DatabaseNames> {
  const result = await requestServerJson<DatabaseNames>(
    server,
    databasePath(workspaceId, applicationName, 'databases'),
    { method: 'GET' },
    undefined,
    request,
  );
  return result ?? { databases: [] };
}

/** Lists tables in one database. */
export async function listTables(
  server: ServerSession,
  workspaceId: string,
  applicationName: string,
  database: string,
  request: typeof fetch = fetch,
): Promise<DatabaseTables> {
  const result = await requestServerJson<DatabaseTables>(
    server,
    `${databasePath(workspaceId, applicationName, 'tables')}?database=${encodeURIComponent(database)}`,
    { method: 'GET' },
    undefined,
    request,
  );
  return result ?? { tables: [] };
}

/** Runs SQL against one database and returns columns plus rows. */
export async function executeQuery(
  server: ServerSession,
  workspaceId: string,
  applicationName: string,
  database: string,
  sql: string,
  request: typeof fetch = fetch,
): Promise<DatabaseQueryResult> {
  const result = await requestServerJson<DatabaseQueryResult>(
    server,
    databasePath(workspaceId, applicationName, 'query'),
    jsonBody({ database, sql }),
    undefined,
    request,
  );
  return result ?? { columns: [], rows: [] };
}
