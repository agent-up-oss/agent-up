import { type ReactNode } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { auBox, auText } from '@agent-up/design-system/native';
import { SheetDockMark } from './SheetLayer';

/**
 * The head of a sheet opened under a page - a transcript item, a file's changes: the grabber, then
 * the title, an optional line under it, and whatever the sheet puts on the right. Dragging it down
 * or tapping the grabber closes the sheet; the mark under it is where a sheet opened inside docks.
 */
export function SheetHead({ title, subtitle, subtitleTone, onClose, closeLabel = 'Close', leading, trailing, testID }: {
  title: string;
  subtitle?: string;
  subtitleTone?: 'failed' | 'running';
  onClose: () => void;
  closeLabel?: string;
  leading?: ReactNode;
  trailing?: ReactNode;
  testID?: string;
}) {
  return (
    <View testID={testID}>
      <Pressable
        testID={testID ? `${testID}-grabber` : undefined}
        accessibilityRole="button"
        accessibilityLabel={closeLabel}
        onPress={onClose}
        style={styles.grabberRow}>
        <View style={styles.grabber} />
      </Pressable>
      <View style={styles.head}>
        {leading}
        <View style={styles.heading}>
          <Text accessibilityRole="header" numberOfLines={1} style={styles.title}>{title}</Text>
          {subtitle ? (
            <Text
              numberOfLines={1}
              style={[styles.subtitle, subtitleTone === 'failed' && styles.failed, subtitleTone === 'running' && styles.running]}>
              {subtitle}
            </Text>
          ) : null}
        </View>
        {trailing}
      </View>
      <SheetDockMark />
    </View>
  );
}

const styles = StyleSheet.create({
  grabberRow: { ...auBox('sheetGrabberRow'), alignItems: 'center' },
  grabber: auBox('sheetGrabber'),
  head: { ...auBox('sheetHeader'), flexDirection: 'row', alignItems: 'center', gap: 10 },
  heading: { ...auBox('sheetHeading'), flex: 1, minWidth: 0, gap: 2 },
  title: auText('sheetTitle'),
  subtitle: auText('sheetStatus'),
  failed: auText('sheetStatusFailed'),
  running: auText('sheetStatusRunning'),
});
