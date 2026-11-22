import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { createNewUiScene } from '@agent-up/design-system/new-ui-scene';
import { WorkspaceAgentChatScreen } from '@/features/agents/components/WorkspaceAgentChatScreen';
import { ApplicationSpaceScreen } from '@/features/applications/components/ApplicationSpaceScreen';
import { FileEditorScreen } from '@/features/files/components/FileEditorScreen';
import { FilesScreen } from '@/features/files/components/FilesScreen';
import { GitOverviewScreen } from '@/features/git/components/GitOverviewScreen';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { AppShellProvider } from '@/features/shell/controllers/AppShellContext';
import { WorkspaceDashboardScreen } from '@/features/workspaces/components/WorkspaceDashboardScreen';
import { WorkspaceHostProvider, type WorkspaceHost, type WorkspacePlace } from '@/features/workspaces/controllers/WorkspaceHostContext';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import { useInterfaceChoice } from '../controllers/InterfaceContext';
import { useTaskQueue } from '../controllers/TaskQueueContext';
import { useTeamStanding } from '../controllers/TeamStandingContext';
import { useTaskSource } from '../controllers/TaskSourceContext';
import type { CapabilityModule } from '@/features/capabilities/models/CapabilityModule';
import {
  disableCapabilityModule,
  enableCapabilityModule,
  listCapabilityModules,
} from '@/features/capabilities/providers/CapabilityModulesApiProvider';
import { SceneIcon } from './SceneIcon';
import { runTaskAction } from '../providers/TaskActionProvider';
import {
  bindAgent,
  enableAgentCapability,
  getAgent,
  getAutonomy,
  putAutonomy,
  putLoopActivation,
  scheduleAgent,
  type AgentSession,
  type ServerAutonomy,
} from '@agent-up/chat';
import { chatSettingsFields, settingsWorkspaces } from '../providers/ChatSettingsProvider';
import {
  commandCenterScene,
  commandCenterServerLabel,
  tasksTabFor,
  type TasksTab,
} from '../providers/CommandCenterSceneProvider';

const Scene = createNewUiScene(React, { View, Text, Pressable, ScrollView, Icon: SceneIcon });

