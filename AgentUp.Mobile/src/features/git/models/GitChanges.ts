export type GitChangeStatus =
  | 'Modified'
  | 'Added'
  | 'Deleted'
  | 'Renamed'
  | 'Untracked'
  | 'Conflicted';

export type GitChangeFile = {
  name: string;
  path: string;
  status: GitChangeStatus;
};

export type GitChangeDirectory = {
  name: string;
  path: string;
  directories: GitChangeDirectory[];
  files: GitChangeFile[];
};

export type GitChangeTree = {
  workspaceId: string;
  branch: string;
  fileCount: number;
  root: GitChangeDirectory;
  localBranches?: string[];
};

export type GitFileDiff = {
  path: string;
  status: GitChangeStatus;
  isBinary: boolean;
  diff: string;
};

export type GitCommitResult = {
  found: boolean;
  succeeded: boolean;
  commit: string | null;
  error: string | null;
};

export type GitMutationResult = {
  found: boolean;
  succeeded: boolean;
  error: string | null;
};

export type GitHeadState = {
  branch: string;
  localBranches: string[];
};

// One row of the flattened, indented directory listing the clients render.
export type GitChangeNode = {
  key: string;
  name: string;
  path: string;
  depth: number;
  isDirectory: boolean;
  status: GitChangeStatus | null;
};
