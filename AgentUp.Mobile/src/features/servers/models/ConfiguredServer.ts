export type ConfiguredServer = {
  id: string;
  url: string;
  accessToken?: string;
  openAccess?: boolean;
  displayName?: string;
  isRecommended?: boolean;
  canRemove?: boolean;
};
