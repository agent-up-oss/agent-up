/**
 * Geometry for sheets drawn in a sheet layer rather than inside the page they belong to.
 *
 * A page clips what it draws, so a sheet resting on a page could only ever grow to that page: the
 * head above it and the tab bar below it stayed in view. A sheet layer spans the whole surface the
 * page sits in, so a sheet drawn there rests on its page's bottom edge but opens over everything.
 * Kept free of React Native so the arithmetic is tested under plain Node.
 */

/** A rectangle's vertical extent in window coordinates. */
export type WindowSpan = { y: number; height: number };

/** How tall a sheet in the layer may open, and how far the page's bottom edge sits above the layer's. */
export type SheetLayerFrame = { height: number; bottomInset: number };

/**
 * Where a sheet that rests on `page` reaches inside `layer`. It may open to the layer's top, but
 * never under the phone's own chrome: `topInset` is how far that reaches down the window.
 */
export function sheetLayerFrame(input: { layer: WindowSpan; page: WindowSpan; topInset: number }): SheetLayerFrame {
  const { layer, page, topInset } = input;
  const covered = Math.max(0, topInset - layer.y);
  const layerBottom = layer.y + layer.height;
  return {
    height: Math.max(0, Math.round(layer.height - covered)),
    bottomInset: Math.max(0, Math.round(layerBottom - (page.y + page.height))),
  };
}
