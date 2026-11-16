import { useCallback, useMemo, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { WorkspaceTabs } from '@/features/shell/components/WorkspaceTabs';
import { useWorkspaceHost } from '../controllers/WorkspaceHostContext';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import type { Workspace, WorkspaceApplication } from '../models/Workspace';
import { useWorkspaces } from '../controllers/WorkspacesContext';
import {
  reviewReportFileName,
  saveReviewReport,
} from '../providers/ReviewReportExportProvider';
import { getReviewReportHtml } from '../providers/WorkspacesApiProvider';
import {
  statusDotStyle,
  workspaceLedState,
  workspaceLifecycleControls,
  workspaceShowsApplications,
  workspaceStatusLabel,
} from '../providers/WorkspaceStatusProvider';
import { PublicIcon } from '@/features/shell/components/PublicIcon';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';

type WorkspaceDashboardScreenProps = {
  workspace: Workspace;
};

export function WorkspaceDashboardScreen({ workspace }: WorkspaceDashboardScreenProps) {
  const host = useWorkspaceHost(workspace.id);
  const { server, loading, error, refresh, start, stop } = useWorkspaces();
  const [actionPending, setActionPending] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [exporting, setExporting] = useState(false);

  const openApplication = useCallback((applicationName: string) => {
    host.open({ kind: 'application', name: applicationName });
  }, [host, workspace.id]);

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

  const applications = workspaceShowsApplications(workspace.state) ? workspace.applications ?? [] : [];
  const showApplications = workspaceShowsApplications(workspace.state);
  const lifecycle = workspaceLifecycleControls(workspace.state);
  const statusLabel = workspaceStatusLabel(workspace.state, workspace.healthState);
  const ledState = workspaceLedState(workspace.state, workspace.healthState);
  const lifecycleDisabled = actionPending || lifecycle.busy;

  const runLifecycle = useCallback(async () => {
    if (lifecycleDisabled) return;
    setActionPending(true);
    setActionError(null);
    try {
      if (lifecycle.action === 'start') await start(workspace.id);
      else await stop(workspace.id);
    } catch (cause) {
      setActionError(cause instanceof Error ? cause.message : 'Could not update the workspace.');
    } finally {
      setActionPending(false);
    }
  }, [lifecycle.action, lifecycleDisabled, start, stop, workspace.id]);

  const exportReport = useCallback(async () => {
    if (!server || exporting) return;
    setExporting(true);
    setActionError(null);
    try {
      const html = await getReviewReportHtml(server, workspace.id);
      if (!html) throw new Error('Could not export the workspace report.');
      await saveReviewReport(html, reviewReportFileName(workspace.displayName));
    } catch (cause) {
      setActionError(cause instanceof Error ? cause.message : 'Could not export the workspace report.');
    } finally {
      setExporting(false);
    }
  }, [exporting, server, workspace.displayName, workspace.id]);

  return (
    <View style={styles.screen}>
      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        <View style={styles.lifecycleRow}>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={lifecycle.accessibilityLabel}
            accessibilityState={{ disabled: lifecycleDisabled, busy: lifecycle.busy || actionPending }}
            disabled={lifecycleDisabled}
            onPress={() => { void runLifecycle(); }}
            style={[styles.lifecycleButton, lifecycleDisabled && styles.lifecycleButtonDisabled]}>
            <Text style={styles.lifecycleIcon}>{lifecycle.glyph}</Text>
          </Pressable>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Export Report"
            accessibilityState={{ disabled: exporting || !server, busy: exporting }}
            disabled={exporting || !server}
            onPress={() => { void exportReport(); }}
            style={[styles.lifecycleButton, (exporting || !server) && styles.lifecycleButtonDisabled]}>
            <PublicIcon name="download" size={14} />
          </Pressable>
          <View
            accessibilityElementsHidden
            importantForAccessibility="no"
            style={statusDotStyle(ledState)}
          />
          <View style={styles.lifecycleCopy}>
            <Text style={styles.listTitle}>{workspace.displayName}</Text>
            <Text accessibilityLiveRegion="polite" style={styles.listDetail}>{statusLabel}</Text>
          </View>
        </View>

        {loading && <ActivityIndicator color={agentUpTheme.colors.accent} />}
        {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}
        {!!actionError && <Text accessibilityRole="alert" style={styles.error}>{actionError}</Text>}

        {showApplications &&
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
          </View>}
      </ScrollView>
      <WorkspaceTabs workspaceId={workspace.id} active="apps" />
    </View>
  );
}

function ApplicationRow({ application, onPress }: { application: WorkspaceApplication; onPress: () => void }) {
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={`Open application ${application.name}`} onPress={onPress} style={styles.listCard}>
      <View style={styles.listHeader}>
        <View style={statusDotStyle(application.state)} />
        <Text style={styles.listTitle}>{application.name}</Text>
      </View>
      <Text style={styles.listDetail}>{application.state}</Text>
      {application.capabilityStatus && !application.capabilityStatus.canRun &&
        <Text style={styles.listDetail}>
          {application.capabilityStatus.messages.join(' ') || `${application.capabilityStatus.capabilityId} cannot run`}
        </Text>}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: agentUpTheme.colors.canvas },
  content: { padding: agentUpTheme.spacing[4], paddingBottom: agentUpTheme.spacing[8], gap: 16, maxWidth: 672, width: '100%', alignSelf: 'center' },
  lifecycleRow: { ...auBox('workspace'), flexDirection: 'row', alignItems: 'center', gap: agentUpTheme.spacing[2] },
  lifecycleButton: { ...auBox('lifecycleButton'), alignItems: 'center', justifyContent: 'center' },
  lifecycleButtonDisabled: auBox('lifecycleButtonDisabled'),
  lifecycleIcon: auText('lifecycleButton'),
  lifecycleCopy: { flex: 1, gap: 2 },
  error: auText('badgeDanger'),
  section: { gap: 10 },
  sectionTitle: auText('fieldLabel'),
  empty: auText('muted'),
  listCard: { ...auBox('workspace'), flexDirection: 'column', gap: 2 },
  listHeader: { flexDirection: 'row', alignItems: 'center', gap: agentUpTheme.spacing[2] },
  listTitle: auText('workspaceName'),
  listDetail: auText('workspaceBranch'),
});
