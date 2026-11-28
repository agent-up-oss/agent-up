/**
 * Gesture rules for a page sheet: a team member over the team, or an application or a file over
 * that member. It covers the space it opens in, and its head is the handle: dragging the head down,
 * or tapping its grabber, closes it. The page under the head keeps its own scrolling. Kept free of
 * React Native so the rules a finger runs into are tested under plain Node.
 *
 * Offsets are how far the sheet has moved down from fully open, in points.
 */

/** The head's height when it has not been measured yet. */
export const pageSheetHandleFallback = 96;
/** How far a drag must move before it counts as one, so a tap on the grabber or Reload stays a tap. */
export const pageSheetDragSlop = 6;
/** Release speed, in points per millisecond, that reads as a flick down. */
export const pageSheetFlickVelocity = 0.9;
/** The share of the sheet a slow drag must cover before releasing it closes the sheet. */
export const pageSheetCloseShare = 0.3;
/** How far the sheet follows a finger that pulls it up past fully open, as a fraction of the pull. */
export const pageSheetOvershootResistance = 0.2;

/**
 * Whether a move belongs to the sheet. Only a downward drag that starts on its head does: the head
 * reaches `handleHeight` down the sheet, and a sheet docked under it has a head of its own.
 */
export function pageSheetTakesDrag(input: { fromTop: number; handleHeight: number | null; dx: number; dy: number }): boolean {
  const { fromTop, dx, dy } = input;
  const handle = input.handleHeight && input.handleHeight > 0 ? input.handleHeight : pageSheetHandleFallback;
  if (fromTop < 0 || fromTop > handle) return false;
  return dy >= pageSheetDragSlop && Math.abs(dy) > Math.abs(dx);
}

/** The sheet's offset while a finger drags it by `dy` (down is positive). */
export function pageSheetDraggedOffset(dy: number): number {
  return dy >= 0 ? dy : dy * pageSheetOvershootResistance;
}

/** Where a released sheet comes to rest: a flick down or a long drag closes it, anything else reopens it. */
export function settlePageSheet(input: { offset: number; velocity: number; height: number }): 'open' | 'closed' {
  const { offset, velocity, height } = input;
  if (velocity >= pageSheetFlickVelocity) return 'closed';
  if (velocity <= -pageSheetFlickVelocity) return 'open';
  return offset > height * pageSheetCloseShare ? 'closed' : 'open';
}
