/**
 * Geometry for sheets drawn in a sheet layer rather than inside the page they belong to.
 *
 * A page clips what it draws, so a sheet resting on a page could only ever grow to that page. A
 * sheet layer spans the surface the page sits in, so a sheet drawn there rests on its page's bottom
 * edge but opens past the tab bar under the page. It never opens over the head above the page: the
 * sheet docks right below it, so what it belongs to stays in view. Kept free of React Native so the
 * arithmetic is tested under plain Node.
 */

/** A rectangle's vertical extent in window coordinates. */
export type WindowSpan = { y: number; height: number };

/** How tall a sheet in the layer may open, and how far the page's bottom edge sits above the layer's. */
export type SheetLayerFrame = { height: number; bottomInset: number };

/**
 * Where a sheet that rests on `page` reaches inside `layer`: from the top of the page, which is the
 * bottom of the head above it, down to the bottom of the layer. It never opens under the phone's own
 * chrome: `topInset` is how far that reaches down the window.
 */
export function sheetLayerFrame(input: { layer: WindowSpan; page: WindowSpan; topInset: number }): SheetLayerFrame {
  const { layer, page, topInset } = input;
  const layerBottom = layer.y + layer.height;
  const top = Math.max(page.y, layer.y, topInset);
  return {
    height: Math.max(0, Math.round(layerBottom - top)),
    bottomInset: Math.max(0, Math.round(layerBottom - (page.y + page.height))),
  };
}

/** How far below the layer's top a sheet docks: the bottom of the head, as both were measured. */
export function sheetDockOffset(layer: WindowSpan, dock: WindowSpan): number {
  return Math.max(0, Math.round(dock.y - layer.y));
}
