import { useLayoutEffect, useState, type ReactNode } from 'react';
import { Platform, StyleSheet, View, useWindowDimensions } from 'react-native';
import { agentUpTheme } from '@agent-up/design-system/native';
import { readingFrameLayout } from '../providers/ReadingFrameProvider';

/**
 * The app's available screen. On a phone it is the window. On a wider surface it is a 4:3 pane
 * centered on the canvas, with emptiness around it.
 */
export function ReadingFrame({ children }: { children: ReactNode }) {
  const viewport = useWindowDimensions();
  // Static HTML has no window. Fill the parent (the CSS 4:3 #root) until this client has laid out,
  // so a 0×0 overflow box cannot clip the first paint and hydration is not stuck at zero.
  const [webLaidOut, setWebLaidOut] = useState(false);
  useLayoutEffect(() => {
    if (Platform.OS === 'web') setWebLaidOut(true);
  }, []);
  const measured = Platform.OS !== 'web' || webLaidOut;
  const box = readingFrameLayout(measured ? viewport.width : 0, measured ? viewport.height : 0);
  return (
    <View style={styles.stage} testID="reading-frame">
      <View style={[styles.frame, box.kind === 'fill' ? styles.fill : { width: box.width, height: box.height }]}>
        {children}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  stage: {
    flex: 1,
    width: '100%',
    height: '100%',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: agentUpTheme.colors.canvas,
  },
  frame: {
    position: 'relative',
    overflow: 'hidden',
    flexGrow: 0,
    flexShrink: 0,
    backgroundColor: agentUpTheme.colors.canvas,
  },
  fill: {
    width: '100%',
    height: '100%',
    flex: 1,
  },
});
