import { useLayoutEffect } from 'react';
import { defaultShellConfig, useAppShell, type AppShellConfig } from '../controllers/AppShellContext';

export function useShellConfig(config: AppShellConfig) {
  const { setConfig } = useAppShell();

  useLayoutEffect(() => {
    setConfig(config);
    return () => setConfig(defaultShellConfig);
  }, [setConfig, config]);
}
