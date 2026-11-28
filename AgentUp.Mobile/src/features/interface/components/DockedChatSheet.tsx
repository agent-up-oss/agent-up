import { useState } from 'react';
import { ScrollView, StyleSheet } from 'react-native';
import { auBox } from '@agent-up/design-system/native';
import type { ChatSheetHostProps } from '@agent-up/chat';
import { PageSheet } from '@/features/shell/components/PageSheet';
import { SheetHead } from '@/features/shell/components/SheetHead';
import { GitDiffView } from '@/features/git/components/GitDiffView';

/**
 * A transcript item opened from a team member's chat, as one more sheet in that member's stack: it
 * docks right below the member's head, and a drag down or a tap on its grabber closes it before the
 * member's own sheet. A file edit opens as that file's changes, in the same diff view the Git tab
 * uses.
 */
export function DockedChatSheet({ title, status, onClose, children, testID, diff }: ChatSheetHostProps) {
  const [closeRequest, setCloseRequest] = useState(0);
  const close = () => setCloseRequest(current => current + 1);
  if (diff) {
    return (
      <PageSheet placement="docked" testID={testID} closeRequest={closeRequest} onDismiss={() => true} onClosed={onClose}>
        <GitDiffView
          mode="sheet"
          path={diff.path ?? title}
          diff={{ path: diff.path ?? title, status: 'Modified', isBinary: false, diff: diff.unified }}
          file={null}
          loading={false}
          onClose={close}
        />
      </PageSheet>
    );
  }
  return (
    <PageSheet
      placement="docked"
      testID={testID}
      closeRequest={closeRequest}
      onDismiss={() => true}
      onClosed={onClose}>
      <SheetHead
        testID={`${testID}-header`}
        title={title}
        subtitle={status?.text}
        subtitleTone={status?.tone === 'failed' || status?.tone === 'running' ? status.tone : undefined}
        onClose={close}
      />
      <ScrollView testID={`${testID}-content`} style={styles.content} contentContainerStyle={styles.inner}>
        {children}
      </ScrollView>
    </PageSheet>
  );
}

const styles = StyleSheet.create({
  content: { flex: 1 },
  inner: { ...auBox('sheetContent'), gap: 16, paddingBottom: 32 },
});
