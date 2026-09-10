import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type PropsWithChildren,
  type ReactNode,
} from 'react';

export type ShellAction = {
  label: string;
  accessibilityLabel?: string;
  onPress: () => void;
};

export type AppShellConfig = {
  title: string;
  rightAction: ShellAction | null;
  sidebarContent: ReactNode | null;
};

export const defaultShellConfig: AppShellConfig = {
  title: '',
  rightAction: null,
  sidebarContent: null,
};

type AppShellController = {
  config: AppShellConfig;
  sidebarOpen: boolean;
  setConfig(config: AppShellConfig): void;
  openSidebar(): void;
  closeSidebar(): void;
};

const Context = createContext<AppShellController | null>(null);

export function AppShellProvider({ children }: PropsWithChildren) {
  const [config, setConfigState] = useState<AppShellConfig>(defaultShellConfig);
  const [sidebarOpen, setSidebarOpen] = useState(false);

  const setConfig = useCallback((next: AppShellConfig) => {
    setConfigState(next);
  }, []);

  const controller = useMemo<AppShellController>(() => ({
    config,
    sidebarOpen,
    setConfig,
    openSidebar: () => setSidebarOpen(true),
    closeSidebar: () => setSidebarOpen(false),
  }), [config, sidebarOpen, setConfig]);

  return <Context.Provider value={controller}>{children}</Context.Provider>;
}

export function useAppShell(): AppShellController {
  const value = useContext(Context);
  if (!value) throw new Error('useAppShell must be used inside AppShellProvider.');
  return value;
}
