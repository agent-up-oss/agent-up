export function signInUrlForCurrentScope(
  startedScope: string,
  currentScope: string,
  authorizationUrl: string,
): string | null {
  return startedScope === currentScope ? authorizationUrl : null;
}

export function signInUrlAfterScopeChange(
  previousScope: string,
  nextScope: string,
  signInUrl: string | null,
): string | null {
  return previousScope === nextScope ? signInUrl : null;
}
