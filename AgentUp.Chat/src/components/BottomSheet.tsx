import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Animated, Easing, Modal, PanResponder, Platform, Pressable, ScrollView, StyleSheet, Text, View, useWindowDimensions } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { auBox, auText } from '@agent-up/design-system/native';
import { useReducedMotion } from './Motion';
import type { SheetStatusTone } from '../providers/SheetProvider';
import { detentHeight, draggedHeight, settleSheet, sheetDetents, sheetTakesDrag, type SheetDetent, type SheetRest } from '../providers/SheetProvider';

const nativeDriver = Platform.OS !== 'web';
const closeMs = 200;

/**
 * A sheet that rises from the bottom over a scrim to show one thing in full. It opens at half
 * height; dragging up - or tapping the grabber - takes it to full height, and dragging down, the
 * close button, the scrim or the system back gesture closes it. The host mounts it while something
 * is open and unmounts it from `onClose`, which runs once the sheet has slid away.
 */
export function BottomSheet({ title, status, onClose, children, testID = 'agent-sheet' }: {
  title: string;
  /** How the thing shown went, under the title, so the header reads as one unit. */
  status?: { tone: SheetStatusTone; text: string };
  onClose: () => void;
  children: ReactNode;
  testID?: string;
}) {
  const window = useWindowDimensions();
  const insets = useSafeAreaInsets();
  const reduced = useReducedMotion();
  const detents = useMemo(() => sheetDetents(window.height, insets.top), [window.height, insets.top]);
  const shown = useRef(new Animated.Value(0)).current;
  const [detent, setDetent] = useState<SheetDetent>('half');
  // The gesture handlers are made once, so they read the current values through refs.
  const live = useRef({ detent: 'half' as SheetDetent, scrollTop: 0, start: 0, detents, closing: false });
  live.current.detents = detents;

  const rest = useCallback((target: SheetRest, velocity = 0) => {
    if (target === 'closed') {
      if (live.current.closing) return;
      live.current.closing = true;
      Animated.timing(shown, { toValue: 0, duration: reduced ? 0 : closeMs, easing: Easing.in(Easing.cubic), useNativeDriver: nativeDriver })
        .start(() => onClose());
      return;
    }
    live.current.detent = target;
    setDetent(target);
    const toValue = detentHeight(target, live.current.detents);
    if (reduced) { shown.setValue(toValue); return; }
    // The gesture reports downward speed; the sheet's height grows upward.
    Animated.spring(shown, { toValue, velocity: -velocity, damping: 30, stiffness: 320, mass: 1, overshootClamping: true, useNativeDriver: nativeDriver }).start();
  }, [onClose, reduced, shown]);

  useEffect(() => { rest('half'); }, [rest]);

  const responder = useMemo(() => {
    const takes = (dx: number, dy: number) => !live.current.closing && sheetTakesDrag({ detent: live.current.detent, scrollTop: live.current.scrollTop, dx, dy });
    return PanResponder.create({
      onMoveShouldSetPanResponderCapture: (_, gesture) => takes(gesture.dx, gesture.dy),
      onMoveShouldSetPanResponder: (_, gesture) => takes(gesture.dx, gesture.dy),
      onPanResponderGrant: () => {
        shown.stopAnimation();
        live.current.start = detentHeight(live.current.detent, live.current.detents);
      },
      onPanResponderMove: (_, gesture) => shown.setValue(draggedHeight(live.current.start, gesture.dy, live.current.detents)),
      onPanResponderRelease: (_, gesture) => {
        const height = draggedHeight(live.current.start, gesture.dy, live.current.detents);
        rest(settleSheet({ height, velocity: gesture.vy, from: live.current.detent, detents: live.current.detents }), gesture.vy);
      },
      onPanResponderTerminate: () => rest(live.current.detent),
      onPanResponderTerminationRequest: () => false,
    });
  }, [rest, shown]);

  const translateY = Animated.subtract(detents.full, shown);
  const scrim = shown.interpolate({ inputRange: [0, Math.max(1, detents.half)], outputRange: [0, 1], extrapolate: 'clamp' });
  const expanded = detent === 'full';

  return <Modal visible transparent animationType="none" statusBarTranslucent onRequestClose={() => rest('closed')}>
    <View style={styles.root}>
      <Animated.View style={[StyleSheet.absoluteFill, { opacity: scrim }]}>
        <Pressable testID={`${testID}-scrim`} accessibilityLabel="Close" style={[StyleSheet.absoluteFill, styles.scrim]} onPress={() => rest('closed')} />
      </Animated.View>
      <Animated.View
        testID={testID}
        accessibilityViewIsModal
        style={[styles.sheet, { height: detents.full, transform: [{ translateY }] }]}
        {...responder.panHandlers}>
        <Pressable
          testID={`${testID}-grabber`}
          accessibilityRole="button"
          accessibilityLabel={expanded ? 'Show less' : 'Show more'}
          onPress={() => rest(expanded ? 'half' : 'full')}
          style={styles.grabberArea}>
          <View style={styles.grabber} />
        </Pressable>
        <View style={styles.header}>
          <Pressable testID={`${testID}-close`} accessibilityRole="button" accessibilityLabel="Close" onPress={() => rest('closed')} style={styles.close}>
            <Text style={styles.closeGlyph}>✕</Text>
          </Pressable>
          <View style={styles.heading}>
            <Text accessibilityRole="header" style={styles.title} numberOfLines={1}>{title}</Text>
            {status ? <Text testID={`${testID}-status`} style={[styles.status, status.tone === 'failed' && styles.statusFailed, status.tone === 'running' && styles.statusRunning]} numberOfLines={1}>{status.text}</Text> : null}
          </View>
        </View>
        <ScrollView
          testID={`${testID}-content`}
          style={styles.content}
          contentContainerStyle={[styles.contentInner, { paddingBottom: insets.bottom + 24 }]}
          scrollEnabled={expanded}
          scrollEventThrottle={16}
          onScroll={event => { live.current.scrollTop = event.nativeEvent.contentOffset.y; }}>
          {children}
        </ScrollView>
      </Animated.View>
    </View>
  </Modal>;
}

const styles = StyleSheet.create({
  root: { flex: 1, justifyContent: 'flex-end' },
  scrim: auBox('scrim'),
  sheet: {
    ...auBox('sheet'),
    // A sheet is open at the bottom: rounded on top, flush with the screen edge below.
    borderBottomLeftRadius: 0,
    borderBottomRightRadius: 0,
    width: '100%',
    maxWidth: 720,
    alignSelf: 'center',
    overflow: 'hidden',
  },
  grabberArea: { ...auBox('sheetGrabberRow'), alignItems: 'center' },
  grabber: auBox('sheetGrabber'),
  header: { ...auBox('sheetHeader'), flexDirection: 'row', alignItems: 'center', gap: 12 },
  close: { ...auBox('sheetClose'), alignItems: 'center', justifyContent: 'center' },
  closeGlyph: auText('sheetCloseGlyph'),
  heading: { ...auBox('sheetHeading'), flex: 1, gap: 2 },
  title: auText('sheetTitle'),
  status: auText('sheetStatus'),
  statusFailed: auText('sheetStatusFailed'),
  statusRunning: auText('sheetStatusRunning'),
  content: { flex: 1 },
  contentInner: { ...auBox('sheetContent'), gap: 16 },
});
