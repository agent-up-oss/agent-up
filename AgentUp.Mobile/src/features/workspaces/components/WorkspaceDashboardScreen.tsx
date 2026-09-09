import { useRouter } from 'expo-router';
import { useCallback, useMemo } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import type { Workspace, WorkspaceApplication } from '../models/Workspace';
import { useWorkspaces } from '../controllers/WorkspacesContext';

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

      {loading && <ActivityIndicator color="#00d66b" />}
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
        <View style={[styles.stateDot, { backgroundColor: applicationStateColor(application.state) }]} />
        <Text style={styles.listTitle}>{application.name}</Text>
      </View>
      <Text style={styles.listDetail}>{application.state}</Text>
    </Pressable>
  );
}

function applicationStateColor(state: string): string {
  if (state === 'Running') return '#00d66b';
  if (state === 'Starting' || state === 'Stopping') return '#e0a33c';
  if (state === 'Failed') return '#d84f4f';
  return '#718077';
}

const styles = StyleSheet.create({
  content: { padding: 20, paddingBottom: 32, gap: 18 },
  subtitle: { color: '#aebcb3', fontSize: 14 },
  error: { color: '#d84f4f', lineHeight: 21 },
  agentCard: {
    padding: 18,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#2bf27a',
    backgroundColor: '#08150d',
    gap: 8,
  },
  agentTitle: { color: '#f5fbf7', fontSize: 22, fontWeight: '800' },
  agentDetail: { color: '#aebcb3', lineHeight: 21 },
  agentAction: { color: '#2bf27a', fontWeight: '700' },
  section: { gap: 10 },
  sectionTitle: { color: '#f5fbf7', fontSize: 18, fontWeight: '800' },
  empty: { color: '#aebcb3', lineHeight: 21 },
  listCard: {
    padding: 14,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#287038',
    backgroundColor: '#050505',
    gap: 4,
  },
  listHeader: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  stateDot: { width: 8, height: 8, borderRadius: 4 },
  listTitle: { color: '#f5fbf7', fontSize: 16, fontWeight: '700' },
  listDetail: { color: '#9fb2a8', fontSize: 13 },
});
