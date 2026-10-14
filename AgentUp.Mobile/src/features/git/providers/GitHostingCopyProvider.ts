import type { ForgeHosting } from '../models/ForgeHosting';

export type GitHostingCopy = {
  visible: boolean;
  title: string;
  checks: string;
  threads: string;
  mergeability: string;
};

export function hostingCopy(hosting: ForgeHosting | null): GitHostingCopy {
  if (!hosting || hosting.availability === 'none') {
    return { visible: false, title: '', checks: '', threads: '', mergeability: '' };
  }

  if (hosting.availability === 'needsCredential') {
    return { visible: true, title: 'Sign in to GitHub in Server settings to see hosting state.', checks: '', threads: '', mergeability: '' };
  }

  if (hosting.availability === 'unreachable') {
    return {
      visible: true,
      title: hosting.detail?.trim() || 'Could not reach the host.',
      checks: '',
      threads: '',
      mergeability: '',
    };
  }

  if (hosting.availability === 'noProposal') {
    return { visible: true, title: 'No open proposal on this branch.', checks: '', threads: '', mergeability: '' };
  }

  const failed = hosting.checks.some(check => check.conclusion.toLowerCase() === 'failure');
  const pending = hosting.checks.some(check => check.status.toLowerCase() !== 'completed');
  const checks = failed ? 'Checks failed' : pending ? 'Checks pending' : hosting.checks.length === 0 ? 'No checks' : 'Checks passed';
  const count = hosting.threads.length;
  return {
    visible: true,
    title: hosting.proposal?.title ?? 'Open proposal',
    checks,
    threads: `${count} review thread${count === 1 ? '' : 's'}`,
    mergeability: hosting.mergeability == null
      ? 'Mergeability unknown'
      : hosting.mergeability.mergeable
        ? 'Mergeable'
        : 'Conflicts with the base',
  };
}
