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

export type GitRemoteBranch = {
  remote: string;
  name: string;
};

export type GitChangeTree = {
  workspaceId: string;
  branch: string;
  fileCount: number;
  root: GitChangeDirectory;
  localBranches?: string[];
  remoteBranches?: GitRemoteBranch[];
  upstream?: string | null;
  ahead?: number;
  behind?: number;
  commit?: string | null;
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
  remoteBranches?: GitRemoteBranch[];
  upstream?: string | null;
  ahead?: number;
  behind?: number;
  commit?: string | null;
};

export type GitSyncResult = {
  found: boolean;
  succeeded: boolean;
  error: string | null;
  head: GitHeadState | null;
};

export type GitLogCommit = {
  id: string;
  shortId: string;
  parents: string[];
  subject: string;
  author: string;
  timestamp: string;
  refs: string[];
};

export type GitLog = {
  commits: GitLogCommit[];
};

export type GitLogRefKind = 'head' | 'local' | 'remote';

export type GitLogRef = {
  name: string;
  kind: GitLogRefKind;
};

export type GitLogGraphLink = {
  fromLane: number;
  toLane: number;
  colorLane: number;
};

export type GitLogRow = {
  commit: GitLogCommit;
  lane: number;
  parentLanes: number[];
  incomingLanes: number[];
  outgoing: GitLogGraphLink[];
  laneCount: number;
  checkoutName: string | null;
  refs: GitLogRef[];
};

export type GitChangeNode = {
  key: string;
  name: string;
  path: string;
  depth: number;
  isDirectory: boolean;
  status: GitChangeStatus | null;
};
