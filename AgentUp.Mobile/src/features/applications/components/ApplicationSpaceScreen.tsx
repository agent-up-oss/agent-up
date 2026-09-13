import { useEffect, useMemo, useRef, useState } from 'react';
import { ActivityIndicator, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { useServers } from '@/features/servers/controllers/ServersContext';
import type { Workspace } from '@/features/workspaces/models/Workspace';
import { agentUpTheme, auText } from '@agent-up/design-system/native';
import { waitForDesktopViewerUrl } from '../providers/DesktopViewerProvider';
import { DesktopStreamView } from './DesktopStreamView';

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
  const { activeServer } = useServers();
  const [viewerUrl, setViewerUrl] = useState<string | null>(null);
  const [viewerError, setViewerError] = useState<string | null>(null);
  const applicationState = useRef(application?.state);
  applicationState.current = application?.state;

  useEffect(() => {
    if (application?.kind !== 'Desktop' || !activeServer) return;
    const controller = new AbortController();
    setViewerUrl(null);
    setViewerError(null);
    waitForDesktopViewerUrl(activeServer, workspace.id, applicationName, {
      applicationState: () => applicationState.current ?? 'Failed',
      signal: controller.signal,
    })
      .then(url => { if (!controller.signal.aborted) setViewerUrl(url); })
      .catch(error => {
        if (controller.signal.aborted || (error instanceof Error && error.name === 'AbortError')) return;
        setViewerError(error instanceof Error ? error.message : String(error));
      });
    return () => controller.abort();
  }, [activeServer, application?.kind, applicationName, workspace.id]);

  if (application?.kind === 'Desktop') {
    if (viewerError) {
      return (
        <View style={styles.center}>
          <Text style={styles.error}>Could not open the desktop application: {viewerError}</Text>
        </View>
      );
    }
    if (!viewerUrl) {
      return (
        <View style={styles.center}>
          <ActivityIndicator color={agentUpTheme.colors.accent} />
          <Text style={styles.status}>Connecting to the desktop application...</Text>
        </View>
      );
    }
    return <View style={styles.desktop}><DesktopStreamView url={viewerUrl} /></View>;
  }

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
  desktop: { flex: 1, backgroundColor: agentUpTheme.colors.canvas },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: agentUpTheme.colors.canvas, padding: agentUpTheme.spacing[6], gap: agentUpTheme.spacing[3] },
  error: { ...auText('badgeDanger'), textAlign: 'center' },
});
