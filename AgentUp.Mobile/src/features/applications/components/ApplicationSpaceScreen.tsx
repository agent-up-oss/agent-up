import { useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, StyleSheet, Text, View } from 'react-native';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import type { Workspace } from '@/features/workspaces/models/Workspace';
import { applicationProxyUrl, issueApplicationProxyTicket } from '../providers/ApplicationBrowserProvider';
import { RemoteBrowser } from './RemoteBrowser';

type ApplicationSpaceScreenProps = { workspace: Workspace; applicationName: string };

/** Displays an application's HTTP interface through an authenticated HTTPS tunnel. */
export function ApplicationSpaceScreen({ workspace, applicationName }: ApplicationSpaceScreenProps) {
  const { server } = useWorkspaces();
  const [source, setSource] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const application = workspace.applications?.find(entry => entry.name === applicationName);

  useShellConfig(useMemo(() => ({ title: applicationName, rightAction: null, sidebarContent: null }), [applicationName]));

  useEffect(() => {
    let active = true;
    const request = new AbortController();
    setSource(null);
    setError(null);
    if (!server || !application) {
      setError(application ? 'No Server connection is available.' : 'The application no longer exists.');
      return () => { active = false; request.abort(); };
    }
    void issueApplicationProxyTicket(server, workspace.id, application, fetch, request.signal)
      .then(ticket => { if (active) setSource(applicationProxyUrl(server, ticket)); })
      .catch(reason => { if (active) setError(reason instanceof Error ? reason.message : String(reason)); });
    return () => { active = false; request.abort(); };
  }, [server, workspace.id, application]);

  return (
    <View style={styles.container}>
      {!source && !error && <View style={styles.message}><ActivityIndicator color="#00d66b" /><Text style={styles.text}>Opening the application…</Text></View>}
      {!!error && <View style={styles.message}><Text accessibilityRole="alert" style={styles.error}>{error}</Text></View>}
      {source && <RemoteBrowser source={source} />}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, minHeight: 400, backgroundColor: '#050505' },
  message: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: 12, padding: 24 },
  text: { color: '#aebcb3' },
  error: { color: '#d84f4f', textAlign: 'center' },
});
