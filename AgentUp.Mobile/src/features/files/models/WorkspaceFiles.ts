import type { GitChangeStatus } from '@/features/git/models/GitChanges';

export type WorkspaceFileEntryKind = 'Directory' | 'File';

/** A file or folder in one listed folder, as the Server's Files slice reports it. */
export type WorkspaceFileEntry = {
  name: string;
  path: string;
  kind: WorkspaceFileEntryKind;
  size: number | null;
  /** The Git change of a file; null when unchanged or the workspace is not a repository. */
  status: GitChangeStatus | null;
  /** Changed files under a folder that still exist on disk. */
  changedFiles: number;
};

export type WorkspaceDirectoryListing = {
  workspaceId: string;
  path: string;
  entries: WorkspaceFileEntry[];
  truncated: boolean;
};

/** A whole file. Content is null for a binary file or one over the size the Server sends. */
export type WorkspaceFileContent = {
  path: string;
  size: number;
  version: string;
  status: GitChangeStatus | null;
  isBinary: boolean;
  isTooLarge: boolean;
  content: string | null;
};
