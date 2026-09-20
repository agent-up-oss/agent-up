import { useCallback, useMemo } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useRouter } from 'expo-router';
import { WorkspaceTabBar } from '@/features/shell/components/WorkspaceTabBar';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import type { Workspace, WorkspaceApplication } from '../models/Workspace';
import { useWorkspaces } from '../controllers/WorkspacesContext';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';

type WorkspaceDashboardScreenProps = {
  workspace: Workspace;
};

export function WorkspaceDashboardScreen({ workspace }: WorkspaceDashboardScreenProps) {
  const router = useRouter();
  const { loading, error, refresh } = useWorkspaces();

  const openApplication = useCallback((applicationName: string) => {
    router.push(`/(main)/workspace/${workspace.id}/application/${encodeURIComponent(applicationName)}`);
  }, [router, workspace.id]);

  const shellConfig = useMemo(() => ({
    title: workspace.displayName,
    rightAction: {
      label: 'Reload',
      accessibilityLabel: 'Reload workspace',
      onPress: () => { void refresh(); },
    },
    sidebarContent: null,
  }), [workspace.displayName, refresh]);

  useShellConfig(shellConfig);

  const applications = workspace.applications ?? [];

  return (
    <View style={styles.screen}>
      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        <Text style={styles.subtitle}>{workspace.state}</Text>

        {loading && <ActivityIndicator color={agentUpTheme.colors.accent} />}
        {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}

        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Applications</Text>
          {applications.length === 0
            ? <Text style={styles.empty}>No applications configured.</Text>
            : applications.map(application => (
              <ApplicationRow
                key={application.name}
                application={application}
                onPress={() => openApplication(application.name)}
              />
            ))}
        </View>
      </ScrollView>
      <WorkspaceTabBar workspaceId={workspace.id} active="apps" />
    </View>
  );
}

function ApplicationRow({ application, onPress }: { application: WorkspaceApplication; onPress: () => void }) {
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={`Open application ${application.name}`} onPress={onPress} style={styles.listCard}>
      <View style={styles.listHeader}>
        <View style={applicationDot(application.state)} />
        <Text style={styles.listTitle}>{application.name}</Text>
      </View>
      <Text style={styles.listDetail}>{application.state}</Text>
    </Pressable>
  );
}

function applicationDot(state: string) {
  if (state === 'Running') return auBox('statusDot', 'statusDotHealthy');
  if (state === 'Starting' || state === 'Stopping') return auBox('statusDot', 'statusDotWarning');
  if (state === 'Failed') return auBox('statusDot', 'statusDotDanger');
  return auBox('statusDot');
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: agentUpTheme.colors.canvas },
  content: { padding: agentUpTheme.spacing[4], paddingBottom: agentUpTheme.spacing[8], gap: 16, maxWidth: 672, width: '100%', alignSelf: 'center' },
  subtitle: auText('muted'),
  error: auText('badgeDanger'),
  section: { gap: 10 },
  sectionTitle: auText('fieldLabel'),
  empty: auText('muted'),
  listCard: { ...auBox('workspace'), flexDirection: 'column', gap: 2 },
  listHeader: { flexDirection: 'row', alignItems: 'center', gap: agentUpTheme.spacing[2] },
  listTitle: auText('workspaceName'),
  listDetail: auText('workspaceBranch'),
});
