import { useEffect, useRef, useState } from 'react';
import { ActivityIndicator, StyleSheet, Text, View } from 'react-native';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import type { Workspace } from '@/features/workspaces/models/Workspace';
import { agentUpTheme, auText } from '@agent-up/design-system/native';
import { applicationHttpPort, applicationProxySource, issueApplicationProxyTicket, type ApplicationProxySource } from '../providers/ApplicationBrowserProvider';
import { isFakeServerUrl } from '@/features/fake-server/models/FakeServerIdentity';
import { waitForDesktopViewerUrl } from '../providers/DesktopViewerProvider';
import { DesktopStreamView } from './DesktopStreamView';
import { RemoteBrowser } from './RemoteBrowser';

type ApplicationSpaceScreenProps = { workspace: Workspace; applicationName: string };

/** Displays an HTTP application through the Server proxy, or a desktop application through the ticketed viewer. */
export function ApplicationSpaceScreen({ workspace, applicationName }: ApplicationSpaceScreenProps) {
  const { server } = useWorkspaces();
  const { activeServer } = useServers();
  const application = workspace.applications?.find(entry => entry.name === applicationName);
  const allocatedHttpPort = application ? applicationHttpPort(application) : null;
  const applicationKind = application?.kind;
  const [source, setSource] = useState<ApplicationProxySource | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [viewerUrl, setViewerUrl] = useState<string | null>(null);
  const [viewerError, setViewerError] = useState<string | null>(null);
  const applicationState = useRef(application?.state);
  const applicationRef = useRef(application);
  applicationState.current = application?.state;
  applicationRef.current = application;

  useEffect(() => {
    if (applicationKind !== 'Desktop' || !activeServer) return;
    const controller = new AbortController();
    setViewerUrl(null);
    setViewerError(null);
    waitForDesktopViewerUrl(activeServer, workspace.id, applicationName, {
      applicationState: () => applicationState.current ?? 'Failed',
      signal: controller.signal,
    })
      .then(url => { if (!controller.signal.aborted) setViewerUrl(url); })
      .catch(caught => {
        if (controller.signal.aborted || (caught instanceof Error && caught.name === 'AbortError')) return;
        setViewerError(caught instanceof Error ? caught.message : String(caught));
      });
    return () => controller.abort();
  }, [activeServer, applicationKind, applicationName, workspace.id]);

  useEffect(() => {
    if (applicationKind === 'Desktop') return;
    let active = true;
    const request = new AbortController();
    setSource(null);
    setError(null);
    const current = applicationRef.current;
    if (!server || !current) {
      setError(current ? 'No Server connection is available.' : 'The application no longer exists.');
      return () => { active = false; request.abort(); };
    }
    void issueApplicationProxyTicket(server, workspace.id, current, fetch, request.signal)
      .then(async ticket => {
        const source = applicationProxySource(server, ticket);
        if (!isFakeServerUrl(server.url)) return source;
        const page = await fetch(source.uri, { signal: request.signal });
        if (!page.ok) throw new Error('The demo application page is missing.');
        return { ...source, html: await page.text() };
      })
      .then(next => { if (active) setSource(next); })
      .catch(reason => { if (active) setError(reason instanceof Error ? reason.message : String(reason)); });
    return () => { active = false; request.abort(); };
  }, [server, workspace.id, applicationName, allocatedHttpPort, applicationKind]);

  if (applicationKind === 'Desktop') {
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
    <View style={styles.container}>
      {!source && !error && (
        <View style={styles.center}>
          <ActivityIndicator color={agentUpTheme.colors.accent} />
          <Text style={styles.status}>Opening the application…</Text>
        </View>
      )}
      {!!error && (
        <View style={styles.center}>
          <Text accessibilityRole="alert" style={styles.error}>{error}</Text>
        </View>
      )}
      {source && <RemoteBrowser source={source} />}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, minHeight: 400, backgroundColor: agentUpTheme.colors.canvas },
  desktop: { flex: 1, backgroundColor: agentUpTheme.colors.canvas },
  center: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: agentUpTheme.colors.canvas,
    padding: agentUpTheme.spacing[6],
    gap: agentUpTheme.spacing[3],
  },
  status: auText('muted'),
  error: { ...auText('badgeDanger'), textAlign: 'center' },
});
