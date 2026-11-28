import {
  createContext,
  Fragment,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { StyleSheet, View, type ViewInstance } from 'react-native';
import { sheetDockOffset, type WindowSpan } from '../providers/SheetLayerProvider';

export type SheetLayerHandle = {
  /** Draws `node` in the layer under `key`, replacing what that key drew before. */
  mount: (key: string, node: ReactNode) => void;
  unmount: (key: string) => void;
  /** The layer's extent in window coordinates. */
  measure: (done: (span: WindowSpan) => void) => void;
  /** How far the phone's own chrome reaches down the window; no sheet opens under it. */
  topInset: number;
  /**
   * How far below the layer's top the head above the page ends, once a `SheetDockMark` under that
   * head has been measured. A sheet opened in the page docks there, so the head stays in view.
   */
  dock: number | null;
  /** Called by the `SheetDockMark` under the head when it is laid out. */
  markDock: (mark: ViewInstance | null) => void;
};

const SheetLayerContext = createContext<SheetLayerHandle | null>(null);

/** The layer a sheet opens in, or null when nothing above the page offers one. */
export function useSheetLayer(): SheetLayerHandle | null {
  return useContext(SheetLayerContext);
}

/**
 * A surface whose sheets open over its page and tab bar. A page inside it still decides where its
 * sheet rests, but the sheet is drawn here, above the surface's tab bar, and docks right below the
 * head - the surface's title bar - so what the sheet belongs to stays in view. A sheet opened inside
 * a sheet is drawn in that sheet's own layer.
 *
 * `onDockChange` reports where the head ends, which is how far down the surface its own handle reaches.
 */
export function SheetLayer({ children, topInset = 0, onDockChange }: {
  children: ReactNode;
  topInset?: number;
  onDockChange?: (dock: number) => void;
}) {
  const [nodes, setNodes] = useState<Record<string, ReactNode>>({});
  const [dock, setDock] = useState<number | null>(null);
  const frame = useRef<ViewInstance>(null);
  const report = useRef(onDockChange);
  report.current = onDockChange;

  const markDock = useCallback((mark: ViewInstance | null) => {
    if (!mark) return;
    frame.current?.measureInWindow((_x, layerY, _width, layerHeight) => {
      mark.measureInWindow((_mx, markY) => {
        const offset = sheetDockOffset({ y: layerY, height: layerHeight }, { y: markY, height: 0 });
        setDock(current => (current === offset ? current : offset));
        report.current?.(offset);
      });
    });
  }, []);

  const handle = useMemo<SheetLayerHandle>(() => ({
    mount: (key, node) => setNodes(current => (current[key] === node ? current : { ...current, [key]: node })),
    unmount: key => setNodes(current => {
      if (!(key in current)) return current;
      const next = { ...current };
      delete next[key];
      return next;
    }),
    measure: done => frame.current?.measureInWindow((_x, y, _width, height) => done({ y, height })),
    topInset,
    dock,
    markDock,
  }), [topInset, dock, markDock]);

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

/**
 * Marks the bottom of a head: a sheet opened under it docks here. A scene places it in its
 * `sheetDock` slot, right under the head.
 */
export function SheetDockMark() {
  const layer = useSheetLayer();
  const mark = useRef<ViewInstance>(null);
  const markDock = layer?.markDock;
  const measure = useCallback(() => markDock?.(mark.current), [markDock]);
  useEffect(() => { measure(); }, [measure]);
  return <View ref={mark} collapsable={false} pointerEvents="none" style={styles.mark} onLayout={measure} />;
}

const styles = StyleSheet.create({
  surface: { flex: 1 },
  mark: { height: 0, alignSelf: 'stretch' },
});
