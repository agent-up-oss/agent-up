export type ConfiguredServer = {
  id: string;
  url: string;
  accessToken?: string;
  openAccess?: boolean;
  displayName?: string;
  isRecommended?: boolean;
  canRemove?: boolean;
};

export function hasSavedSignIn(server: Pick<ConfiguredServer, 'accessToken' | 'openAccess'> | null | undefined): boolean {
  return Boolean(server?.accessToken) || server?.openAccess === true;
}
