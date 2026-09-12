import { useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { useServers } from '@/features/servers/controllers/ServersContext';
import type { Workspace } from '@/features/workspaces/models/Workspace';
import { createDesktopViewerUrl } from '../providers/DesktopViewerProvider';
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

  useEffect(() => {
    if (application?.kind !== 'Desktop' || application.state !== 'Running' || !activeServer) return;
    let active = true;
    setViewerUrl(null);
    setViewerError(null);
    createDesktopViewerUrl(activeServer, workspace.id, applicationName)
      .then(url => { if (active) setViewerUrl(url); })
      .catch(error => { if (active) setViewerError(error instanceof Error ? error.message : String(error)); });
    return () => { active = false; };
  }, [activeServer, application?.kind, application?.state, applicationName, workspace.id]);

  if (application?.kind === 'Desktop') {
    if (application.state !== 'Running') return <View style={styles.center}><Text style={styles.status}>Current state: {application.state}</Text></View>;
    if (viewerError) return <View style={styles.center}><Text style={styles.error}>{viewerError}</Text></View>;
    if (!viewerUrl) return <View style={styles.center}><ActivityIndicator color="#00d66b" /></View>;
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
  content: { padding: 20, paddingBottom: 32, gap: 12 },
  subtitle: { color: '#aebcb3', fontSize: 14 },
  placeholder: { color: '#f5fbf7', lineHeight: 22, fontSize: 16 },
  status: { color: '#9fb2a8' },
  desktop: { flex: 1, backgroundColor: '#111111' },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: '#111111', padding: 24 },
  error: { color: '#e48989', textAlign: 'center' },
});
