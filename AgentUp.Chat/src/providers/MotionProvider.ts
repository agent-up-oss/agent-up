import { isFollowing } from './AgentEventPresentationProvider';

/** How long expand, collapse and arrival take, matching the Desktop. */
export const revealMs = 220;
export const arriveMs = 240;

/**
 * How much of a streamed text to show next. Text arrives in bursts; showing it at a pace that
 * catches up within about a quarter of a second makes it flow instead of pop, without ever falling
 * far behind the agent. The same step the Desktop takes.
 */
export function revealStep(shown: number, target: number, elapsedMs: number): number {
  if (shown >= target) return target;
  const frames = Math.max(1, elapsedMs / 16);
  const step = Math.ceil(Math.max(4, (target - shown) * 0.25) * frames);
  return Math.min(target, shown + step);
}

/**
 * Whether the transcript should go on following new output after a scroll. The transcript's own
 * glide only ever moves down, so moving up is the reader taking over; reaching the end again
 * hands it back. In between, whatever was decided stands, which is what keeps an animated scroll
 * from mistaking its own halfway positions for the reader.
 */
export function followAfterScroll(following: boolean, previousOffset: number, offset: number, viewport: number, content: number): boolean {
  if (isFollowing(offset, viewport, content)) return true;
  if (offset < previousOffset - 2) return false;
  return following;
}

/** The text to keep showing when a streamed text changes: paced only when it extends what is shown. */
export function pacedStart(shown: string, next: string, animate: boolean): string {
  return animate && shown.length > 0 && next.startsWith(shown) ? shown : next;
}
