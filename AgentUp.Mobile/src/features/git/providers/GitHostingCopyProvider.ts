import type { ForgeHosting } from '../models/ForgeHosting';

export type GitHostingCopy = {
  visible: boolean;
  title: string;
  remedy: string;
  canSignIn: boolean;
  checks: string;
  checksTone: 'danger' | 'warning' | 'healthy' | '';
  threads: string;
  mergeability: string;
};

function hidden(): GitHostingCopy {
  return {
    visible: false,
    title: '',
    remedy: '',
    canSignIn: false,
    checks: '',
    checksTone: '',
    threads: '',
    mergeability: '',
  };
}

function fallbackTitle(availability: string): string {
  switch (availability) {
    case 'notInstalled':
    case 'none':
      return 'No forge capability package is enabled.';
    case 'needsCredential':
      return 'This workspace has no repository host credential.';
    case 'noRemote':
      return 'No repository host origin is configured.';
    case 'unreachable':
      return 'Could not reach the host.';
    case 'noProposal':
      return 'No open proposal on this branch.';
    default:
      return 'Repository hosting';
  }
}

export function hostingCopy(hosting: ForgeHosting | null): GitHostingCopy {
  if (!hosting) {
    return hidden();
  }

  if (hosting.availability === 'ready') {
    const failed = hosting.checks.some(check => check.conclusion.toLowerCase() === 'failure');
    const pending = hosting.checks.some(check => check.status.toLowerCase() !== 'completed');
    const checks = failed ? 'Checks failed' : pending ? 'Checks pending' : hosting.checks.length === 0 ? 'No checks' : 'Checks passed';
    const count = hosting.threads.length;
    return {
      visible: true,
      title: hosting.proposal?.title ?? 'Open proposal',
      remedy: '',
      canSignIn: false,
      checks,
      checksTone: failed ? 'danger' : pending ? 'warning' : 'healthy',
      threads: `${count} review thread${count === 1 ? '' : 's'}`,
      mergeability: hosting.mergeability == null
        ? 'Mergeability unknown'
        : hosting.mergeability.mergeable
          ? 'Mergeable'
          : 'Conflicts with the base',
    };
  }

  return {
    visible: true,
    title: hosting.detail?.trim() || fallbackTitle(hosting.availability),
    remedy: hosting.remedy?.trim() || '',
    canSignIn: hosting.availability === 'needsCredential',
    checks: '',
    checksTone: '',
    threads: '',
    mergeability: '',
  };
}
