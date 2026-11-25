/** One team member, whether a personal access token is stored for them, and which host it talks to. */
export type TaskSourceIdentity = {
  workspaceId: string;
  displayName: string;
  initials: string;
  moduleId?: string | null;
  connected: boolean;
  detail: string;
  endpointUrl?: string | null;
};

/** The forge connections. The token is never in this document. */
export type TaskSource = {
  identities: TaskSourceIdentity[];
};

/** A team member a client can draw as a round banner. */
export type TaskSourceMember = {
  workspaceId: string;
  displayName: string;
  initials: string;
};

/** A repository any stored token can see, and the team members whose tokens can see it. */
export type TaskSourceRepository = {
  moduleId: string;
  owner: string;
  name: string;
  members?: TaskSourceMember[];
};

/** One kind of review inferred from a pull request. */
export type TaskSourceReview = {
  kind: string;
  standing: string;
  login?: string | null;
  verdict?: string | null;
};

/** An open pull request and the reviews the Server inferred from it. */
export type TaskSourcePull = {
  id: string;
  number: number;
  title: string;
  url: string;
  reviews: TaskSourceReview[];
};

/** The two letters a round member banner shows when there is no portrait. */
export function memberInitials(name: string): string {
  const parts = String(name ?? '').split(/[\s\-_]+/).filter(Boolean);
  if (parts.length >= 2) return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
  const trimmed = String(name ?? '');
  return (trimmed.length >= 2 ? trimmed.slice(0, 2) : trimmed).toUpperCase();
}

/** The id a repository row carries, so opening it names the host and the repository. */
export function repositoryId(repository: TaskSourceRepository): string {
  return `${repository.moduleId}/${repository.owner}/${repository.name}`;
}

/** The host a repository row names under its owner and name. */
export function forgeHostLabel(moduleId: string): string {
  return moduleId === 'forgejo' ? 'Forgejo' : 'GitHub';
}
