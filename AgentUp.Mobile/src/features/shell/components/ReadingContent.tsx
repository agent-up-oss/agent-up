import { type ReactNode } from 'react';
import { StyleSheet, View } from 'react-native';
import { useReadingFrame } from './ReadingFrame';

/**
 * The page body. On a phone it fills the window. On a wider surface it is the centered 4:3
 * reading column; top and bottom bars stay outside this view so they can span the window.
 */
export function ReadingContent({ children }: { children: ReactNode }) {
  const { layout } = useReadingFrame();
  return (
    <View
      testID="reading-content"
      style={[styles.column, layout.kind === 'column' ? { width: layout.width } : styles.fill]}>
      {children}
    </View>
  );
}

const styles = StyleSheet.create({
  column: {
    flex: 1,
    alignSelf: 'center',
    maxWidth: '100%',
    minWidth: 0,
    width: '100%',
  },
  fill: {
    width: '100%',
  },
});
