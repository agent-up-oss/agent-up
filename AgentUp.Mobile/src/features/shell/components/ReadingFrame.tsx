import { StyleSheet, View, useWindowDimensions, type ReactNode } from 'react-native';
import { agentUpTheme } from '@agent-up/design-system/native';
import { readingFrameSize } from '../providers/ReadingFrameProvider';

/**
 * The app's available screen. On a phone it is the window. On a wider surface it is a 4:3 pane
 * centered on the canvas, with emptiness around it.
 */
export function ReadingFrame({ children }: { children: ReactNode }) {
  const viewport = useWindowDimensions();
  const frame = readingFrameSize(viewport.width, viewport.height);
  return (
    <View style={styles.stage} testID="reading-frame">
      <View style={[styles.frame, { width: frame.width, height: frame.height }]}>
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
});
