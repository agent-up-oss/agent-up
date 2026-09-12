import { useRouter } from 'expo-router';
import { useCallback, useMemo } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
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

  const openAgent = useCallback(() => {
    router.push(`/(main)/workspace/${workspace.id}/agent`);
  }, [router, workspace.id]);

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
  const agents = [{ id: 'workspace-agent', name: 'Workspace agent', detail: 'Primary coding agent for this workspace' }];

  return (
    <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
      <Text style={styles.subtitle}>{workspace.branch} · {workspace.state}</Text>

      {loading && <ActivityIndicator color={agentUpTheme.colors.accent} />}
      {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}

      <Pressable accessibilityRole="button" accessibilityLabel="Open workspace agent chat" onPress={openAgent} style={styles.agentCard}>
        <Text style={styles.agentTitle}>Workspace agent</Text>
        <Text style={styles.agentDetail}>Open the agent chat for this workspace.</Text>
        <Text style={styles.agentAction}>Open chat →</Text>
      </Pressable>

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

      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Agents</Text>
        {agents.map(agent => (
          <Pressable
            key={agent.id}
            accessibilityRole="button"
            accessibilityLabel={`Open ${agent.name}`}
            onPress={openAgent}
            style={styles.listCard}>
            <Text style={styles.listTitle}>{agent.name}</Text>
            <Text style={styles.listDetail}>{agent.detail}</Text>
          </Pressable>
        ))}
      </View>
    </ScrollView>
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
  content: { padding: agentUpTheme.spacing[4], paddingBottom: agentUpTheme.spacing[8], gap: 16, maxWidth: 672, width: '100%', alignSelf: 'center' },
  subtitle: auText('muted'),
  error: auText('badgeDanger'),
  agentCard: { ...auBox('card'), gap: agentUpTheme.spacing[2], alignSelf: 'stretch' },
  agentTitle: auText('pageTitle'),
  agentDetail: auText('muted'),
  agentAction: auText('accent'),
  section: { gap: 10 },
  sectionTitle: auText('fieldLabel'),
  empty: auText('muted'),
  listCard: { ...auBox('workspace'), flexDirection: 'column', gap: 2 },
  listHeader: { flexDirection: 'row', alignItems: 'center', gap: agentUpTheme.spacing[2] },
  listTitle: auText('workspaceName'),
  listDetail: auText('workspaceBranch'),
});
