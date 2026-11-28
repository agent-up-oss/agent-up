import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Pressable, ScrollView, Text, TextInput, View } from 'react-native';
import { createNewUiScene } from '@agent-up/design-system/new-ui-scene';
import { WorkspaceAgentChatScreen } from '@/features/agents/components/WorkspaceAgentChatScreen';
import { ApplicationSpaceScreen } from '@/features/applications/components/ApplicationSpaceScreen';
import { FileEditorScreen } from '@/features/files/components/FileEditorScreen';
import { FilesScreen } from '@/features/files/components/FilesScreen';
import { GitOverviewScreen } from '@/features/git/components/GitOverviewScreen';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { useReadingFrame } from '@/features/shell/components/ReadingFrame';
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
  updateCapabilitySettings,
} from '@/features/capabilities/providers/CapabilityModulesApiProvider';
import { MemberSheet } from './MemberSheet';
import { SceneIcon } from './SceneIcon';
import { runTaskAction } from '../providers/TaskActionProvider';
import { pickIssue } from '../providers/TaskQueueProvider';
import { readRepositoryIssues, readRepositoryPulls } from '../providers/TaskSourceProvider';
import { forgeHostLabel, repositoryId, type TaskSourcePull } from '../models/TaskSource';
import type { OfferedIssue } from '../models/TaskQueue';
import {
  bindAgent,
  enableAgentCapability,
  getAgent,
  getAutonomy,
  getMember,
  putLoopActivation,
  putMember,
  putAutonomy,
  scheduleAgent,
  type AgentSession,
  type ServerAutonomy,
  type TeamMember,
} from '@agent-up/chat';
import { chatSettingsFields, settingsWorkspaces } from '../providers/ChatSettingsProvider';
import {
  agentSettingSave,
  applyMemberIfCurrent,
  commandCenterScene,
  commandCenterServerLabel,
  memberKindChange,
  prefillEndpoint,
  promptSave,
  TASK_SOURCE_REFRESH_MS,
  tasksTabFor,
  tokenSave,
  type TasksTab,
} from '../providers/CommandCenterSceneProvider';

const Scene = createNewUiScene(React, { View, Text, Pressable, ScrollView, Icon: SceneIcon, TextInput });
// The team stays drawn under an open member's sheet. It is a scene of its own, so the drafts a
// member's fields keep are never shared with the page underneath.
const TeamScene = createNewUiScene(React, { View, Text, Pressable, ScrollView, Icon: SceneIcon, TextInput });

