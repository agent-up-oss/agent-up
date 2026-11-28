import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import {
  Animated,
  BackHandler,
  PanResponder,
  Platform,
  StyleSheet,
  View,
  type ViewInstance,
  useWindowDimensions,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { auBox } from '@agent-up/design-system/native';
import { SheetLayer, useSheetLayer } from './SheetLayer';
import {
  pageSheetDraggedOffset,
  pageSheetTakesDrag,
  settlePageSheet,
} from '../providers/PageSheetProvider';

const nativeDriver = Platform.OS !== 'web';
const spring = { damping: 30, stiffness: 320, mass: 1, overshootClamping: true, useNativeDriver: nativeDriver } as const;

/**
 * A page opened as a sheet: on a phone, a team member over the team, and an application or a file
 * over that member. It slides up and its head is the handle - drag the head down, or tap its
 * grabber, to close it. The page under the head keeps its own scrolling.
 *
 * `screen` covers the whole screen below the phone's chrome. `docked` opens inside the sheet layer
 * it is rendered in and docks right below that layer's head, so the page it belongs to stays named
 * above it. Each sheet is a sheet layer of its own, so what it opens docks below its own head.
 *
 * `onDismiss` answers a drag down, the Android back button and the grabber's own request: return
 * true to close now, or false to stay open (a file with an unsaved edit asks first, and changes
 * `closeRequest` once it may go). A changed `closeRequest` slides the sheet down; `onClosed` follows.
 */
export function PageSheet({ placement, closeRequest, onDismiss, onClosed, testID, children }: {
  placement: 'screen' | 'docked';
  closeRequest: number;
  onDismiss: () => boolean;
  onClosed: () => void;
  testID?: string;
  children: ReactNode;
}) {
  const window = useWindowDimensions();
  const insets = useSafeAreaInsets();
  const parent = useSheetLayer();
  const topInset = placement === 'screen' ? insets.top : 0;
  const dockedAt = placement === 'docked' ? parent?.dock ?? null : 0;
  const [height, setHeight] = useState(window.height);
  const offset = useRef(new Animated.Value(window.height)).current;
  const sheet = useRef<ViewInstance>(null);
  const live = useRef({
    height,
    top: 0,
    handle: null as number | null,
    closing: false,
    start: 0,
    onClosed,
    onDismiss,
    topInset,
  });
  live.current.height = height;
  live.current.onClosed = onClosed;
  live.current.onDismiss = onDismiss;
  live.current.topInset = topInset;

  const measure = () => sheet.current?.measureInWindow((_x, y) => { live.current.top = y; });

  const settle = (target: 'open' | 'closed', velocity = 0) => {
    if (target === 'closed') live.current.closing = true;
    Animated.spring(offset, { ...spring, toValue: target === 'open' ? 0 : live.current.height, velocity })
      .start(({ finished }) => {
        if (!finished) return;
        if (target === 'closed') live.current.onClosed();
        else measure();
      });
  };
  const dismiss = (velocity = 0) => {
    if (live.current.closing) return;
    settle(live.current.onDismiss() ? 'closed' : 'open', velocity);
  };

  // A docked sheet opens once it knows where the head above it ends.
  const ready = dockedAt !== null;
  const opened = useRef(false);
  useEffect(() => {
    if (!ready || opened.current) return;
    opened.current = true;
    settle('open');
  });

  const firstRequest = useRef(closeRequest);
  useEffect(() => {
    if (closeRequest === firstRequest.current || live.current.closing) return;
    settle('closed');
  }, [closeRequest]);

  // The newest sheet hears the back button first; it answers as a drag down would.
  useEffect(() => {
    const subscription = BackHandler.addEventListener('hardwareBackPress', () => {
      dismiss();
      return true;
    });
    return () => subscription.remove();
  }, []);

  const responder = useMemo(() => {
    // PanResponder sets `y0` only once a responder is granted; before that, where the finger went
    // down is where it is now less how far it has moved.
    const takes = (moveY: number, dx: number, dy: number) =>
      !live.current.closing && pageSheetTakesDrag({
        fromTop: moveY - dy - live.current.top - live.current.topInset,
        handleHeight: live.current.handle,
        dx,
        dy,
      });
    return PanResponder.create({
      onMoveShouldSetPanResponderCapture: (_, gesture) => takes(gesture.moveY, gesture.dx, gesture.dy),
      onMoveShouldSetPanResponder: (_, gesture) => takes(gesture.moveY, gesture.dx, gesture.dy),
      onPanResponderGrant: () => {
        offset.stopAnimation(value => { live.current.start = value; });
      },
      onPanResponderMove: (_, gesture) => {
        offset.setValue(pageSheetDraggedOffset(live.current.start + gesture.dy));
      },
      onPanResponderRelease: (_, gesture) => {
        const moved = pageSheetDraggedOffset(live.current.start + gesture.dy);
        if (settlePageSheet({ offset: moved, velocity: gesture.vy, height: live.current.height }) === 'closed') dismiss(gesture.vy);
        else settle('open', gesture.vy);
      },
      onPanResponderTerminate: () => settle('open'),
      onPanResponderTerminationRequest: () => false,
    });
    // The handlers read everything that changes through `live`.
  }, [offset]);

  // What the sheet covers stays in view behind it, dimmed while the sheet covers it.
  const dim = useMemo(
    () => offset.interpolate({ inputRange: [0, Math.max(1, height)], outputRange: [1, 0], extrapolate: 'clamp' }),
    [offset, height],
  );

  if (!ready) return null;
  return (
    <View
      pointerEvents="box-none"
      style={[StyleSheet.absoluteFill, placement === 'docked' ? { top: dockedAt, overflow: 'hidden' } : null]}
      onLayout={event => setHeight(event.nativeEvent.layout.height)}>
      <Animated.View pointerEvents="none" style={[StyleSheet.absoluteFill, styles.scrim, { opacity: dim }]} />
      <Animated.View
        ref={sheet}
        testID={testID}
        onLayout={measure}
        style={[
          styles.sheet,
          placement === 'docked' ? styles.docked : null,
          { paddingTop: topInset, transform: [{ translateY: offset }] },
        ]}
        {...responder.panHandlers}>
        <SheetLayer topInset={topInset} onDockChange={dock => { live.current.handle = dock; }}>
          {children}
        </SheetLayer>
      </Animated.View>
    </View>
  );
}

const styles = StyleSheet.create({
  scrim: auBox('scrim'),
  sheet: {
    ...auBox('sheet'),
    ...StyleSheet.absoluteFill,
    // Open, the sheet fills its space; its rounded top and hairline show while it is dragged away.
    borderBottomWidth: 0,
    borderBottomLeftRadius: 0,
    borderBottomRightRadius: 0,
    overflow: 'hidden',
  },
  // A sheet docked under a head sits flush against it: its hairline is the edge between them.
  docked: {
    borderTopLeftRadius: 0,
    borderTopRightRadius: 0,
    borderLeftWidth: 0,
    borderRightWidth: 0,
  },
});
