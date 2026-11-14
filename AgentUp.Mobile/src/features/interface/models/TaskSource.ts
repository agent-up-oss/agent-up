/** A repository host this Server could read a backlog from. */
export type TaskSourceModule = {
  moduleId: string;
  displayName: string;
};

/** Where this Server's agents take their work from, and whether it can be read. */
export type TaskSource = {
  moduleId: string | null;
  owner: string | null;
  repository: string | null;
  configured: boolean;
  connected: boolean;
  /** True when a sign-in can be started: a host is installed and OAuth is set up. */
  canConnect: boolean;
  detail: string;
  modules: TaskSourceModule[];
};

/** The repository as one string, which is how the source is named on screen. */
export function sourceLabel(source: TaskSource): string {
  return source.owner && source.repository ? `${source.owner}/${source.repository}` : 'Not set';
}

/**
 * Whether what the reader has typed could be saved.
 *
 * A half-named source is not kept: a repository needs a host, an owner and a name, and saving
 * two of the three would leave the Server pointed at nothing while looking configured.
 */
export function canSaveSource(moduleId: string, owner: string, repository: string): boolean {
  return moduleId.trim().length > 0 && owner.trim().length > 0 && repository.trim().length > 0;
}

/**
 * Splits what a reader pasted into owner and repository.
 *
 * People paste `owner/repo`, a full browser URL, or type the two parts separately. All three mean
 * the same repository, so all three are accepted rather than only the one shape we asked for.
 */
export function splitRepository(typed: string): { owner: string; repository: string } | null {
  const cleaned = typed.trim().replace(/^https?:\/\/[^/]+\//i, '').replace(/\.git$/i, '').replace(/\/+$/, '');
  const parts = cleaned.split('/').filter(part => part.length > 0);
  if (parts.length < 2) return null;
  return { owner: parts[parts.length - 2], repository: parts[parts.length - 1] };
}
