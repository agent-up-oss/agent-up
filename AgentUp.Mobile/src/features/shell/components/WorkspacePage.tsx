import { type ReactNode } from 'react';
import { StyleSheet, View } from 'react-native';
import { agentUpTheme } from '@agent-up/design-system/native';
import { ReadingContent } from './ReadingContent';
import { WorkspaceTabs } from './WorkspaceTabs';
import type { WorkspaceOverviewTab } from '../providers/WorkspaceTabProvider';

/**
 * A workspace overview: the body in the reading column, the tab bar across the window.
 */
export function WorkspacePage({
  children,
  workspaceId,
  active,
}: {
  children: ReactNode;
  workspaceId: string;
  active: WorkspaceOverviewTab;
}) {
  return (
    <View style={styles.screen}>
      <ReadingContent>{children}</ReadingContent>
      <WorkspaceTabs workspaceId={workspaceId} active={active} />
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: agentUpTheme.colors.canvas },
});
