/** One repository host, whether a personal access token is stored for it, and the origin that token talks to. */
export type TaskSourceModule = {
  moduleId: string;
  displayName: string;
  connected: boolean;
  detail: string;
  endpointUrl?: string | null;
};

/** The forge connections. The token is never in this document. */
export type TaskSource = {
  modules: TaskSourceModule[];
};

/** A repository the stored token can see. */
export type TaskSourceRepository = {
  moduleId: string;
  owner: string;
  name: string;
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

/** The id a repository row carries, so opening it names the host and the repository. */
export function repositoryId(repository: TaskSourceRepository): string {
  return `${repository.moduleId}/${repository.owner}/${repository.name}`;
}

/** The host a repository row names under its owner and name. */
export function forgeHostLabel(moduleId: string): string {
  return moduleId === 'forgejo' ? 'Forgejo' : 'GitHub';
}
