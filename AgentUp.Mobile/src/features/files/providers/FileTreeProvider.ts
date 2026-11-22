import type { GitChangeStatus } from '@/features/git/models/GitChanges';
import type { WorkspaceDirectoryListing, WorkspaceFileEntry } from '../models/WorkspaceFiles';

/**
 * What the Files tree knows: each folder the reader has unfolded, as the Server listed it, and
 * which of those are open now. Nothing else is held, so a folder nobody unfolded is never read.
 */
export type FileTreeState = {
  folders: Record<string, WorkspaceDirectoryListing>;
  expanded: string[];
};

export type FileTreeRow =
  | { kind: 'entry'; entry: WorkspaceFileEntry; depth: number; expanded: boolean }
  | { kind: 'notice'; key: string; depth: number; text: string };

export const emptyFileTree: FileTreeState = { folders: {}, expanded: [] };

/** The rows on screen, in order: a folder's rows follow it while it is unfolded. */
export function visibleFileRows(state: FileTreeState): FileTreeRow[] {
  const rows: FileTreeRow[] = [];
  const walk = (path: string, depth: number) => {
    const listing = state.folders[path];
    if (!listing) return;
    for (const entry of listing.entries) {
      const expanded = entry.kind === 'Directory' && state.expanded.includes(entry.path);
      rows.push({ kind: 'entry', entry, depth, expanded });
      if (expanded) walk(entry.path, depth + 1);
    }
    if (listing.truncated) {
      rows.push({
        kind: 'notice',
        key: `${path}#truncated`,
        depth,
        text: `Showing the first ${listing.entries.length} entries of this folder.`,
      });
    }
  };
  walk('', 0);
  return rows;
}

/** A folder the Server listed is kept; reading it again only replaces it. */
export function withListing(state: FileTreeState, listing: WorkspaceDirectoryListing): FileTreeState {
  return { ...state, folders: { ...state.folders, [listing.path]: listing } };
}

export function withExpanded(state: FileTreeState, path: string, expanded: boolean): FileTreeState {
  const others = state.expanded.filter(item => item !== path);
  return { ...state, expanded: expanded ? [...others, path] : others };
}

/** Whether unfolding a folder has to ask the Server, which it does only the first time. */
export function needsListing(state: FileTreeState, path: string): boolean {
  return state.folders[path] === undefined;
}

/**
 * The folders Reload reads again, top level first: those still unfolded and still under an
 * unfolded parent. A folder that was folded away is forgotten instead, so it is read fresh when
 * it is next opened.
 */
export function foldersToReload(state: FileTreeState): string[] {
  const open = new Set(state.expanded);
  const reachable = (path: string): boolean => {
    const slash = path.lastIndexOf('/');
    return slash < 0 || (open.has(path.slice(0, slash)) && reachable(path.slice(0, slash)));
  };
  return ['', ...state.expanded.filter(reachable).sort((a, b) => a.split('/').length - b.split('/').length)];
}

export function fileSizeLabel(bytes: number | null): string {
  if (bytes === null) return '';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${trim(bytes / 1024)} KB`;
  return `${trim(bytes / (1024 * 1024))} MB`;
}

/** The change-list glyph, so a file reads the same in Files as in Git. */
export function fileStatusGlyph(status: GitChangeStatus | null): string {
  switch (status) {
    case null: return '';
    case 'Added': return '+';
    case 'Untracked': return '?';
    case 'Deleted': return '−';
    case 'Renamed': return '→';
    case 'Conflicted': return '!';
    case 'Modified': return 'M';
  }
}

export type FileStatusClass =
  | 'gitStatusAdded'
  | 'gitStatusUntracked'
  | 'gitStatusDeleted'
  | 'gitStatusRenamed'
  | 'gitStatusConflicted'
  | 'gitStatusModified';

export function fileStatusClass(status: GitChangeStatus | null): FileStatusClass | null {
  switch (status) {
    case null: return null;
    case 'Added': return 'gitStatusAdded';
    case 'Untracked': return 'gitStatusUntracked';
    case 'Deleted': return 'gitStatusDeleted';
    case 'Renamed': return 'gitStatusRenamed';
    case 'Conflicted': return 'gitStatusConflicted';
    case 'Modified': return 'gitStatusModified';
  }
}

function trim(value: number): string {
  return (Math.round(value * 10) / 10).toString();
}
