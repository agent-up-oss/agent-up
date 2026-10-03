export function nextExpiredSignInGuard(
  requiresSignIn: boolean,
  hasActiveServer: boolean,
  alreadyApplied: boolean,
): { applied: boolean; run: boolean } {
  if (!requiresSignIn)
    return { applied: false, run: false };
  if (!hasActiveServer || alreadyApplied)
    return { applied: alreadyApplied, run: false };
  return { applied: true, run: true };
}
