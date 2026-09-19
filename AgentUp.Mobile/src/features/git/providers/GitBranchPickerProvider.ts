import type { GitHeadState, GitRemoteBranch } from '../models/GitChanges';

export const GIT_BRANCH_PICKER_VISIBLE_ROWS = 5;
export const GIT_BRANCH_PICKER_ROW_HEIGHT = 40;

export type GitBranchPickerRow =
  | { kind: 'section'; title: 'Local' | 'Remote' }
  | { kind: 'local'; name: string }
  | { kind: 'remote'; remote: string; name: string; label: string };

export type GitConfirmCopy = {
  title: string;
  message: string;
  confirm: string;
  destructive?: boolean;
};

export type GitBranchMutationKind =
  | 'fetch'
  | 'pull'
  | 'push'
  | 'forcePush'
  | 'switch'
  | 'checkoutRemote'
  | 'create';

export type GitBranchPickerPointerSurface = 'overlay' | 'search' | 'list';

export function gitBranchPickerClosesFromPointer(surface: GitBranchPickerPointerSurface): boolean {
  return surface === 'overlay';
}

export function gitBranchPickerViewportHeight(
  visibleRows = GIT_BRANCH_PICKER_VISIBLE_ROWS,
  rowHeight = GIT_BRANCH_PICKER_ROW_HEIGHT,
): number {
  return visibleRows * rowHeight;
}

export function filterGitBranchPickerRows(
  localBranches: string[],
  remoteBranches: GitRemoteBranch[],
  query: string,
): GitBranchPickerRow[] {
  const needle = query.trim().toLowerCase();
  const matches = (value: string) => needle.length === 0 || value.toLowerCase().includes(needle);

  const local = localBranches.filter(name => matches(name));
  const remotes = remoteBranches.filter(item => {
    const label = formatRemoteBranch(item);
    return matches(label) || matches(item.name) || matches(item.remote);
  });

  const rows: GitBranchPickerRow[] = [];
  if (local.length > 0) {
    rows.push({ kind: 'section', title: 'Local' });
    for (const name of local) rows.push({ kind: 'local', name });
  }
  if (remotes.length > 0) {
    rows.push({ kind: 'section', title: 'Remote' });
    for (const item of remotes) {
      rows.push({
        kind: 'remote',
        remote: item.remote,
        name: item.name,
        label: formatRemoteBranch(item),
      });
    }
  }
  return rows;
}

export function gitFetchRemoteLabel(head: GitHeadState | null): string {
  const remotes = uniqueRemoteNames(head);
  if (remotes.length === 1) return remotes[0];
  if (remotes.length > 1) return `all remotes (${remotes.join(', ')})`;
  return remoteFromUpstream(head?.upstream) ?? 'all remotes';
}

export function gitUpstreamLabel(head: GitHeadState | null): string {
  const upstream = head?.upstream?.trim();
  return upstream && upstream.length > 0 ? upstream : 'its upstream';
}

export function gitBranchMutationConfirm(
  kind: GitBranchMutationKind,
  head: GitHeadState | null,
  target = '',
): GitConfirmCopy {
  const branch = head?.branch?.trim() || 'the current branch';
  const remote = gitFetchRemoteLabel(head);
  const upstream = gitUpstreamLabel(head);

  switch (kind) {
    case 'fetch':
      return {
        title: 'Fetch and prune?',
        message: remote.startsWith('all remotes')
          ? `This fetches and prunes from ${remote}.`
          : `This fetches and prunes from remote ${remote}.`,
        confirm: 'Fetch',
      };
    case 'pull':
      return {
        title: 'Pull fast-forward only?',
        message: `This fast-forwards the current branch ${branch} from ${upstream}.`,
        confirm: 'Pull',
      };
    case 'push':
      return {
        title: 'Push to upstream?',
        message: `This pushes the current branch ${branch} to ${upstream}.`,
        confirm: 'Push',
      };
    case 'forcePush':
      return {
        title: 'Force-push with lease?',
        message:
          `This force-pushes branch ${branch} to ${upstream} with --force-with-lease. `
          + 'The remote updates only if nobody else has pushed since your last fetch.',
        confirm: 'Force push',
        destructive: true,
      };
    case 'switch':
      return {
        title: 'Switch branch?',
        message: `This checks out the local branch ${target} in the workspace worktree.`,
        confirm: 'Switch',
      };
    case 'checkoutRemote':
      return {
        title: 'Checkout remote branch?',
        message: `This checks out ${target}, creating a local tracking branch if needed.`,
        confirm: 'Checkout',
      };
    case 'create':
      return {
        title: 'Create branch?',
        message: `This creates branch ${target} from the current HEAD and checks it out.`,
        confirm: 'Create',
      };
  }
}

function formatRemoteBranch(item: GitRemoteBranch): string {
  return `${item.remote}/${item.name}`;
}

function uniqueRemoteNames(head: GitHeadState | null): string[] {
  const names = new Set<string>();
  for (const item of head?.remoteBranches ?? []) {
    if (item.remote) names.add(item.remote);
  }
  return [...names].sort((left, right) => left.localeCompare(right));
}

function remoteFromUpstream(upstream: string | null | undefined): string | null {
  if (!upstream) return null;
  const slash = upstream.indexOf('/');
  return slash > 0 ? upstream.slice(0, slash) : null;
}
