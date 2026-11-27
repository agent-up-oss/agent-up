/**
 * The installable web app is also opened in a desktop browser for a quick look. Phone-width
 * surfaces stay full bleed. Wider surfaces keep the top and bottom bars across the window and
 * center only the page body in a 4:3 reading column, including Team and Tasks.
 */

export const readingAspectWidth = 4;
export const readingAspectHeight = 3;

export type ReadingFrameSize = { width: number; height: number };
export type ReadingFrameLayout = { kind: 'fill' } | { kind: 'column'; width: number };

/** Largest 4:3 width that fits in the viewport without cropping a taller phone. */
export function readingFrameSize(viewportWidth: number, viewportHeight: number): ReadingFrameSize {
  const width = Math.max(0, viewportWidth);
  const height = Math.max(0, viewportHeight);
  return {
    width: Math.min(width, Math.round((height * readingAspectWidth) / readingAspectHeight)),
    height,
  };
}

/**
 * An unmeasured viewport (static HTML, a window of 0) fills its parent instead of clipping to a
 * 0-wide column. A measured viewport that is already no wider than 4:3 of its height stays fill.
 * A wider window gets a centered reading column; chrome stays full bleed.
 */
export function readingFrameLayout(viewportWidth: number, viewportHeight: number): ReadingFrameLayout {
  if (viewportWidth <= 0 || viewportHeight <= 0) return { kind: 'fill' };
  const { width } = readingFrameSize(viewportWidth, viewportHeight);
  if (width >= viewportWidth) return { kind: 'fill' };
  return { kind: 'column', width };
}
