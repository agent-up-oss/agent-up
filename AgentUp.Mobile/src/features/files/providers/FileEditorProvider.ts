import type { WorkspaceFileContent } from '../models/WorkspaceFiles';

/** The arithmetic and copy an editor page needs about its text. */
export function lineCount(text: string): number {
  let count = 1;
  for (let index = text.indexOf('\n'); index >= 0; index = text.indexOf('\n', index + 1)) count += 1;
  return count;
}

/** One number per line, for the gutter beside the editor. */
export function lineNumbers(count: number): string {
  return Array.from({ length: Math.max(1, count) }, (_, index) => String(index + 1)).join('\n');
}

export function caretLabel(text: string, caret: number): string {
  const index = Math.max(0, Math.min(caret, text.length));
  const before = text.slice(0, index);
  const line = lineCount(before);
  const column = index - before.lastIndexOf('\n');
  return `Ln ${line}, Col ${column}`;
}

/** Why a file shows no editor, or null when it does. */
export function fileNotice(file: WorkspaceFileContent | null): string | null {
  if (!file) return 'This file is no longer in the workspace.';
  if (file.content !== null) return null;
  return file.isTooLarge
    ? 'This file is more than the Server sends to a client.'
    : 'This is a binary file. Files shows text only.';
}

export type EditorState = {
  dirty: boolean;
  saving: boolean;
  canWrite: boolean;
  hasContent: boolean;
  error: string | null;
};

/** The footer's one line: saved, unsaved, saving, read only, or what failed. */
export function editorStateLabel(state: EditorState): string {
  if (state.error) return state.error;
  if (state.saving) return 'Saving…';
  if (state.dirty) return 'Unsaved changes';
  if (!state.canWrite && state.hasContent) return 'Read only. This sign-in cannot change files.';
  return 'Saved';
}

export function statusLabel(file: WorkspaceFileContent | null): string {
  return file?.status ?? 'Unchanged';
}

export function fileNameOf(path: string): string {
  return path.slice(path.lastIndexOf('/') + 1);
}
