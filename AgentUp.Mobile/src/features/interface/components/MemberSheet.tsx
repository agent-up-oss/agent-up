import { useEffect, useMemo, useRef, type ReactNode } from 'react';
import { Animated, BackHandler, PanResponder, Platform, StyleSheet, View, type ViewInstance, useWindowDimensions } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { auBox } from '@agent-up/design-system/native';
import { SheetLayer } from '@/features/shell/components/SheetLayer';
import {
  memberSheetDraggedOffset,
  memberSheetTakesDrag,
  settleMemberSheet,
} from '../providers/MemberSheetProvider';

const nativeDriver = Platform.OS !== 'web';
const spring = { damping: 30, stiffness: 320, mass: 1, overshootClamping: true, useNativeDriver: nativeDriver } as const;

/**
 * A team member, opened as a sheet over the team. It slides up to cover the whole screen and its
 * head is the handle: drag it down, or tap the grabber, to return to the team. The sheet is also a
 * sheet layer, so a sheet the member's page opens - the Git change list - opens over the member's
 * head and tab bar rather than stopping at the page, and a drag down closes that inner sheet first.
 *
 * `closeRequest` changes when something else asks it to close, such as the grabber; the sheet
 * slides down and then calls `onClosed`. `onBack` answers the Android back button.
 */
export function MemberSheet({ closeRequest, onClosed, onBack, children }: {
  closeRequest: number;
  onClosed: () => void;
  onBack: () => void;
  children: ReactNode;
}) {
  const { height } = useWindowDimensions();
  const insets = useSafeAreaInsets();
  const offset = useRef(new Animated.Value(height)).current;
  const frame = useRef<ViewInstance>(null);
  const live = useRef({ height, top: 0, innerExpanded: false, closing: false, start: 0, onClosed, onBack, insetTop: insets.top });
  live.current.height = height;
  live.current.onClosed = onClosed;
  live.current.onBack = onBack;
  live.current.insetTop = insets.top;

  const measure = () => frame.current?.measureInWindow((_x, y) => { live.current.top = y; });

  const settle = (target: 'open' | 'closed', velocity = 0) => {
    if (target === 'closed') live.current.closing = true;
    Animated.spring(offset, {
      ...spring,
      toValue: target === 'open' ? 0 : live.current.height,
      velocity,
    }).start(({ finished }) => {
      if (!finished) return;
      if (target === 'closed') live.current.onClosed();
      else measure();
    });
  };

  // It opens by sliding up from below the screen.
  useEffect(() => {
    settle('open');
    // Opening once is the point; later renders must not restart it.
  }, []);

  const firstRequest = useRef(closeRequest);
  useEffect(() => {
    if (closeRequest === firstRequest.current || live.current.closing) return;
    settle('closed');
  }, [closeRequest]);

  useEffect(() => {
    const subscription = BackHandler.addEventListener('hardwareBackPress', () => {
      live.current.onBack();
      return true;
    });
    return () => subscription.remove();
  }, []);

  const responder = useMemo(() => {
    // PanResponder sets `y0` only once a responder is granted; before that, where the finger went
    // down is where it is now less how far it has moved.
    const takes = (moveY: number, dx: number, dy: number) =>
      !live.current.closing && memberSheetTakesDrag({
        fromTop: moveY - dy - live.current.top - live.current.insetTop,
        dx,
        dy,
        innerExpanded: live.current.innerExpanded,
      });
    return PanResponder.create({
      onMoveShouldSetPanResponderCapture: (_, gesture) => takes(gesture.moveY, gesture.dx, gesture.dy),
      onMoveShouldSetPanResponder: (_, gesture) => takes(gesture.moveY, gesture.dx, gesture.dy),
      onPanResponderGrant: () => {
        offset.stopAnimation(value => { live.current.start = value; });
      },
      onPanResponderMove: (_, gesture) => {
        offset.setValue(memberSheetDraggedOffset(live.current.start + gesture.dy));
      },
      onPanResponderRelease: (_, gesture) => {
        const moved = memberSheetDraggedOffset(live.current.start + gesture.dy);
        settle(settleMemberSheet({ offset: moved, velocity: gesture.vy, height: live.current.height }), gesture.vy);
      },
      onPanResponderTerminate: () => settle('open'),
      onPanResponderTerminationRequest: () => false,
    });
    // The handlers read everything that changes through `live`.
  }, [offset]);

  // The team stays in view behind the sheet, dimmed while the sheet covers it.
  const dim = useMemo(() => offset.interpolate({ inputRange: [0, Math.max(1, height)], outputRange: [1, 0], extrapolate: 'clamp' }), [offset, height]);

  return (
    <View pointerEvents="box-none" style={StyleSheet.absoluteFill}>
      <Animated.View pointerEvents="none" style={[StyleSheet.absoluteFill, styles.scrim, { opacity: dim }]} />
      <Animated.View
        ref={frame}
        testID="member-sheet"
        onLayout={measure}
        style={[styles.sheet, { paddingTop: insets.top, transform: [{ translateY: offset }] }]}
        {...responder.panHandlers}>
        <SheetLayer topInset={insets.top} onExpandedChange={expanded => { live.current.innerExpanded = expanded; }}>
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
    // Open, the sheet is the whole screen; its rounded top and hairline show while it is dragged
    // away from the team.
    borderBottomWidth: 0,
    borderBottomLeftRadius: 0,
    borderBottomRightRadius: 0,
    overflow: 'hidden',
  },
});