type Section = 'overview' | 'chat' | 'git' | 'files' | 'file' | 'application';

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
  const [tasksTab, setTasksTab] = useState<TasksTab>('issues');
  const [section, setSection] = useState<Section>('overview');
  const [openAgent, setOpenAgent] = useState<string | null>(null);
  // The file opened from Files, and a request for its page to leave, which it answers by asking
  // before it discards an edit.
  const [openFile, setOpenFile] = useState<string | null>(null);
  const [leaveFile, setLeaveFile] = useState(0);
  const [modules, setModules] = useState<CapabilityModule[]>([]);
  const [modulesNonce, setModulesNonce] = useState(0);
  const source = useTaskSource();
  const [settingsWorkspace, setSettingsWorkspace] = useState<string | null>(null);
  const [chatSession, setChatSession] = useState<AgentSession | null>(null);
  const [chatAutonomy, setChatAutonomy] = useState<ServerAutonomy | null>(null);
  const [chatNonce, setChatNonce] = useState(0);
  const chatWorkspace = settingsWorkspace ?? standing.team[0]?.workspaceId ?? null;

  // Settings · Chat reads the selected workspace's agent and loop when Settings is shown.
  useEffect(() => {
    if (!activeServer || nav !== 2 || openAgent !== null || !chatWorkspace) return;
    let live = true;
    Promise.all([getAgent(activeServer, chatWorkspace), getAutonomy(activeServer, chatWorkspace)])
      .then(([session, autonomy]) => { if (live) { setChatSession(session); setChatAutonomy(autonomy); } })
      .catch(() => { if (live) { setChatSession(null); setChatAutonomy(null); } });
    return () => { live = false; };
  }, [activeServer, nav, openAgent, chatWorkspace, chatNonce]);

  const changeChat = (change: Promise<unknown>) => {
    void change.catch(() => undefined).finally(() => setChatNonce(current => current + 1));
  };

  // Settings lists the Server's own modules, read when Settings is shown and after every change.
  useEffect(() => {
    if (!activeServer || nav !== 2 || openAgent !== null) return;
    let live = true;
    listCapabilityModules(activeServer)
      .then(listed => { if (live) setModules(listed); })
      .catch(() => { if (live) setModules([]); });
    return () => { live = false; };
  }, [activeServer, nav, openAgent, modulesNonce]);

  const changeModule = (id: string | null, enable: boolean) => {
    const module = modules.find(item => item.id === id);
    if (!activeServer || !module) return;
    const change = enable
      ? enableCapabilityModule(activeServer, module.id)
      : disableCapabilityModule(activeServer, module.id);
    void change.catch(() => undefined).finally(() => setModulesNonce(current => current + 1));
  };

  const agent = openAgent === null ? null : standing.team.find(item => item.workspaceId === openAgent) ?? null;
  const workspace = agent ? workspaces.find(item => item.id === agent.workspaceId) ?? null : null;
  const applicationName = workspace?.applications?.[0]?.name ?? '';


  const model = commandCenterScene({
    surface: 'mobile',
    nav,
    section,
    agentOpen: agent !== null,
    serverLabel: commandCenterServerLabel(activeServer),
    openWorkspaceId: agent?.workspaceId ?? null,
    openBranch: workspace?.branch ?? '',
    openFile: openFile ?? undefined,
    applicationName,
    team: standing.team,
    queue: tasks.queue,
    working: tasks.working,
    offered: tasks.offered,
    names: tasks.names,
    tasksTab,
    // The application page carries the client's own dock, which replays a flow for real.
    flows: [],
    modules,
    chat: {
      workspaces: settingsWorkspaces(standing.team, chatWorkspace),
      fields: chatSettingsFields({
        autonomy: chatAutonomy,
        loop: chatSession?.loop,
        agents: chatSession?.agents ?? [],
        boundAgent: chatSession?.boundAgent,
      }),
    },
    validationsOpen: false,
    validationRunning: false,
    validationPassed: false,
    validationSummary: '',
  });

  const host = useCallback((): WorkspaceHost => ({
    open: (place: WorkspacePlace) => {
      if (place.kind === 'git' || place.kind === 'gitHistory' || place.kind === 'gitReview') setSection('git');
      else if (place.kind === 'files') setSection('files');
      else if (place.kind === 'file') { setOpenFile(place.path); setSection('file'); }
      else if (place.kind === 'agent' || place.kind === 'agentPolicy' || place.kind === 'agentCreate') setSection('chat');
      else if (place.kind === 'application') setSection('application');
      else setSection('overview');
    },
    back: fallback => setSection(fallback.kind === 'files' ? 'files' : 'overview'),
    showsTabBar: false,
  }), []);
  const provide = useMemo(() => () => host(), [host]);

  const content: Record<string, React.ReactNode> = {};
  if (model.scene === 'mobile-application' && workspace && applicationName) {
    content.application = (
      <ApplicationSpaceScreen workspace={workspace} applicationName={applicationName} />
    );
    content.dock = <View />;
  }
  if (model.scene === 'mobile-chat' && agent && activeServer) {
    content.transcript = (
      <WorkspaceAgentChatScreen workspaceId={agent.workspaceId} displayName={agent.displayName} server={activeServer} loopControls={false} />
    );
  }
  if (model.scene === 'mobile-agent-git' && agent) {
    content.changes = <GitOverviewScreen workspaceId={agent.workspaceId} />;
  }
  if (model.scene === 'mobile-agent-files' && agent) {
    content.files = <FilesScreen workspaceId={agent.workspaceId} />;
  }
  if (model.scene === 'mobile-agent-file' && agent && openFile) {
    content.file = <FileEditorScreen workspaceId={agent.workspaceId} path={openFile} leaveRequest={leaveFile} />;
  }
  if (model.scene === 'mobile-agent-overview' && workspace) {
    content.overview = <WorkspaceDashboardScreen workspace={workspace} />;
  }

  const onAction = (name: string, id: string | null) => {
    if (name === 'nav-team') { setOpenAgent(null); setNav(0); return; }
    if (name === 'nav-tasks') { setOpenAgent(null); setNav(1); return; }
    if (name === 'nav-settings') { setOpenAgent(null); setNav(2); return; }
    const tab = tasksTabFor(name);
    if (tab) { setTasksTab(tab); return; }
    // An application is an inner page of its agent: back returns to Apps, not to the team.
    if (name === 'back' && section === 'application') { setSection('overview'); return; }
    // A file is an inner page of Files; its own page decides whether leaving needs a word first.
    if (name === 'back' && section === 'file') { setLeaveFile(current => current + 1); return; }
    if (name === 'back') { setOpenAgent(null); setSection('overview'); return; }
    if (name === 'tab-overview' || name === 'tab-apps') { setSection('overview'); return; }
    if (name === 'tab-chat') { setSection('chat'); return; }
    if (name === 'tab-git') { setSection('git'); return; }
    if (name === 'tab-files') { setSection('files'); return; }
    if (name === 'tab-application') { setSection('application'); return; }
    if (name === 'open-workspace' && id) { setOpenAgent(id); setSection('overview'); return; }
    if (name === 'logout') { logout(); return; }
    if (name === 'use-classic') { choose('classic'); return; }
    if (name === 'reload') { refresh(); return; }
    if (name === 'connect-source') { void source.connect(); return; }
    if (name === 'enable-module') { changeModule(id, true); return; }
    if (name === 'disable-module') { changeModule(id, false); return; }
    if (runTaskAction(name, id, tasks)) return;
    if (name === 'settings-workspace' && id) { setSettingsWorkspace(id); return; }
    if (activeServer && chatWorkspace) {
      if (name === 'enable-autonomy') { changeChat(putAutonomy(activeServer, true)); return; }
      if (name === 'activate-loop') { changeChat(putLoopActivation(activeServer, chatWorkspace, true)); return; }
      if (name === 'cancel-loop') { changeChat(putLoopActivation(activeServer, chatWorkspace, false)); return; }
      if (name === 'select-agent' && id) { changeChat(bindAgent(activeServer, chatWorkspace, id)); return; }
      if (name === 'enable-agent' && id) { changeChat(enableAgentCapability(activeServer, id)); return; }
      if (name === 'start-bound' && chatSession?.boundAgent) { changeChat(scheduleAgent(activeServer, chatWorkspace, chatSession.boundAgent)); return; }
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
