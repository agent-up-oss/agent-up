import type { ForgeHosting } from '../models/ForgeHosting';

export type GitHostingCopy = {
  visible: boolean;
  title: string;
  checks: string;
  checksTone: 'danger' | 'warning' | 'healthy' | '';
  threads: string;
  mergeability: string;
};

function hidden(): GitHostingCopy {
  return { visible: false, title: '', checks: '', checksTone: '', threads: '', mergeability: '' };
}

function notice(title: string): GitHostingCopy {
  return { visible: true, title, checks: '', checksTone: '', threads: '', mergeability: '' };
}

export function hostingCopy(hosting: ForgeHosting | null): GitHostingCopy {
  if (!hosting || hosting.availability === 'none') {
    return hidden();
  }

  if (hosting.availability === 'needsCredential') {
    return notice('Sign in to GitHub in Server settings to see hosting state.');
  }

  if (hosting.availability === 'unreachable') {
    return notice(hosting.detail?.trim() || 'Could not reach the host.');
  }

  if (hosting.availability === 'noProposal') {
    return notice('No open proposal on this branch.');
  }

  const failed = hosting.checks.some(check => check.conclusion.toLowerCase() === 'failure');
  const pending = hosting.checks.some(check => check.status.toLowerCase() !== 'completed');
  const checks = failed ? 'Checks failed' : pending ? 'Checks pending' : hosting.checks.length === 0 ? 'No checks' : 'Checks passed';
  const count = hosting.threads.length;
  return {
    visible: true,
    title: hosting.proposal?.title ?? 'Open proposal',
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
