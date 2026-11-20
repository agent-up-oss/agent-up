import { useEffect, useRef, useState, useSyncExternalStore, type ReactNode } from 'react';
import { AccessibilityInfo, Animated, Easing, Platform, View } from 'react-native';
import { arriveMs, pacedStart, revealMs, revealStep } from '../providers/MotionProvider';

const nativeDriver = Platform.OS !== 'web';

let reducedMotion = false;
let watching = false;
const listeners = new Set<() => void>();

function watchReducedMotion() {
  if (watching) return;
  watching = true;
  const set = (value: boolean) => { reducedMotion = value; listeners.forEach(listener => listener()); };
  void AccessibilityInfo.isReduceMotionEnabled().then(set).catch(() => undefined);
  AccessibilityInfo.addEventListener('reduceMotionChanged', set);
}

/** Whether the person asked their system for reduced motion; motion then lands at once. One subscription serves every caller. */
export function useReducedMotion(): boolean {
  return useSyncExternalStore(listener => {
    watchReducedMotion();
    listeners.add(listener);
    return () => { listeners.delete(listener); };
  }, () => reducedMotion, () => false);
}

/**
 * Opens and closes its children by growing and shrinking its height while they fade, so what
 * sits below glides instead of jumping. Closed, nothing is rendered; settled open, the children
 * take their natural height and nothing is animated.
 */
export function Reveal({ open, children }: { open: boolean; children: ReactNode }) {
  const reduced = useReducedMotion();
  const progress = useRef(new Animated.Value(open ? 1 : 0)).current;
  const [phase, setPhase] = useState<'open' | 'closed' | 'moving'>(open ? 'open' : 'closed');
  const [height, setHeight] = useState(0);
  const first = useRef(true);
  useEffect(() => {
    if (first.current) { first.current = false; return; }
    if (reduced) { progress.setValue(open ? 1 : 0); setPhase(open ? 'open' : 'closed'); return; }
    setPhase('moving');
    const animation = Animated.timing(progress, { toValue: open ? 1 : 0, duration: revealMs, easing: Easing.out(Easing.cubic), useNativeDriver: false });
    animation.start(({ finished }) => { if (finished) setPhase(open ? 'open' : 'closed'); });
    return () => animation.stop();
  }, [open, reduced, progress]);
  if (phase === 'closed') return null;
  const content = <View onLayout={event => setHeight(event.nativeEvent.layout.height)}>{children}</View>;
  if (phase === 'open') return content;
  return <Animated.View style={{ overflow: 'hidden', opacity: progress, height: progress.interpolate({ inputRange: [0, 1], outputRange: [0, height] }) }}>
    {content}
  </Animated.View>;
}

/** Fades a new transcript entry up and lifts it a few pixels, once, when it first appears. */
export function Arrive({ children }: { children: ReactNode }) {
  const reduced = useReducedMotion();
  const progress = useRef(new Animated.Value(0)).current;
  useEffect(() => {
    const animation = Animated.timing(progress, { toValue: 1, duration: reduced ? 0 : arriveMs, easing: Easing.out(Easing.cubic), useNativeDriver: nativeDriver });
    animation.start();
    return () => animation.stop();
  }, [progress, reduced]);
  return <Animated.View style={{ opacity: progress, transform: [{ translateY: progress.interpolate({ inputRange: [0, 1], outputRange: [6, 0] }) }] }}>
    {children}
  </Animated.View>;
}

/**
 * The part of a streamed text to draw now: it advances once a frame towards everything that has
 * arrived, so a reply flows rather than jumping a chunk at a time.
 */
export function usePacedText(text: string): string {
  const reduced = useReducedMotion();
  const [shown, setShown] = useState(text);
  const latest = useRef(text);
  latest.current = text;
  useEffect(() => {
    const start = pacedStart(shown, text, !reduced);
    if (start === text) { if (shown !== text) setShown(text); return; }
    let frame = 0;
    let last = Date.now();
    let current = start.length;
    const tick = () => {
      const now = Date.now();
      current = revealStep(current, latest.current.length, now - last);
      last = now;
      setShown(latest.current.slice(0, current));
      if (current < latest.current.length) frame = requestAnimationFrame(tick);
    };
    frame = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(frame);
    // Restart only when new text arrives; the frame loop itself moves `shown`.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [text, reduced]);
  return shown;
}
