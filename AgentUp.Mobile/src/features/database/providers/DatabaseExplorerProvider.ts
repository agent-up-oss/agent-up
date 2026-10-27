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

/** Matches Desktop's result grid: columns stay between 120 and 360 logical pixels. */
export function estimateColumnWidth(value: string): number {
  const length = value.length;
  const width = 12 + (length === 0 ? 8 : length) * 7.5;
  return Math.min(360, Math.max(120, width));
}

export function buildColumnWidths(columns: string[], rows: string[][]): number[] {
  return columns.map((column, index) => {
    let width = estimateColumnWidth(column);
    for (const row of rows) {
      const cell = row[index];
      if (cell != null) width = Math.max(width, estimateColumnWidth(cell));
    }
    return width;
  });
}
