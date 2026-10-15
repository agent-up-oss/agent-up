export type InnerBackAction = 'back' | 'fallback';

export function resolveInnerBack(canGoBack: boolean): InnerBackAction {
  return canGoBack ? 'back' : 'fallback';
}
