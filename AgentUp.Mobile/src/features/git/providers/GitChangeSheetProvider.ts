import { usesSheet } from '@agent-up/chat/providers/SheetProvider';

/**
 * Geometry and gesture decisions for the change sheet on the Mobile Git page. The change list rests
 * in a sheet pinned to the bottom of the page: a peek of about a quarter of the page, whose rows stay
 * tappable, and the full page. There is no half detent and no closed state - the list is always
 * there. Kept free of React Native so the rules a finger runs into are tested under plain Node.
 *
 * Heights are how much of the sheet shows above the bottom of the page.
 */

export type GitChangeSheetDetent = 'peek' | 'full';
export type GitChangeSheetDetents = { peek: number; full: number };

/** The share of the page the sheet shows while it rests at its peek. */
export const changeSheetPeekShare = 0.25;
/** A peek never shrinks below the grabber, the heading and about three rows. */
export const changeSheetPeekMinimum = 168;
/** How far a drag must move before it counts as one, so a tap on a checkbox stays a tap. */
export const changeSheetDragSlop = 6;
/** Release speed, in points per millisecond, that reads as a flick rather than a placement. */
export const changeSheetFlickVelocity = 0.9;
/** How far past either detent the sheet follows the finger, as a fraction of the overshoot. */
export const changeSheetOvershootResistance = 0.2;

/** Peek and full heights for a Git page of `pageHeight` points. */
export function gitChangeSheetDetents(pageHeight: number): GitChangeSheetDetents {
  const full = Math.max(0, Math.round(pageHeight));
  const peek = Math.min(full, Math.max(changeSheetPeekMinimum, Math.round(full * changeSheetPeekShare)));
  return { peek, full };
}

export function gitChangeSheetHeight(detent: GitChangeSheetDetent, detents: GitChangeSheetDetents): number {
  return detent === 'full' ? detents.full : detents.peek;
}

/**
 * Whether a move inside the sheet belongs to the sheet rather than its list. Sideways moves and
 * taps stay with the list. At the peek every vertical drag moves the sheet, which is how pulling up
 * opens it. At full height the list scrolls, and the sheet only takes a downward drag once the list
 * is back at its top.
 */
export function gitChangeSheetTakesDrag(input: { detent: GitChangeSheetDetent; scrollTop: number; dx: number; dy: number }): boolean {
  const { detent, scrollTop, dx, dy } = input;
  if (Math.abs(dy) < changeSheetDragSlop || Math.abs(dy) <= Math.abs(dx)) return false;
  if (detent === 'peek') return true;
  return dy > 0 && scrollTop <= 0;
}

/** The height the sheet shows while a finger drags it from `start` by `dy` (down is positive). */
export function gitChangeSheetDraggedHeight(start: number, dy: number, detents: GitChangeSheetDetents): number {
  // A drag can start where a spring stopped past a detent. That height is already resisted, so it
  // goes back to the finger's coordinate before the move is added, or it would be resisted twice.
  const origin = start < detents.peek
    ? detents.peek - (detents.peek - start) / changeSheetOvershootResistance
    : start > detents.full
      ? detents.full + (start - detents.full) / changeSheetOvershootResistance
      : start;
  const raw = origin - dy;
  if (raw < detents.peek) return detents.peek - (detents.peek - raw) * changeSheetOvershootResistance;
  if (raw > detents.full) return detents.full + (raw - detents.full) * changeSheetOvershootResistance;
  return raw;
}

/** Where a released sheet comes to rest: a flick goes its way, a slow release to the nearer detent. */
export function settleGitChangeSheet(input: { height: number; velocity: number; detents: GitChangeSheetDetents }): GitChangeSheetDetent {
  const { height, velocity, detents } = input;
  if (velocity >= changeSheetFlickVelocity) return 'peek';
  if (velocity <= -changeSheetFlickVelocity) return 'full';
  return Math.abs(detents.full - height) < Math.abs(height - detents.peek) ? 'full' : 'peek';
}

/** The count under the sheet's heading: how many changed files the next commit takes. */
export function gitChangeSheetCount(selected: number, total: number): string {
  if (total === 0) return 'No changes';
  return `${selected} of ${total} selected`;
}

/**
 * Whether the Git page rests its change list in a sheet. Only a phone-width window does, at the
 * same width where the chat stops opening sheets; anything wider lists the changes in a scrolling
 * pane under the commit box.
 */
export function gitChangesUseSheet(windowWidth: number): boolean {
  return usesSheet(windowWidth);
}
