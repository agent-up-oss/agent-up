import { useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, StyleSheet, Text, View } from 'react-native';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import type { Workspace } from '@/features/workspaces/models/Workspace';
import { browserViewerUrl, navigateApplicationBrowser } from '../providers/ApplicationBrowserProvider';
import { RemoteBrowser } from './RemoteBrowser';

type ApplicationSpaceScreenProps = { workspace: Workspace; applicationName: string };

/** Displays an application's authenticated Server-owned browser session. */
export function ApplicationSpaceScreen({ workspace, applicationName }: ApplicationSpaceScreenProps) {
  const { server } = useWorkspaces();
  const [ready, setReady] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const application = workspace.applications?.find(entry => entry.name === applicationName);
  const viewerUrl = server ? browserViewerUrl(server, workspace.id) : null;

  useShellConfig(useMemo(() => ({ title: applicationName, rightAction: null, sidebarContent: null }), [applicationName]));

  useEffect(() => {
    let active = true;
    const request = new AbortController();
    setReady(false);
    setError(null);
    if (!server || !application) {
      setError(application ? 'No Server connection is available.' : 'The application no longer exists.');
      return () => { active = false; request.abort(); };
    }
    void navigateApplicationBrowser(server, workspace.id, application, fetch, request.signal)
      .then(() => { if (active) setReady(true); })
      .catch(reason => { if (active) setError(reason instanceof Error ? reason.message : String(reason)); });
    return () => { active = false; request.abort(); };
  }, [server, workspace.id, application]);

  return (
    <View style={styles.container}>
      {!ready && !error && <View style={styles.message}><ActivityIndicator color="#00d66b" /><Text style={styles.text}>Connecting to the Server browser…</Text></View>}
      {!!error && <View style={styles.message}><Text accessibilityRole="alert" style={styles.error}>{error}</Text></View>}
      {ready && viewerUrl && <RemoteBrowser source={viewerUrl} />}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, minHeight: 400, backgroundColor: '#050505' },
  message: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: 12, padding: 24 },
  text: { color: '#aebcb3' },
  error: { color: '#d84f4f', textAlign: 'center' },
});
