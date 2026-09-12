export type WorkspaceApplication = {
  name: string;
  state: string;
  kind?: 'Process' | 'Desktop';
};

export type Workspace = {
  id: string;
  displayName: string;
  repositoryPath: string;
  worktreePath: string;
  branch: string;
  commit: string;
  state: string;
  applications?: WorkspaceApplication[];
};

export type CloneSourceRequest = {
  repository: string;
  branch: string;
};