type Section = 'overview' | 'chat' | 'git' | 'files' | 'file' | 'settings' | 'application';

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
  const reading = useReadingFrame();
  const { activeServer, logout } = useServers();
  const { choose } = useInterfaceChoice();
  const { workspaces, refresh } = useWorkspaces();
  const [nav, setNav] = useState<0 | 1 | 2>(0);
  const [tasksTab, setTasksTab] = useState<TasksTab>('issues');
  const [authTab, setAuthTab] = useState<string | null>(null);
  const [forgeTab, setForgeTab] = useState('github');
  const [openRepo, setOpenRepo] = useState<{ moduleId: string; owner: string; name: string } | null>(null);
  const [repoIssues, setRepoIssues] = useState<OfferedIssue[]>([]);
  const [pulls, setPulls] = useState<TaskSourcePull[]>([]);
  const [repoNonce, setRepoNonce] = useState(0);
  const [section, setSection] = useState<Section>('overview');
  const [openAgent, setOpenAgent] = useState<string | null>(null);
  // A request for the open member's sheet to slide back down to the team.
  const [closeMember, setCloseMember] = useState(0);
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
  const [memberKind, setMemberKind] = useState('Author');
  const [systemPrompt, setSystemPrompt] = useState('');
  const [customizationPrompt, setCustomizationPrompt] = useState('');
  const [defaultSystemPrompt, setDefaultSystemPrompt] = useState('');
  const [promptOverwrite, setPromptOverwrite] = useState(false);
  const [pendingMemberKind, setPendingMemberKind] = useState<string | null>(null);
  const [memberLoadedFor, setMemberLoadedFor] = useState<string | null>(null);
  const [chatNonce, setChatNonce] = useState(0);
  const chatWorkspace = openAgent ?? settingsWorkspace ?? standing.team[0]?.workspaceId ?? null;
  const chatWorkspaceRef = useRef(chatWorkspace);
  chatWorkspaceRef.current = chatWorkspace;
  const authMember = openAgent
    ?? authTab
    ?? source.source?.identities?.find(item => item.connected)?.workspaceId
    ?? standing.team[0]?.workspaceId
    ?? null;
  const identity = source.source?.identities?.find(item => item.workspaceId === authMember) ?? null;
  const memberSettingsOpen = openAgent !== null && section === 'settings';

  const applyMember = (member: TeamMember | null) => {
    setMemberKind(member?.kind ?? 'Author');
    setSystemPrompt(member?.systemPrompt ?? '');
    setCustomizationPrompt(member?.customizationPrompt ?? '');
    setDefaultSystemPrompt(member?.defaultSystemPrompt ?? '');
    setPromptOverwrite(false);
    setPendingMemberKind(null);
  };

  // Member Settings reads this workspace's agent, loop, and kind.
  useEffect(() => {
    if (!activeServer || !chatWorkspace || !memberSettingsOpen) return;
    setMemberLoadedFor(null);
    setSystemPrompt('');
    setCustomizationPrompt('');
    setDefaultSystemPrompt('');
    setPromptOverwrite(false);
    setPendingMemberKind(null);
    let live = true;
    Promise.all([
      getAgent(activeServer, chatWorkspace),
      getAutonomy(activeServer, chatWorkspace),
      getMember(activeServer, chatWorkspace).catch(() => null),
    ])
      .then(([session, autonomy, member]) => {
        if (!live) return;
        setChatSession(session);
        setChatAutonomy(autonomy);
        applyMember(member);
        setMemberLoadedFor(chatWorkspace);
      })
      .catch(() => {
        if (!live) return;
        setChatSession(null);
        setChatAutonomy(null);
        applyMember(null);
        setMemberLoadedFor(chatWorkspace);
      });
    return () => { live = false; };
  }, [activeServer, chatWorkspace, chatNonce, memberSettingsOpen]);

  const refreshSource = source.refresh;
  useEffect(() => {
    const timer = setInterval(() => {
      refreshSource();
      setRepoNonce(current => current + 1);
    }, TASK_SOURCE_REFRESH_MS);
    return () => clearInterval(timer);
  }, [refreshSource]);

  const changeChat = (change: Promise<unknown>) => {
    void change.catch(() => undefined).finally(() => setChatNonce(current => current + 1));
  };

  // Instance Settings and member Settings both need the Server's modules.
  useEffect(() => {
    if (!activeServer || (nav !== 2 && !memberSettingsOpen)) return;
    let live = true;
    listCapabilityModules(activeServer)
      .then(listed => { if (live) setModules(listed); })
      .catch(() => { if (live) setModules([]); });
    return () => { live = false; };
  }, [activeServer, nav, memberSettingsOpen, modulesNonce]);

  const changeModule = (id: string | null, enable: boolean) => {
    const module = modules.find(item => item.id === id);
    if (!activeServer || !module) return;
    const change = enable
      ? enableCapabilityModule(activeServer, module.id)
      : disableCapabilityModule(activeServer, module.id);
    void change.catch(() => undefined).finally(() => setModulesNonce(current => current + 1));
  };

  useEffect(() => {
    if (!activeServer || !openRepo) {
      setRepoIssues([]);
      setPulls([]);
      return;
    }
    let live = true;
    Promise.all([
      readRepositoryIssues(activeServer, openRepo.moduleId, openRepo.owner, openRepo.name),
      readRepositoryPulls(activeServer, openRepo.moduleId, openRepo.owner, openRepo.name),
    ])
      .then(([issues, listed]) => { if (live) { setRepoIssues(issues); setPulls(listed); } })
      .catch(() => { if (live) { setRepoIssues([]); setPulls([]); } });
    return () => { live = false; };
  }, [activeServer, openRepo, repoNonce]);

  const agent = openAgent === null ? null : standing.team.find(item => item.workspaceId === openAgent) ?? null;
  const workspace = agent ? workspaces.find(item => item.id === agent.workspaceId) ?? null : null;
  const applicationName = workspace?.applications?.[0]?.name ?? '';
  const boundAgent = chatSession?.boundAgent ?? null;
  const boundModule = modules.find(module => module.id === boundAgent);
  const agentSetting = (key: string) => boundModule?.settings?.find(setting => setting.key === key)?.value ?? '';

  const sceneInput: Parameters<typeof commandCenterScene>[0] = {
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
    offered: openRepo ? repoIssues : tasks.offered,
    names: tasks.names,
    tasksTab,
    repository: openRepo ? `${openRepo.owner}/${openRepo.name}` : null,
    repositories: source.repositories.map(repository => ({
      id: repositoryId(repository),
      name: `${repository.owner}/${repository.name}`,
      host: forgeHostLabel(repository.moduleId),
      members: (repository.members ?? []).map(member => ({
        id: member.workspaceId,
        name: member.displayName,
        initials: member.initials,
      })),
    })),
    pulls: openRepo ? pulls : undefined,
    authTab: authMember ?? '',
    forgeTab,
    forgeDetail: identity && identity.moduleId === forgeTab
      ? identity.detail
      : identity
        ? `Paste the host URL and a personal access token for ${identity.displayName}. It needs access to repositories and pull requests.`
        : source.error ?? 'Add a workspace to give a team member their own access token.',
    forgeEndpoint: prefillEndpoint(
      forgeTab,
      identity?.moduleId === forgeTab ? identity.endpointUrl : null,
    ),
    forgeConnected: identity?.connected === true && identity.moduleId === forgeTab,
    memberKind,
    boundAgent,
    agentEndpoint: agentSetting('endpoint'),
    agentModel: agentSetting('model'),
    systemPrompt,
    customizationPrompt,
    promptOverwrite,
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
  };
  const model = commandCenterScene(sceneInput);
  // What the open member's sheet covers: the page the member was opened from.
  const team = agent ? commandCenterScene({ ...sceneInput, agentOpen: false, openWorkspaceId: null }) : model;

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

  const onAction = (name: string, id: string | null, text?: string, fields?: Record<string, string>) => {
    if (name === 'nav-team') { setOpenAgent(null); setNav(0); return; }
    if (name === 'nav-tasks') { setOpenAgent(null); setNav(1); return; }
    if (name === 'nav-settings') { setOpenAgent(null); setNav(2); return; }
    const tab = tasksTabFor(name);
    if (tab) { setTasksTab(tab); return; }
    // An application is an inner page of its agent: back returns to Apps, not to the team.
    if (name === 'back' && section === 'application') { setSection('overview'); return; }
    // A file is an inner page of Files; its own page decides whether leaving needs a word first.
    if (name === 'back' && section === 'file') { setLeaveFile(current => current + 1); return; }
    // The member's own pages close the sheet back to the team; it slides down before it goes.
    if (name === 'back') { setCloseMember(current => current + 1); return; }
    if (name === 'tab-overview' || name === 'tab-apps') { setSection('overview'); return; }
    if (name === 'tab-chat') { setSection('chat'); return; }
    if (name === 'tab-git') { setSection('git'); return; }
    if (name === 'tab-files') { setSection('files'); return; }
    if (name === 'tab-settings') { setSection('settings'); return; }
    if (name === 'tab-application') { setSection('application'); return; }
    if (name === 'open-workspace' && id) { setOpenAgent(id); setSection('overview'); return; }
    if (name === 'logout') { logout(); return; }
    if (name === 'use-classic') { choose('classic'); return; }
    if (name === 'reload') { refresh(); return; }
    if (name === 'auth-member' && id) { setAuthTab(id); return; }
    if (name === 'forge-tab' && id) { setForgeTab(id); return; }
    if (name === 'save-token') {
      const saved = tokenSave(fields, text);
      if (saved.token && authMember) void source.save(authMember, forgeTab, saved.endpointUrl, saved.token);
      return;
    }
    if (name === 'forget-token') {
      if (authMember) void source.forget(authMember);
      return;
    }
    if (name === 'open-repo' && id) {
      const [moduleId, owner, name] = id.split('/');
      if (moduleId && owner && name) setOpenRepo({ moduleId, owner, name });
      return;
    }
    if (name === 'tasks-back') { setOpenRepo(null); setTasksTab('issues'); return; }
    if (name === 'queue' && openRepo && id && activeServer) {
      void pickIssue(activeServer, null, id, null, fetch, openRepo).then(() => {
        tasks.refresh();
        setRepoNonce(current => current + 1);
      });
      return;
    }
    if (name === 'enable-module') { changeModule(id, true); return; }
    if (name === 'disable-module') { changeModule(id, false); return; }
    if (runTaskAction(name, id, tasks)) return;
    if (name === 'settings-workspace' && id) { setSettingsWorkspace(id); return; }
    if (activeServer && chatWorkspace) {
      if (name === 'enable-autonomy' || name === 'toggle-autonomy') {
        changeChat((async () => {
          if (chatSession?.loop?.active) {
            await putLoopActivation(activeServer, chatWorkspace, false);
            return;
          }
          await putAutonomy(activeServer, true);
          await putLoopActivation(activeServer, chatWorkspace, true);
        })());
        return;
      }
      if (name === 'activate-loop') { changeChat(putLoopActivation(activeServer, chatWorkspace, true)); return; }
      if (name === 'cancel-loop') { changeChat(putLoopActivation(activeServer, chatWorkspace, false)); return; }
      if (name === 'select-agent' && id) { changeChat(bindAgent(activeServer, chatWorkspace, id)); return; }
      if (name === 'enable-agent' && id) { changeChat(enableAgentCapability(activeServer, id)); return; }
      if (name === 'start-bound' && chatSession?.boundAgent) { changeChat(scheduleAgent(activeServer, chatWorkspace, chatSession.boundAgent)); return; }
      if (name === 'member-kind' && id) {
        if (memberLoadedFor !== chatWorkspace) return;
        const drafts = promptSave(fields, systemPrompt, customizationPrompt);
        const change = memberKindChange(id, memberKind, promptOverwrite, drafts.systemPrompt, defaultSystemPrompt);
        if (change === 'none') return;
        if (change === 'confirm') {
          setPendingMemberKind(id);
          setPromptOverwrite(true);
          return;
        }
        const requested = chatWorkspace;
        changeChat(putMember(activeServer, chatWorkspace, id, fetch, {
          customizationPrompt: drafts.customizationPrompt,
        }).then(member => {
          if (member && applyMemberIfCurrent(requested, chatWorkspaceRef.current)) applyMember(member);
        }));
        return;
      }
      if (name === 'save-prompts') {
        if (memberLoadedFor !== chatWorkspace) return;
        const requested = chatWorkspace;
        const drafts = promptSave(fields, systemPrompt, customizationPrompt);
        changeChat(putMember(activeServer, chatWorkspace, memberKind, fetch, {
          systemPrompt: drafts.systemPrompt,
          customizationPrompt: drafts.customizationPrompt,
        }).then(member => {
          if (member && applyMemberIfCurrent(requested, chatWorkspaceRef.current)) applyMember(member);
        }));
        return;
      }
      if (name === 'confirm-overwrite' && pendingMemberKind) {
        if (memberLoadedFor !== chatWorkspace) return;
        const kind = pendingMemberKind;
        const requested = chatWorkspace;
        const drafts = promptSave(fields, systemPrompt, customizationPrompt);
        setPromptOverwrite(false);
        setPendingMemberKind(null);
        changeChat(putMember(activeServer, chatWorkspace, kind, fetch, {
          customizationPrompt: drafts.customizationPrompt,
        }).then(member => {
          if (member && applyMemberIfCurrent(requested, chatWorkspaceRef.current)) applyMember(member);
        }));
        return;
      }
      if (name === 'cancel-overwrite') {
        setPromptOverwrite(false);
        setPendingMemberKind(null);
        return;
      }
      if (name === 'save-agent-settings' && boundAgent) {
        const saved = agentSettingSave(fields, text);
        const values: Record<string, string> = {};
        if (saved.endpoint) values.endpoint = saved.endpoint;
        if (saved.model) values.model = saved.model;
        if (saved.token) values.token = saved.token;
        if (Object.keys(values).length > 0) {
          void updateCapabilitySettings(activeServer, boundAgent, values)
            .catch(() => undefined)
            .finally(() => setModulesNonce(current => current + 1));
        }
        return;
      }
    }
  };

  return (
    <WorkspaceHostProvider host={provide}>
      <View style={{ flex: 1 }}>
        <TeamScene
          scene={team.scene}
          text={team.text}
          items={team.items}
          state={team.state}
          content={agent ? {} : content}
          contentWidth={reading.layout.kind === 'column' ? reading.layout.width : undefined}
          onAction={onAction}
        />
        {agent ? (
          <MemberSheet
            key={agent.workspaceId}
            closeRequest={closeMember}
            onBack={() => onAction('back', null)}
            onClosed={() => { setOpenAgent(null); setSection('overview'); }}>
            <Scene
              scene={model.scene}
              text={model.text}
              items={model.items}
              state={model.state}
              content={content}
              contentWidth={reading.layout.kind === 'column' ? reading.layout.width : undefined}
              onAction={onAction}
            />
          </MemberSheet>
        ) : null}
      </View>
    </WorkspaceHostProvider>
  );
}
