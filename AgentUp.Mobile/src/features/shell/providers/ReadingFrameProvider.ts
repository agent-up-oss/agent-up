/**
 * The installable web app is also opened in a desktop browser for a quick look. Phone-width
 * surfaces stay full bleed. Wider surfaces shrink to a 4:3 reading frame centered on the canvas,
 * including Team and Tasks, so chrome does not stretch across the window.
 */

export const readingAspectWidth = 4;
export const readingAspectHeight = 3;

export type ReadingFrameSize = { width: number; height: number };

/** Largest 4:3 rectangle that fits in the viewport without cropping a taller phone. */
export function readingFrameSize(viewportWidth: number, viewportHeight: number): ReadingFrameSize {
  const width = Math.max(0, viewportWidth);
  const height = Math.max(0, viewportHeight);
  return {
    width: Math.min(width, Math.round((height * readingAspectWidth) / readingAspectHeight)),
    height,
  };
}
