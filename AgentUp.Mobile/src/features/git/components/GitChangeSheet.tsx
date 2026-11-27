import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Animated, PanResponder, Platform, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { auBox, auText } from '@agent-up/design-system/native';
import {
  gitChangeSheetDetents,
  gitChangeSheetDraggedHeight,
  gitChangeSheetHeight,
  gitChangeSheetTakesDrag,
  settleGitChangeSheet,
  type GitChangeSheetDetent,
} from '../providers/GitChangeSheetProvider';

const nativeDriver = Platform.OS !== 'web';

/**
 * The change list, resting in a sheet on the bottom edge of the Git page. It peeks at about a
 * quarter of the page with its first rows live - a checkbox there works without opening the sheet -
 * and a drag up or a tap on the grabber opens it to the whole page. A drag down brings it back to
 * the peek; it never closes. `pageHeight` is the height of the page it rests on; the page clips it.
 */
export function GitChangeSheet({ pageHeight, count, children }: {
  pageHeight: number;
  count: string;
  children: ReactNode;
}) {
  const detents = useMemo(() => gitChangeSheetDetents(pageHeight), [pageHeight]);
  const shown = useRef(new Animated.Value(detents.peek)).current;
  const [detent, setDetent] = useState<GitChangeSheetDetent>('peek');
  // The gesture handlers are made once, so they read the current values through refs.
  const live = useRef({ detent: 'peek' as GitChangeSheetDetent, scrollTop: 0, start: 0, detents });
  live.current.detents = detents;

  const rest = useCallback((target: GitChangeSheetDetent, velocity = 0) => {
    live.current.detent = target;
    setDetent(target);
    // The gesture reports downward speed; the sheet's height grows upward.
    Animated.spring(shown, {
      toValue: gitChangeSheetHeight(target, live.current.detents),
      velocity: -velocity,
      damping: 30,
      stiffness: 320,
      mass: 1,
      overshootClamping: true,
      useNativeDriver: nativeDriver,
    }).start();
  }, [shown]);

  // A rotation, a keyboard or a resized window moves the detents; the sheet keeps its detent.
  useEffect(() => {
    shown.setValue(gitChangeSheetHeight(live.current.detent, detents));
  }, [detents, shown]);

  const makeResponder = useCallback((fromHeader: boolean) => {
    const takes = (dx: number, dy: number) =>
      gitChangeSheetTakesDrag({ detent: live.current.detent, scrollTop: fromHeader ? 0 : live.current.scrollTop, dx, dy });
    return PanResponder.create({
      onMoveShouldSetPanResponderCapture: (_, gesture) => takes(gesture.dx, gesture.dy),
      onMoveShouldSetPanResponder: (_, gesture) => takes(gesture.dx, gesture.dy),
      onPanResponderGrant: () => {
        shown.stopAnimation();
        live.current.start = gitChangeSheetHeight(live.current.detent, live.current.detents);
      },
      onPanResponderMove: (_, gesture) =>
        shown.setValue(gitChangeSheetDraggedHeight(live.current.start, gesture.dy, live.current.detents)),
      onPanResponderRelease: (_, gesture) => {
        const height = gitChangeSheetDraggedHeight(live.current.start, gesture.dy, live.current.detents);
        rest(settleGitChangeSheet({ height, velocity: gesture.vy, detents: live.current.detents }), gesture.vy);
      },
      onPanResponderTerminate: () => rest(live.current.detent),
      onPanResponderTerminationRequest: () => false,
    });
  }, [rest, shown]);
  const responder = useMemo(() => makeResponder(false), [makeResponder]);
  const headerResponder = useMemo(() => makeResponder(true), [makeResponder]);

  const expanded = detent === 'full';
  const translateY = Animated.subtract(detents.full, shown);

  return (
    <Animated.View
      testID="git-change-sheet"
      style={[styles.sheet, { height: detents.full, transform: [{ translateY }] }]}
      {...responder.panHandlers}>
      <View {...headerResponder.panHandlers}>
        <Pressable
          testID="git-change-sheet-grabber"
          accessibilityRole="button"
          accessibilityLabel={expanded ? 'Show fewer changes' : 'Show all changes'}
          onPress={() => rest(expanded ? 'peek' : 'full')}
          style={styles.grabberRow}>
          <View style={styles.grabber} />
        </Pressable>
        <View style={styles.head}>
          <Text accessibilityRole="header" style={styles.title}>Changes</Text>
          <Text style={styles.count}>{count}</Text>
        </View>
      </View>
      <ScrollView
        testID="git-change-sheet-list"
        style={styles.list}
        contentContainerStyle={styles.listContent}
        scrollEnabled={expanded}
        scrollEventThrottle={16}
        keyboardShouldPersistTaps="handled"
        onScroll={event => { live.current.scrollTop = event.nativeEvent.contentOffset.y; }}>
        {children}
      </ScrollView>
    </Animated.View>
  );
}

const styles = StyleSheet.create({
  sheet: {
    ...auBox('sheet', 'gitChangeSheet'),
    // The sheet rests on the bottom edge: rounded on top, flush below.
    borderBottomLeftRadius: 0,
    borderBottomRightRadius: 0,
    position: 'absolute',
    left: 0,
    right: 0,
    bottom: 0,
    overflow: 'hidden',
  },
  grabberRow: { ...auBox('sheetGrabberRow'), alignItems: 'center' },
  grabber: auBox('sheetGrabber'),
  head: { ...auBox('gitChangeSheetHead'), flexDirection: 'row', alignItems: 'baseline', justifyContent: 'space-between' },
  title: auText('gitChangeSheetTitle'),
  count: auText('gitChangeSheetCount'),
  list: { flex: 1 },
  listContent: { ...auBox('gitChangeSheetList'), flexGrow: 1 },
});
