import {
  createContext,
  Fragment,
  useContext,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { StyleSheet, View, type ViewInstance } from 'react-native';
import type { WindowSpan } from '../providers/SheetLayerProvider';

export type SheetLayerHandle = {
  /** Draws `node` in the layer under `key`, replacing what that key drew before. */
  mount: (key: string, node: ReactNode) => void;
  unmount: (key: string) => void;
  /** The layer's extent in window coordinates. */
  measure: (done: (span: WindowSpan) => void) => void;
  /** How far the phone's own chrome reaches down the window; no sheet opens under it. */
  topInset: number;
  /** Whether a sheet in the layer is open over everything, which the surface's own drag then leaves alone. */
  setExpanded: (key: string, expanded: boolean) => void;
};

const SheetLayerContext = createContext<SheetLayerHandle | null>(null);

/** The layer a sheet opens in, or null when nothing above the page offers one. */
export function useSheetLayer(): SheetLayerHandle | null {
  return useContext(SheetLayerContext);
}

/**
 * A surface whose sheets open over all of it. A page inside it still decides where its sheet rests,
 * but the sheet is drawn here, above the surface's head and tab bar, so drawn up it covers them
 * instead of stopping at the page's edges. Each sheet is on top of whatever it opened from: a sheet
 * opened inside a sheet is drawn in that sheet's own layer.
 */
export function SheetLayer({ children, topInset = 0, onExpandedChange }: {
  children: ReactNode;
  topInset?: number;
  onExpandedChange?: (expanded: boolean) => void;
}) {
  const [nodes, setNodes] = useState<Record<string, ReactNode>>({});
  const frame = useRef<ViewInstance>(null);
  const expanded = useRef(new Set<string>());
  const report = useRef(onExpandedChange);
  report.current = onExpandedChange;

  const handle = useMemo<SheetLayerHandle>(() => {
    const setExpanded = (key: string, on: boolean) => {
      const before = expanded.current.size > 0;
      if (on) expanded.current.add(key);
      else expanded.current.delete(key);
      const after = expanded.current.size > 0;
      if (before !== after) report.current?.(after);
    };
    return {
      mount: (key, node) => setNodes(current => (current[key] === node ? current : { ...current, [key]: node })),
      unmount: key => {
        setExpanded(key, false);
        setNodes(current => {
          if (!(key in current)) return current;
          const next = { ...current };
          delete next[key];
          return next;
        });
      },
      measure: done => frame.current?.measureInWindow((_x, y, _width, height) => done({ y, height })),
      topInset,
      setExpanded,
    };
  }, [topInset]);

  return (
    <SheetLayerContext.Provider value={handle}>
      <View ref={frame} collapsable={false} style={styles.surface}>
        {children}
        <View pointerEvents="box-none" style={StyleSheet.absoluteFill}>
          {Object.entries(nodes).map(([key, node]) => <Fragment key={key}>{node}</Fragment>)}
        </View>
      </View>
    </SheetLayerContext.Provider>
  );
}

const styles = StyleSheet.create({
  surface: { flex: 1 },
});
