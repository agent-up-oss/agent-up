import { useCallback, useMemo, useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { AgentCreationScreen } from '@/features/agents/components/AgentCreationScreen';
import { AgentPermissionPolicyScreen } from '@/features/agents/components/AgentPermissionPolicyScreen';
import { AgentsOverviewScreen } from '@/features/agents/components/AgentsOverviewScreen';
import { WorkspaceAgentChatScreen } from '@/features/agents/components/WorkspaceAgentChatScreen';
import { ApplicationSpaceScreen } from '@/features/applications/components/ApplicationSpaceScreen';
import { GitHistoryScreen } from '@/features/git/components/GitHistoryScreen';
import { GitOverviewScreen } from '@/features/git/components/GitOverviewScreen';
import { GitReviewScreen } from '@/features/git/components/GitReviewScreen';
import { AppShellProvider, useAppShell } from '@/features/shell/controllers/AppShellContext';
import { WorkspaceDashboardScreen } from '@/features/workspaces/components/WorkspaceDashboardScreen';
import { WorkspaceSettingsScreen } from '@/features/workspaces/components/WorkspaceSettingsScreen';
import {
  WorkspaceHostProvider,
  type WorkspaceHost,
  type WorkspacePlace,
} from '@/features/workspaces/controllers/WorkspaceHostContext';
import type { ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { Workspace } from '@/features/workspaces/models/Workspace';
import { agentUpTheme, auBox } from '@agent-up/design-system/native';
import type { TeamStanding } from '../models/TeamStanding';

/** The tabs an agent's workspace opens on, which are the workspace's own tabs. */
const tabs = [
  { place: 'apps', label: 'Apps' },
  { place: 'git', label: 'Git' },
  { place: 'agents', label: 'Agent' },
  { place: 'settings', label: 'Settings' },
] as const;

type TabPlace = (typeof tabs)[number]['place'];

/**
 * One agent's workspace, read from the inside.
 *
 * Nothing here is a second version of a workspace screen: these are the screens the classic shell
 * opens, hosted under the agent's own tabs. They reach the same Server through the same providers,
 * so what works in one shell works in the other, including everything they open on top of
 * themselves.
 */
export function AgentPage(props: {
  standing: TeamStanding;
  workspace: Workspace | null;
  server: ServerSession | null;
  onBack: () => void;
  /** How far the phone's own chrome reaches down the screen, so the head starts below it. */
  topInset?: number;
}) {
  return (
    <AppShellProvider>
      <AgentPageBody {...props} />
    </AppShellProvider>
  );
}

function AgentPageBody({
  standing,
  workspace,
  server,
  onBack,
  topInset = 0,
}: {
  standing: TeamStanding;
  workspace: Workspace | null;
  server: ServerSession | null;
  onBack: () => void;
  topInset?: number;
}) {
  const [tab, setTab] = useState<TabPlace>('apps');
  // What the tab has opened on top of itself, innermost last. A workspace screen that opens
  // another is the same gesture here as a route push is in the classic shell.
  const [stack, setStack] = useState<WorkspacePlace[]>([]);
  const { config } = useAppShell();

  const host = useCallback((): WorkspaceHost => ({
    open: (place, options) => {
      if (isTab(place)) {
        setTab(place.kind);
        setStack([]);
        return;
      }
      setStack(current => (options?.replace ? [...current.slice(0, -1), place] : [...current, place]));
    },
    back: fallback => {
      setStack(current => {
        if (current.length > 0) return current.slice(0, -1);
        if (isTab(fallback)) setTab(fallback.kind);
        return current;
      });
    },
    // The agent page carries its own tabs above, so the workspace's own row would be a second one.
    showsTabBar: false,
  }), []);

  const provide = useMemo(() => () => host(), [host]);
  const place: WorkspacePlace = stack.at(-1) ?? { kind: tab };
  const deep = stack.length > 0;

  // A screen opened on top names itself; the agent names the page otherwise.
  const title = deep && config.title ? config.title : standing.displayName;
  // The page owns its own stack, so going back pops it rather than trusting the screen's own
  // back action, which in the classic shell is a route this shell does not use.
  const goBack = deep ? () => setStack(current => current.slice(0, -1)) : onBack;

  return (
    <WorkspaceHostProvider host={provide}>
      <View style={styles.page}>
        <View style={[styles.head, { paddingTop: topInset + agentUpTheme.spacing[3] }]}>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={deep ? 'Go back' : 'Back to the team'}
            onPress={goBack}
            style={styles.backButton}
          >
            <Text style={styles.backGlyph}>‹</Text>
          </Pressable>
          <View style={styles.headBody}>
            <Text style={styles.title} numberOfLines={1}>{title}</Text>
            <Text style={styles.subtitle} numberOfLines={1}>
              {deep ? standing.displayName : subtitle(standing, workspace)}
            </Text>
          </View>
          {config.rightAction ? (
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={config.rightAction.accessibilityLabel ?? config.rightAction.label}
              testID={config.rightAction.testID}
              onPress={config.rightAction.onPress}
              style={styles.headAction}
            >
              <Text style={styles.headActionText}>{config.rightAction.label}</Text>
            </Pressable>
          ) : null}
        </View>

        {deep ? null : (
          <View style={styles.tabs}>
            {tabs.map(item => (
              <Pressable
                key={item.place}
                accessibilityRole="tab"
                accessibilityLabel={item.label}
                accessibilityState={{ selected: tab === item.place }}
                style={[styles.tab, tab === item.place && styles.tabOn]}
                onPress={() => { setTab(item.place); setStack([]); }}
              >
                <Text style={[styles.tabText, tab === item.place && styles.tabTextOn]}>{item.label}</Text>
              </Pressable>
            ))}
          </View>
        )}

        <View style={styles.body}>
          <Screen place={place} standing={standing} workspace={workspace} server={server} />
        </View>
      </View>
    </WorkspaceHostProvider>
  );
}

/** The workspace screen one place names, which is the screen the classic shell opens for it. */
function Screen({
  place,
  standing,
  workspace,
  server,
}: {
  place: WorkspacePlace;
  standing: TeamStanding;
  workspace: Workspace | null;
  server: ServerSession | null;
}) {
  const workspaceId = standing.workspaceId;

  switch (place.kind) {
    case 'apps':
      return workspace ? <WorkspaceDashboardScreen workspace={workspace} /> : <Missing />;
    case 'git':
      return <GitOverviewScreen workspaceId={workspaceId} />;
    case 'agents':
      return <AgentsOverviewScreen workspaceId={workspaceId} />;
    case 'settings':
      return <WorkspaceSettingsScreen workspaceId={workspaceId} />;
    case 'application':
      return workspace
        ? <ApplicationSpaceScreen workspace={workspace} applicationName={place.name} />
        : <Missing />;
    case 'gitHistory':
      return <GitHistoryScreen workspaceId={workspaceId} />;
    case 'gitReview':
      return <GitReviewScreen workspaceId={workspaceId} />;
    case 'agent':
      return (
        <WorkspaceAgentChatScreen
          workspaceId={workspaceId}
          displayName={standing.displayName}
          server={server}
        />
      );
    case 'agentPolicy':
      return <AgentPermissionPolicyScreen workspaceId={workspaceId} />;
    case 'agentCreate':
      return <AgentCreationScreen workspaceId={workspaceId} />;
  }
}

/** The Server has not answered with this workspace yet, so there is nothing to draw it from. */
function Missing() {
  return (
    <View style={styles.missing}>
      <Text style={styles.subtitle}>This workspace is not on the Server yet.</Text>
    </View>
  );
}

function isTab(place: WorkspacePlace): place is { kind: TabPlace } {
  return tabs.some(tab => tab.place === place.kind);
}

/** What the agent is, under their name: what they are doing, and where. */
function subtitle(standing: TeamStanding, workspace: Workspace | null): string {
  const parts = [standing.agent, workspace?.branch ?? standing.branch].filter(
    (part): part is string => typeof part === 'string' && part.length > 0,
  );
  return parts.join(' · ');
}

const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: agentUpTheme.colors.canvas },
  head: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    paddingHorizontal: agentUpTheme.spacing[4],
    paddingVertical: agentUpTheme.spacing[3],
    borderBottomWidth: 1,
    borderBottomColor: agentUpTheme.colors.borderSubtle,
  },
  backButton: { width: 36, height: 44, alignItems: 'flex-start', justifyContent: 'center', marginLeft: -6 },
  backGlyph: { color: agentUpTheme.colors.textSecondary, fontSize: 30, lineHeight: 32 },
  headBody: { flex: 1, minWidth: 0 },
  headAction: { paddingHorizontal: agentUpTheme.spacing[2], paddingVertical: 6 },
  headActionText: { color: agentUpTheme.colors.accentBright, fontSize: 13, fontWeight: '600' },
  title: { color: agentUpTheme.colors.textPrimary, fontSize: 20, fontWeight: '700' },
  subtitle: { color: agentUpTheme.colors.textMuted, fontSize: 12 },
  tabs: {
    ...auBox('tabs'),
    flexDirection: 'row',
    gap: 6,
    marginHorizontal: agentUpTheme.spacing[4],
    marginTop: agentUpTheme.spacing[3],
  },
  tab: auBox('tab'),
  tabOn: auBox('tab', 'tabSelected'),
  tabText: { color: agentUpTheme.colors.textMuted, fontSize: 13, fontWeight: '600' },
  tabTextOn: { color: agentUpTheme.colors.textPrimary },
  body: { flex: 1 },
  missing: { padding: agentUpTheme.spacing[4] },
});
