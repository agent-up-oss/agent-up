import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';
import {
  browserInterfaceStorage,
  readInterfaceChoice,
  writeInterfaceChoice,
  type InterfaceChoice,
} from '../providers/InterfacePreferenceProvider';

interface InterfaceState {
  choice: InterfaceChoice;
  /** False when this client cannot store the preference, so the UI can say the choice will not survive a reload. */
  persists: boolean;
  choose: (next: InterfaceChoice) => void;
}

const InterfaceContext = createContext<InterfaceState | null>(null);

export function InterfaceProvider({ children }: { children: ReactNode }) {
  const storage = useMemo(() => browserInterfaceStorage(), []);
  const [choice, setChoice] = useState<InterfaceChoice>(() => readInterfaceChoice(storage));
  const [persists, setPersists] = useState(() => storage !== null);

  const choose = useCallback((next: InterfaceChoice) => {
    setChoice(next);
    setPersists(writeInterfaceChoice(storage, next));
  }, [storage]);

  const value = useMemo(() => ({ choice, persists, choose }), [choice, persists, choose]);
  return <InterfaceContext.Provider value={value}>{children}</InterfaceContext.Provider>;
}

export function useInterfaceChoice(): InterfaceState {
  const value = useContext(InterfaceContext);
  if (!value) throw new Error('useInterfaceChoice must be used inside an InterfaceProvider.');
  return value;
}
