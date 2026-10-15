export type WorkspaceApplication = {
  name: string;
  state: string;
  kind?: 'Process' | 'Desktop';
  allocatedPorts?: Array<{
    allocatedPort: number;
    protocol: string;
  }>;
  capabilityStatus?: {
    capabilityId: string;
    requiredVersion?: string | null;
    canRun: boolean;
    messages: string[];
  };
};

export type Workspace = {
  id: string;
  displayName: string;
  repositoryPath: string;
  worktreePath: string;
  branch: string;
  commit: string;
  state: string;
  healthState?: string;
  derived?: boolean;
  sourceRepository?: string | null;
  sourceRevision?: string | null;
  applications?: WorkspaceApplication[];
};

export type CloneSourceRequest = {
  repository: string;
  branch: string;
  revision?: string;
  derived?: boolean;
};
