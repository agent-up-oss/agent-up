/**
 * Gesture rules for the sheet a team member opens in, over the team. It covers the whole screen
 * while open; dragging its head down, or tapping its grabber, returns to the team. Kept free of
 * React Native so the rules a finger runs into are tested under plain Node.
 *
 * Offsets are how far the sheet has moved down from fully open, in points.
 */

/** How far down the sheet the head reaches: a drag that starts there moves the sheet. */
export const memberSheetHandleHeight = 96;
/** How far a drag must move before it counts as one, so a tap on the grabber or Reload stays a tap. */
export const memberSheetDragSlop = 6;
/** Release speed, in points per millisecond, that reads as a flick down. */
export const memberSheetFlickVelocity = 0.9;
/** The share of the sheet a slow drag must cover before releasing it closes the sheet. */
export const memberSheetCloseShare = 0.3;
/** How far the sheet follows a finger that pulls it up past fully open, as a fraction of the pull. */
export const memberSheetOvershootResistance = 0.2;

/**
 * Whether a move belongs to the member sheet. Only a downward drag that starts on the head does: the
 * page under it keeps its own scrolling and its own sheets. A sheet the page has opened over the
 * whole member sheet takes the drag instead, so a drag down closes the innermost sheet first.
 */
export function memberSheetTakesDrag(input: { fromTop: number; dx: number; dy: number; innerExpanded: boolean }): boolean {
  const { fromTop, dx, dy, innerExpanded } = input;
  if (innerExpanded) return false;
  if (fromTop < 0 || fromTop > memberSheetHandleHeight) return false;
  return dy >= memberSheetDragSlop && Math.abs(dy) > Math.abs(dx);
}

/** The sheet's offset while a finger drags it by `dy` (down is positive). */
export function memberSheetDraggedOffset(dy: number): number {
  return dy >= 0 ? dy : dy * memberSheetOvershootResistance;
}

/** Where a released sheet comes to rest: a flick down or a long drag closes it, anything else reopens it. */
export function settleMemberSheet(input: { offset: number; velocity: number; height: number }): 'open' | 'closed' {
  const { offset, velocity, height } = input;
  if (velocity >= memberSheetFlickVelocity) return 'closed';
  if (velocity <= -memberSheetFlickVelocity) return 'open';
  return offset > height * memberSheetCloseShare ? 'closed' : 'open';
}
