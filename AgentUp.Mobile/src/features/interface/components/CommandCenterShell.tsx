import { useMemo, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import type { Workspace } from '@/features/workspaces/models/Workspace';
import { agentUpTheme } from '@agent-up/design-system/native';
import { useInterfaceChoice } from '../controllers/InterfaceContext';
import { InterfaceToggleRow } from './InterfaceToggleRow';

type Surface = 'bridge' | 'tasks' | 'team' | 'settings';

/**
 * The command center, on the phone.
 *
 * An agent and its workspace are the same thing here, so the team is the workspace list and a
 * workspace's own page is that agent's page. Only what the Server already answers is drawn:
 * surfaces that need reads the Server does not expose yet say so rather than showing a number
 * that nothing produced.
 */
export function CommandCenterShell() {
  const { workspaces, loading, error, refresh } = useWorkspaces();
  const { activeServer, logout } = useServers();
  const [surface, setSurface] = useState<Surface>('bridge');
  const [menuOpen, setMenuOpen] = useState(false);

  const serverLabel = activeServer?.url ?? 'No server';

  return (
    <View style={styles.shell}>
      <View style={styles.head}>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Open menu"
          onPress={() => setMenuOpen(true)}
          style={styles.iconButton}
        >
          <Text style={styles.iconGlyph}>☰</Text>
        </Pressable>
        <View style={styles.headBody}>
          <Text style={styles.title}>{titleFor(surface)}</Text>
          <Text style={styles.subtitle} numberOfLines={1}>{serverLabel}</Text>
        </View>
      </View>

      <ScrollView contentContainerStyle={styles.body}>
        {surface === 'bridge' ? <Bridge workspaces={workspaces} loading={loading} error={error} onRetry={refresh} /> : null}
        {surface === 'tasks' ? <Tasks /> : null}
        {surface === 'team' ? <Team workspaces={workspaces} loading={loading} /> : null}
        {surface === 'settings' ? <Settings onSignOut={logout} /> : null}
      </ScrollView>

      <View style={styles.tabBar}>
        <TabItem label="Bridge" active={surface === 'bridge'} onPress={() => setSurface('bridge')} />
        <TabItem label="Tasks" active={surface === 'tasks'} onPress={() => setSurface('tasks')} />
        <TabItem label="Team" active={surface === 'team'} onPress={() => setSurface('team')} />
      </View>

      {menuOpen ? (
        <View style={styles.drawer}>
          <Pressable accessibilityLabel="Close menu" style={styles.scrim} onPress={() => setMenuOpen(false)} />
          <View style={styles.drawerPanel}>
            <View style={styles.drawerServer}>
              <Text style={styles.drawerServerName}>Server</Text>
              <Text style={styles.subtitle} numberOfLines={2}>{serverLabel}</Text>
            </View>
            {(['bridge', 'tasks', 'team', 'settings'] as Surface[]).map(item => (
              <Pressable
                key={item}
                accessibilityRole="link"
                style={[styles.drawerLink, surface === item && styles.drawerLinkOn]}
                onPress={() => { setSurface(item); setMenuOpen(false); }}
              >
                <Text style={surface === item ? styles.drawerLinkTextOn : styles.drawerLinkText}>{titleFor(item)}</Text>
              </Pressable>
            ))}
          </View>
        </View>
      ) : null}
    </View>
  );
}

function titleFor(surface: Surface): string {
  if (surface === 'bridge') return 'Bridge';
  if (surface === 'tasks') return 'Tasks';
  if (surface === 'team') return 'Team';
  return 'Settings';
}

function Bridge({ workspaces, loading, error, onRetry }: {
  workspaces: Workspace[];
  loading: boolean;
  error: string | null;
  onRetry: () => void;
}) {
  const failed = useMemo(() => workspaces.filter(item => item.state === 'Failed'), [workspaces]);

  if (error) return <Empty title="Could not reach the Server" note={error} action="Retry" onPress={onRetry} />;
  if (loading && !workspaces.length) return <Empty title="Loading" note="Reading the team from the Server." />;
  if (!workspaces.length) return <Empty title="No agents yet" note="Register a workspace and it becomes an agent's own space." />;

  return (
    <>
      <Section title={`HALTED · ${failed.length}`} tone={failed.length ? 'danger' : 'muted'} />
      {failed.length
        ? failed.map(item => (
          <View key={item.id} style={[styles.card, styles.cardDanger]}>
            <Text style={styles.cardTitle}>{item.displayName} stopped</Text>
            <Text style={styles.note}>The workspace reports Failed. Open it on the desktop for the diagnostic.</Text>
          </View>
        ))
        : <Text style={styles.note}>Nothing halted.</Text>}

      <Section title={`TEAM · ${workspaces.length}`} tone="muted" />
      {workspaces.map(item => (
        <View key={item.id} style={styles.card}>
          <Text style={styles.cardTitle}>{item.displayName}</Text>
          <Text style={styles.note}>{item.state}</Text>
        </View>
      ))}

      <View style={styles.degrade}>
        <Text style={styles.degradeTitle}>Approval gates are not here yet</Text>
        <Text style={styles.note}>
          Triage needs one read across every agent, which this Server does not answer yet. Until it does,
          this screen shows what the workspace list reports rather than inventing a queue.
        </Text>
      </View>
    </>
  );
}

function Tasks() {
  return (
    <Empty
      title="No task source connected"
      note="Tasks mirror the issues assigned to this Server on its repository host. Connect a host and enable a forge capability to fill this screen."
    />
  );
}

function Team({ workspaces, loading }: { workspaces: Workspace[]; loading: boolean }) {
  if (loading && !workspaces.length) return <Empty title="Loading" note="Reading the team from the Server." />;
  if (!workspaces.length) return <Empty title="No agents yet" note="Each workspace on this Server is one agent's own space." />;
  return (
    <>
      {workspaces.map(item => (
        <View key={item.id} style={styles.card}>
          <Text style={styles.cardTitle}>{item.displayName}</Text>
          <Text style={styles.note}>{item.state}</Text>
        </View>
      ))}
    </>
  );
}

function Settings({ onSignOut }: { onSignOut: () => void }) {
  return (
    <>
      <Section title="INTERFACE" tone="muted" />
      <InterfaceToggleRow />
      <Section title="SERVER" tone="muted" />
      <Pressable accessibilityRole="button" style={[styles.card, styles.cardAction]} onPress={onSignOut}>
        <Text style={styles.cardTitleDanger}>Sign out of this server</Text>
        <Text style={styles.note}>Disconnects this client. Agents keep running on the Server.</Text>
      </Pressable>
    </>
  );
}

function Section({ title, tone }: { title: string; tone: 'muted' | 'danger' }) {
  return <Text style={[styles.section, tone === 'danger' && styles.sectionDanger]}>{title}</Text>;
}

function Empty({ title, note, action, onPress }: { title: string; note: string; action?: string; onPress?: () => void }) {
  return (
    <View style={styles.card}>
      <Text style={styles.cardTitle}>{title}</Text>
      <Text style={styles.note}>{note}</Text>
      {action && onPress ? (
        <Pressable accessibilityRole="button" style={styles.button} onPress={onPress}>
          <Text style={styles.buttonText}>{action}</Text>
        </Pressable>
      ) : null}
    </View>
  );
}

function TabItem({ label, active, onPress }: { label: string; active: boolean; onPress: () => void }) {
  return (
    <Pressable accessibilityRole="tab" accessibilityState={{ selected: active }} style={styles.tabItem} onPress={onPress}>
      <Text style={active ? styles.tabTextOn : styles.tabText}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  shell: { flex: 1, backgroundColor: agentUpTheme.colors.canvas },
  head: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    paddingHorizontal: agentUpTheme.spacing[4],
    paddingVertical: agentUpTheme.spacing[3],
    borderBottomWidth: 1,
    borderBottomColor: agentUpTheme.colors.borderSubtle,
  },
  headBody: { flex: 1, minWidth: 0 },
  iconButton: { width: 44, height: 44, alignItems: 'center', justifyContent: 'center', marginLeft: -10 },
  iconGlyph: { color: agentUpTheme.colors.textSecondary, fontSize: 20 },
  title: { color: agentUpTheme.colors.textPrimary, fontSize: 20, fontWeight: '700' },
  subtitle: { color: agentUpTheme.colors.textMuted, fontSize: 12 },
  body: { padding: agentUpTheme.spacing[4], gap: 10, paddingBottom: agentUpTheme.spacing[8] },
  section: { color: agentUpTheme.colors.textMuted, fontSize: 11, fontWeight: '700', letterSpacing: 1, marginTop: 6 },
  sectionDanger: { color: agentUpTheme.colors.textDanger },
  card: {
    borderWidth: 1,
    borderColor: agentUpTheme.colors.borderSubtle,
    borderRadius: 12,
    backgroundColor: agentUpTheme.colors.surface,
    padding: agentUpTheme.spacing[4],
    gap: 4,
  },
  cardDanger: { borderColor: agentUpTheme.colors.borderDanger, backgroundColor: agentUpTheme.colors.surfaceDanger },
  cardAction: { minHeight: 44 },
  cardTitle: { color: agentUpTheme.colors.textPrimary, fontSize: 15, fontWeight: '600' },
  cardTitleDanger: { color: agentUpTheme.colors.textDanger, fontSize: 15, fontWeight: '600' },
  note: { color: agentUpTheme.colors.textMuted, fontSize: 13, lineHeight: 19 },
  degrade: {
    borderWidth: 1,
    borderStyle: 'dashed',
    borderColor: agentUpTheme.colors.borderStrong,
    borderRadius: 12,
    padding: agentUpTheme.spacing[4],
    gap: 4,
    marginTop: 6,
  },
  degradeTitle: { color: agentUpTheme.colors.textPrimary, fontSize: 14, fontWeight: '600' },
  button: {
    minHeight: 44,
    marginTop: 10,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 8,
    borderWidth: 1,
    borderColor: agentUpTheme.colors.borderSelected,
    backgroundColor: agentUpTheme.colors.surfaceSelected,
  },
  buttonText: { color: agentUpTheme.colors.accentSoft, fontSize: 13, fontWeight: '600' },
  tabBar: {
    flexDirection: 'row',
    borderTopWidth: 1,
    borderTopColor: agentUpTheme.colors.borderSubtle,
    backgroundColor: agentUpTheme.colors.surface,
  },
  tabItem: { flex: 1, minHeight: 52, alignItems: 'center', justifyContent: 'center' },
  tabText: { color: agentUpTheme.colors.textFaint, fontSize: 12 },
  tabTextOn: { color: agentUpTheme.colors.textPrimary, fontSize: 12, fontWeight: '600' },
  drawer: { position: 'absolute', top: 0, right: 0, bottom: 0, left: 0, flexDirection: 'row' },
  scrim: { position: 'absolute', top: 0, right: 0, bottom: 0, left: 0, backgroundColor: agentUpTheme.colors.scrim },
  drawerPanel: {
    width: 272,
    maxWidth: '82%',
    backgroundColor: agentUpTheme.colors.surface,
    borderRightWidth: 1,
    borderRightColor: agentUpTheme.colors.borderSubtle,
    padding: agentUpTheme.spacing[3],
    gap: 2,
  },
  drawerServer: {
    padding: agentUpTheme.spacing[3],
    marginBottom: agentUpTheme.spacing[3],
    borderWidth: 1,
    borderColor: agentUpTheme.colors.borderSubtle,
    borderRadius: 10,
    backgroundColor: agentUpTheme.colors.surfaceRaised,
    gap: 2,
  },
  drawerServerName: { color: agentUpTheme.colors.textPrimary, fontSize: 13, fontWeight: '700' },
  drawerLink: { minHeight: 44, justifyContent: 'center', paddingHorizontal: agentUpTheme.spacing[3], borderRadius: 8 },
  drawerLinkOn: { backgroundColor: agentUpTheme.colors.surfaceSelected },
  drawerLinkText: { color: agentUpTheme.colors.textMuted, fontSize: 15 },
  drawerLinkTextOn: { color: agentUpTheme.colors.textPrimary, fontSize: 15, fontWeight: '600' },
});
