import { createContext, useContext, type ComponentType, type ReactNode } from 'react';
import type { SheetStatusTone } from '../providers/SheetProvider';

/** What the chat asks a host to draw: one transcript item opened to read. */
export type ChatSheetHostProps = {
  title: string;
  status?: { tone: SheetStatusTone; text: string };
  /** The host calls this once the sheet has gone, however it was closed. */
  onClose: () => void;
  children: ReactNode;
  testID: string;
  /**
   * When the item is a file edit, the edit as unified text. A host with a diff view of its own
   * opens the item as that diff instead of drawing `children`.
   */
  diff?: { path?: string; unified: string };
};

/**
 * Where the chat opens its sheets. Left unset, the chat draws its own sheet over the whole screen.
 * A host that stacks sheets of its own - a team member opened as a sheet - provides one, so a
 * transcript item opens as one more sheet in that stack instead of on top of everything.
 */
export const ChatSheetHostContext = createContext<ComponentType<ChatSheetHostProps> | null>(null);

export function useChatSheetHost(): ComponentType<ChatSheetHostProps> | null {
  return useContext(ChatSheetHostContext);
}
