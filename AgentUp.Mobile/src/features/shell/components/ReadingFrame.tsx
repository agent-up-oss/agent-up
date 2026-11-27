import { createContext, useContext, useLayoutEffect, useMemo, useState, type ReactNode } from 'react';
import { Platform, StyleSheet, View, useWindowDimensions } from 'react-native';
import { agentUpTheme } from '@agent-up/design-system/native';
import { readingFrameLayout, type ReadingFrameLayout } from '../providers/ReadingFrameProvider';

export type ReadingFrameValue = {
  layout: ReadingFrameLayout;
  /** Column width when the window is wider than 4:3 of its height; otherwise the window width. */
  contentWidth: number;
};

const ReadingFrameContext = createContext<ReadingFrameValue>({
  layout: { kind: 'fill' },
  contentWidth: 0,
});

export function useReadingFrame(): ReadingFrameValue {
  return useContext(ReadingFrameContext);
}

/**
 * The app's available screen. Chrome (top and bottom bars) spans the window. On a wider surface
 * the page body sits in a centered 4:3 column with emptiness beside it.
 */
export function ReadingFrame({ children }: { children: ReactNode }) {
  const viewport = useWindowDimensions();
  // Static HTML has no window. Fill until this client has laid out, so a 0-wide column cannot
  // clip the first paint and hydration is not stuck at zero.
  const [webLaidOut, setWebLaidOut] = useState(false);
  useLayoutEffect(() => {
    if (Platform.OS === 'web') setWebLaidOut(true);
  }, []);
  const measured = Platform.OS !== 'web' || webLaidOut;
  const layout = readingFrameLayout(measured ? viewport.width : 0, measured ? viewport.height : 0);
  const value = useMemo<ReadingFrameValue>(() => ({
    layout,
    contentWidth: layout.kind === 'column' ? layout.width : Math.max(0, viewport.width),
  }), [layout, viewport.width]);
  return (
    <ReadingFrameContext.Provider value={value}>
      <View style={styles.stage} testID="reading-frame">
        {children}
      </View>
    </ReadingFrameContext.Provider>
  );
}

const styles = StyleSheet.create({
  stage: {
    flex: 1,
    width: '100%',
    height: '100%',
    backgroundColor: agentUpTheme.colors.canvas,
  },
});
