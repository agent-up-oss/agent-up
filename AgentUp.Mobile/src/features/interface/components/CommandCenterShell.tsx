import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { createNewUiScene } from '@agent-up/design-system/new-ui-scene';
import { WorkspaceAgentChatScreen } from '@/features/agents/components/WorkspaceAgentChatScreen';
import { ApplicationSpaceScreen } from '@/features/applications/components/ApplicationSpaceScreen';
import { GitOverviewScreen } from '@/features/git/components/GitOverviewScreen';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { AppShellProvider } from '@/features/shell/controllers/AppShellContext';
import { listValidationFlows } from '@/features/validation/providers/ValidationFlowApiProvider';
import { WorkspaceDashboardScreen } from '@/features/workspaces/components/WorkspaceDashboardScreen';
import { WorkspaceHostProvider, type WorkspaceHost, type WorkspacePlace } from '@/features/workspaces/controllers/WorkspaceHostContext';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import { useInterfaceChoice } from '../controllers/InterfaceContext';
import { useTaskQueue } from '../controllers/TaskQueueContext';
import { useTeamStanding } from '../controllers/TeamStandingContext';
import {
  commandCenterScene,
  type CommandCenterFlowSlot,
} from '../providers/CommandCenterSceneProvider';

const Scene = createNewUiScene(React, { View, Text, Pressable, ScrollView });

type Section = 'overview' | 'chat' | 'git' | 'application';

/**
 * The command center is the generated New UI scene. This host selects that scene and
 * fills its slots. It does not draw a second page.
 */
export function CommandCenterShell() {
  return (
    <AppShellProvider>
      <CommandCenterBinder />
    </AppShellProvider>
  );
}

function CommandCenterBinder() {
  const standing = useTeamStanding();
  const tasks = useTaskQueue();
  const { activeServer, logout } = useServers();
  const { choose } = useInterfaceChoice();
  const { workspaces, refresh } = useWorkspaces();
  const [nav, setNav] = useState<0 | 1 | 2>(0);
  const [section, setSection] = useState<Section>('overview');
  const [openAgent, setOpenAgent] = useState<string | null>(null);
  const [flows, setFlows] = useState<CommandCenterFlowSlot[]>([]);
  const [validationsOpen, setValidationsOpen] = useState(false);
  const [validationRunning, setValidationRunning] = useState(false);
  const [validationPassed, setValidationPassed] = useState(false);
  const [validationSummary, setValidationSummary] = useState('No check selected');

  const agent = openAgent === null ? null : standing.team.find(item => item.workspaceId === openAgent) ?? null;
  const workspace = agent ? workspaces.find(item => item.id === agent.workspaceId) ?? null : null;
  const applicationName = workspace?.applications?.[0]?.name ?? '';

  useEffect(() => {
    if (!activeServer || !workspace || !applicationName || section !== 'application') return;
    let live = true;
    listValidationFlows(activeServer, workspace.id, applicationName)
      .then(listed => {
        if (!live) return;
        setFlows(listed.map(flow => ({ id: flow.id, name: flow.name, description: flow.description })));
      })
      .catch(() => {
        if (live) setFlows([]);
      });
    return () => {
      live = false;
    };
  }, [activeServer, workspace, applicationName, section]);

  const model = commandCenterScene({
    surface: 'mobile',
    nav,
    section,
    agentOpen: agent !== null,
    serverLabel: activeServer?.url ?? 'No server',
    openWorkspaceId: agent?.workspaceId ?? null,
    team: standing.team,
    queue: tasks.queue,
    working: tasks.working,
    offered: tasks.offered,
    names: tasks.names,
    flows,
    validationsOpen,
    validationRunning,
    validationPassed,
    validationSummary,
  });

  const host = useCallback((): WorkspaceHost => ({
    open: (place: WorkspacePlace) => {
      if (place.kind === 'git' || place.kind === 'gitHistory' || place.kind === 'gitReview') setSection('git');
      else if (place.kind === 'agent' || place.kind === 'agentPolicy' || place.kind === 'agentCreate') setSection('chat');
      else if (place.kind === 'application') setSection('application');
      else setSection('overview');
    },
    back: () => setSection('overview'),
    showsTabBar: false,
  }), []);
  const provide = useMemo(() => () => host(), [host]);

  const content: Record<string, React.ReactNode> = {};
  if (model.scene === 'mobile-application' && workspace && applicationName) {
    content.application = (
      <ApplicationSpaceScreen workspace={workspace} applicationName={applicationName} showValidation={false} />
    );
  }
  if (model.scene === 'mobile-chat' && agent && activeServer) {
    content.transcript = (
      <WorkspaceAgentChatScreen workspaceId={agent.workspaceId} displayName={agent.displayName} server={activeServer} />
    );
  }
  if (model.scene === 'mobile-agent-git' && agent) {
    content.changes = <GitOverviewScreen workspaceId={agent.workspaceId} />;
  }
  if (model.scene === 'mobile-agent-overview' && workspace) {
    content.overview = <WorkspaceDashboardScreen workspace={workspace} />;
  }

  const onAction = (name: string, id: string | null) => {
    if (name === 'nav-team') { setOpenAgent(null); setNav(0); return; }
    if (name === 'nav-tasks') { setOpenAgent(null); setNav(1); return; }
    if (name === 'nav-settings') { setOpenAgent(null); setNav(2); return; }
    if (name === 'back') { setOpenAgent(null); setSection('overview'); return; }
    if (name === 'tab-overview') { setSection('overview'); return; }
    if (name === 'tab-chat') { setSection('chat'); return; }
    if (name === 'tab-git') { setSection('git'); return; }
    if (name === 'tab-application') { setSection('application'); return; }
    if (name === 'open-workspace' && id) { setOpenAgent(id); setSection('overview'); return; }
    if (name === 'logout') { logout(); return; }
    if (name === 'use-classic') { choose('classic'); return; }
    if (name === 'reload') { refresh(); return; }
    if (name === 'open-validations') { setValidationsOpen(open => !open); return; }
    if (name === 'dequeue') { const task = tasks.queue.find(item => item.id === id); if (task) tasks.remove(task); return; }
    if (name === 'stop') { const task = tasks.working.find(item => item.id === id); if (task) tasks.stop(task); return; }
    if (name === 'queue') { const issue = tasks.offered.find(item => item.id === id); if (issue) tasks.pick(issue); return; }
    if (name === 'run') {
      const flow = flows.find(item => item.id === id);
      setValidationsOpen(false);
      setValidationSummary(flow?.name ?? 'No check selected');
      setValidationRunning(true);
      setValidationPassed(false);
      return;
    }
  };

  return (
    <WorkspaceHostProvider host={provide}>
      <View style={{ flex: 1 }}>
        <Scene
          scene={model.scene}
          text={model.text}
          items={model.items}
          state={model.state}
          content={content}
          onAction={onAction}
        />
      </View>
    </WorkspaceHostProvider>
  );
}
