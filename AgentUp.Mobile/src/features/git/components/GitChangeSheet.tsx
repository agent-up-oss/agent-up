import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Animated, PanResponder, Platform, Pressable, ScrollView, StyleSheet, Text, View, type ViewInstance } from 'react-native';
import { auBox, auText } from '@agent-up/design-system/native';
import { useSheetLayer } from '@/features/shell/components/SheetLayer';
import { sheetLayerFrame, type SheetLayerFrame } from '@/features/shell/providers/SheetLayerProvider';
import {
  gitChangeSheetDetents,
  gitChangeSheetDraggedHeight,
  gitChangeSheetHeight,
  gitChangeSheetLayerDetents,
  gitChangeSheetLift,
  gitChangeSheetTakesDrag,
  settleGitChangeSheet,
  type GitChangeSheetDetent,
} from '../providers/GitChangeSheetProvider';

const nativeDriver = Platform.OS !== 'web';
let sheetKeys = 0;

/**
 * The change list, resting in a sheet on the bottom edge of the Git page. It peeks at about a
 * quarter of the page with its first rows live - a checkbox there works without opening the sheet -
 * and a drag up or a tap on the grabber opens it to the whole page. A drag down brings it back to
 * the peek; it never closes. `pageHeight` is the height of the page it rests on.
 *
 * Inside a sheet layer - a team member's sheet - the sheet is drawn in that layer rather than in the
 * page, so drawn up it covers the member's head and tab bar too. Elsewhere the page clips it.
 */
export function GitChangeSheet({ pageHeight, count, children }: {
  pageHeight: number;
  count: string;
  children: ReactNode;
}) {
  const layer = useSheetLayer();
  const [key] = useState(() => `git-change-sheet-${++sheetKeys}`);
  const anchor = useRef<ViewInstance>(null);
  const [frame, setFrame] = useState<SheetLayerFrame | null>(null);
  const layered = layer !== null && frame !== null;
  const detents = useMemo(
    () => (layer && frame ? gitChangeSheetLayerDetents(pageHeight, frame) : gitChangeSheetDetents(pageHeight)),
    [layer, frame, pageHeight],
  );
  const inset = layer && frame ? frame.bottomInset : 0;
  const shown = useRef(new Animated.Value(detents.peek)).current;
  const [detent, setDetent] = useState<GitChangeSheetDetent>('peek');
  // The gesture handlers are made once, so they read the current values through refs.
  const live = useRef({ detent: 'peek' as GitChangeSheetDetent, scrollTop: 0, start: 0, height: detents.peek, grant: 0, moved: false, detents });
  live.current.detents = detents;

  // The height on screen, so a drag that interrupts a spring starts where the sheet is, not where
  // it was heading.
  useEffect(() => {
    const id = shown.addListener(({ value }) => { live.current.height = value; });
    return () => shown.removeListener(id);
  }, [shown]);

  const rest = useCallback((target: GitChangeSheetDetent, velocity = 0) => {
    live.current.detent = target;
    setDetent(target);
    layer?.setExpanded(key, target === 'full');
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
  }, [shown, layer, key]);

  // A rotation, a keyboard or a resized window moves the detents; the sheet keeps its detent.
  useEffect(() => {
    const height = gitChangeSheetHeight(live.current.detent, detents);
    live.current.height = height;
    shown.setValue(height);
  }, [detents, shown]);

  const makeResponder = useCallback((fromHeader: boolean) => {
    const takes = (dx: number, dy: number) =>
      gitChangeSheetTakesDrag({ detent: live.current.detent, scrollTop: fromHeader ? 0 : live.current.scrollTop, dx, dy });
    return PanResponder.create({
      onMoveShouldSetPanResponderCapture: (_, gesture) => takes(gesture.dx, gesture.dy),
      onMoveShouldSetPanResponder: (_, gesture) => takes(gesture.dx, gesture.dy),
      onPanResponderGrant: () => {
        // A native-driven spring reports where it stopped through the callback; until then the
        // last height the listener saw is the closest to what is on screen.
        // It is ignored once this drag has moved, or once a later drag has begun, so a late
        // callback never moves a baseline the finger is already following.
        const grant = ++live.current.grant;
        live.current.moved = false;
        live.current.start = live.current.height;
        shown.stopAnimation(value => {
          if (grant === live.current.grant && !live.current.moved) live.current.start = value;
        });
      },
      onPanResponderMove: (_, gesture) => {
        live.current.moved = true;
        shown.setValue(gitChangeSheetDraggedHeight(live.current.start, gesture.dy, live.current.detents));
      },
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

  // Where the page sits in the layer: the sheet rests on the page's bottom edge, which is above the
  // layer's own by whatever is under the page, such as a tab bar.
  const measure = useCallback(() => {
    if (!layer) return;
    layer.measure(span => anchor.current?.measureInWindow((_x, y, _width, height) => {
      const next = sheetLayerFrame({ layer: span, page: { y, height }, topInset: layer.topInset });
      setFrame(current => (current && current.height === next.height && current.bottomInset === next.bottomInset ? current : next));
    }));
  }, [layer]);

  const expanded = detent === 'full';
  // While it rests at the peek, a layered sheet's bottom edge stays above the tab bar; it reaches the
  // layer's bottom as it opens. Its clip rises by that lift and the sheet sinks by it, so the sheet
  // stays where the finger put it while its hidden edge moves.
  const lift = useMemo(() => {
    const range = gitChangeSheetLift(detents, inset);
    return shown.interpolate({ ...range, extrapolate: 'clamp' });
  }, [detents, inset, shown]);
  const raise = useMemo(() => Animated.multiply(lift, -1), [lift]);
  const translateY = useMemo(
    () => Animated.add(Animated.subtract(detents.full, shown), lift),
    [detents.full, shown, lift],
  );

  const sheet = (
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

  // The layer draws what this sheet is on every render, so its rows stay as live as they are here.
  useLayoutEffect(() => {
    if (layer && frame) layer.mount(key, <Animated.View pointerEvents="box-none" style={[styles.lift, { transform: [{ translateY: raise }] }]}>{sheet}</Animated.View>);
  });
  useEffect(() => {
    if (!layer || !layered) return;
    return () => layer.unmount(key);
  }, [layered, layer, key]);

  if (!layer) return sheet;
  // In a layer the page keeps only an anchor that says where it is.
  return <View ref={anchor} collapsable={false} pointerEvents="none" style={StyleSheet.absoluteFill} onLayout={measure} />;
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
  // The layered sheet's clip: the whole layer, lifted while the sheet rests at its peek.
  lift: { ...StyleSheet.absoluteFill, overflow: 'hidden' },
  list: { flex: 1 },
  listContent: { ...auBox('gitChangeSheetList'), flexGrow: 1 },
});
