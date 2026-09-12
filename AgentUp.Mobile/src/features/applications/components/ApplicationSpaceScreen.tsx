import { useMemo } from 'react';
import { ScrollView, StyleSheet, Text } from 'react-native';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import type { Workspace } from '@/features/workspaces/models/Workspace';
import { agentUpTheme, auText } from '@agent-up/design-system/native';

type ApplicationSpaceScreenProps = {
  workspace: Workspace;
  applicationName: string;
};

export function ApplicationSpaceScreen({ workspace, applicationName }: ApplicationSpaceScreenProps) {
  const shellConfig = useMemo(() => ({
    title: applicationName,
    rightAction: null,
    sidebarContent: null,
  }), [applicationName]);

  useShellConfig(shellConfig);

  const application = workspace.applications?.find(entry => entry.name === applicationName);

  return (
    <ScrollView contentContainerStyle={styles.content}>
      <Text style={styles.subtitle}>{workspace.displayName}</Text>
      <Text style={styles.placeholder}>
        The application space for {applicationName} will be implemented here.
      </Text>
      {application && <Text style={styles.status}>Current state: {application.state}</Text>}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  content: { padding: agentUpTheme.spacing[4], paddingBottom: agentUpTheme.spacing[8], gap: agentUpTheme.spacing[3] },
  subtitle: auText('muted'),
  placeholder: auText('muted'),
  status: auText('muted'),
});
