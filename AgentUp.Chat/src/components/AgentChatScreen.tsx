import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import * as Clipboard from 'expo-clipboard';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import type { ServerSession } from '@agent-up/server-client';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import type { AgentActivityKind, AgentEvent, AgentKind, AgentPermission, AgentSession, SessionContext, TranscriptItem } from '../models/AgentSession';
import { authenticateAgent, cancelAgent, decideAgentPermission, getAgent, scheduleAgent, sendAgentMessage, stopAgent, streamAgentEvents, submitAgentLoginCallback, submitAgentLoginCode } from '../providers/AgentApiProvider';
import { AgentSignIn } from './AgentSignIn';
import {
  activityHint,
  agentEventText,
  applyPresentedUpdate,
  groupTranscript,
  liveRunId,
  mergeAgentSession,
  mergeContext,
  parsePermission,
  permissionOptionLabel,
  permissionOptionTone,
  presentSessionUpdate,
  resolveActivity,
  runSummary,
  unwrapSessionUpdate,
  visibleText,
} from '../providers/AgentEventPresentationProvider';

type AgentTab = 'chat' | 'changes';

/** All this screen needs of a workspace: which one to talk about, and what to call it. */
export type ChatWorkspace = { id: string; displayName: string };

export type AgentChatScreenProps = {
  workspace: ChatWorkspace;
  /** The Server this workspace lives on, or null while the host is still connecting. */
  server: ServerSession | null;
  /**
   * Rendered behind the Changes tab. A host that has nothing to show there omits it and the tab
   * bar goes with it, which is what lets this screen stand alone in a harness app.
   */
  changesPanel?: ReactNode;
  /** Lets a host title its own chrome while this screen is mounted. */
  onPresent?: (presentation: { title: string }) => void;
};

export function AgentChatScreen({ workspace, server, changesPanel, onPresent }: AgentChatScreenProps) {
  const insets = useSafeAreaInsets();
  const [tab, setTab] = useState<AgentTab>('chat');
  const [session, setSession] = useState<AgentSession | null>(null);
  const [items, setItems] = useState<TranscriptItem[]>([]);
  const [context, setContext] = useState<SessionContext>({});
  const [permission, setPermission] = useState<AgentPermission | null>(null);
  const [hint, setHint] = useState<{ kind: AgentActivityKind; toolTitle?: string } | null>(null);
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});
  const [expandedRuns, setExpandedRuns] = useState<Record<string, boolean>>({});
  const [message, setMessage] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const lastSequence = useRef(0);
  useEffect(() => { onPresent?.({ title: 'Workspace agent' }); }, [onPresent]);

  const applyEvent = useCallback((event: AgentEvent) => {
    if (typeof event.sequence === 'number') lastSequence.current = Math.max(lastSequence.current, event.sequence);
    if (event.type === 'state') {
      const next = event.payload as AgentSession;
      setSession(current => mergeAgentSession(current, next));
      if (next.state !== 'running') setHint(null);
      return;
    }
    if (event.type === 'permission_request') { setPermission(parsePermission(event.payload)); return; }
    if (event.type === 'user_message') {
      setItems(current => [...current, { id: `${event.sequence}`, role: 'user', text: agentEventText(event.payload) }]);
      return;
    }
    if (event.type !== 'session_update') return;
    const presented = presentSessionUpdate(unwrapSessionUpdate(event.payload));
    if (presented.kind === 'context') { setContext(current => mergeContext(current, presented.context)); return; }
    if (presented.kind === 'ignore') return;
    const nextHint = activityHint(presented);
    if (nextHint) setHint(current => nextHint.kind === 'tool' && !nextHint.toolTitle ? { ...nextHint, toolTitle: current?.toolTitle } : nextHint);
    setItems(current => applyPresentedUpdate(current, `${event.sequence}`, presented));
  }, []);

  useEffect(() => {
    if (!server) return;
    lastSequence.current = 0;
    const controller = new AbortController();
    const load = async () => {
      while (!controller.signal.aborted) {
        try {
          const value = await getAgent(server, workspace.id);
          if (!controller.signal.aborted) setSession(current => mergeAgentSession(current, value));
          return;
        }
        catch (cause) {
          if (!controller.signal.aborted) { setError(readError(cause)); await delay(1500); }
        }
      }
    };
    const connect = async () => {
      while (!controller.signal.aborted) {
        try { await streamAgentEvents(server, workspace.id, lastSequence.current, applyEvent, controller.signal); if (!controller.signal.aborted) await delay(500); }
        catch (cause) { if (!controller.signal.aborted) { setError(readError(cause)); await delay(1500); } }
      }
    };
    void load();
    void connect();
    return () => { controller.abort(); };
  }, [server, workspace.id, applyEvent]);

  // Adapts this client's own authenticated transport into the shared module's interface, so the
  // sign-in module never has to know how mobile reaches its Server.
  const loginApi = useMemo(
    () => server
      ? {
          submitCode: async (code: string) => { await submitAgentLoginCode(server, workspace.id, code); },
          submitCallback: async (url: string) => { await submitAgentLoginCallback(server, workspace.id, url); },
        }
      : null,
    [server, workspace.id],
  );
  const copyToClipboard = useCallback((value: string) => Clipboard.setStringAsync(value), []);

  const activity = resolveActivity({ state: session?.state, error: session?.error, hasPermission: Boolean(permission), hint });
  const selectedAgentName = session?.agents.find(agent => agent.agent === session.agent)?.displayName ?? session?.agent ?? 'Agent';
  const waiting = busy || session?.state === 'running' || Boolean(permission) || session?.state === 'authentication_required' || session?.state === 'authenticating';
  const blocks = useMemo(() => groupTranscript(items), [items]);
  const openRunId = liveRunId(blocks);
  const choose = async (agent: AgentKind) => {
    if (!server) return; setBusy(true); setError(null);
    try {
      const next = await scheduleAgent(server, workspace.id, agent);
      setSession(current => mergeAgentSession(current, next));
    }
    catch (cause) { setError(readError(cause)); }
    finally { setBusy(false); }
  };
  const send = async () => {
    const text = message.trim(); if (!server || !text || waiting) return;
    setMessage(''); setBusy(true); setError(null);
    try { await sendAgentMessage(server, workspace.id, text); }
    catch (cause) { setMessage(text); setError(readError(cause)); }
    finally { setBusy(false); }
  };
  const decide = async (optionId: string) => {
    if (!server || !permission) return;
    try { await decideAgentPermission(server, workspace.id, permission.requestId, optionId); setPermission(null); }
    catch (cause) { setError(readError(cause)); }
  };

  return <View style={styles.screen}>
    <View style={styles.content}>{tab === 'changes' && changesPanel
      ? <View style={styles.changesPane}>{changesPanel}</View>
      : <View style={styles.chat}>
          <View style={styles.heading}>
            <View style={styles.headingCopy}>
              <Text style={styles.chatTitle}>{workspace.displayName}</Text>
              <View style={styles.statusRow}>
                <View style={[styles.dot, activityDot[activity.kind]]} />
                <Text style={styles.status}>{session?.agent ? selectedAgentName : 'No agent'} · {activity.label}</Text>
              </View>
            </View>
            <View style={styles.headingActions}>
              {session?.state === 'running' && <Pressable accessibilityRole="button" style={styles.cancel} onPress={() => { if (!server) return; void cancelAgent(server, workspace.id).then(() => { setPermission(null); setHint(null); }).catch(cause => setError(readError(cause))); }}><Text style={styles.cancelText}>Cancel</Text></Pressable>}
              {session?.agent && <Pressable accessibilityRole="button" style={styles.stop} onPress={() => { if (!server) return; void stopAgent(server, workspace.id).then(() => { setSession(current => mergeAgentSession(current, null)); setItems([]); setContext({}); setPermission(null); setHint(null); }).catch(cause => setError(readError(cause))); }}><Text style={styles.stopText}>Stop</Text></Pressable>}
            </View>
          </View>
          {hasContext(context) && <View style={styles.chips}>
            {context.title ? <Text style={styles.chip}>{context.title}</Text> : null}
            {context.mode ? <Text style={styles.chip}>Mode · {context.mode}</Text> : null}
            {context.usage ? <Text style={styles.chip}>{context.usage}</Text> : null}
            {context.compacting ? <Text style={styles.chip}>Compacting context</Text> : null}
          </View>}
          {!session?.agent && <View style={styles.picker}><Text testID="agent-picker-prompt" style={styles.prompt}>Choose an ACP agent</Text>{session?.agents?.map(agent =>
            <Pressable key={agent.agent} testID={`agent-picker-${agent.agent}`} disabled={!agent.available || waiting} onPress={() => void choose(agent.agent)} style={[styles.agentButton, !agent.available && auBox('choiceDisabled')]}><Text style={styles.agentText}>{agent.displayName}</Text><Text style={agent.available ? styles.available : styles.unavailable}>{agent.available ? 'Available' : 'Not installed'}</Text></Pressable>)}</View>}
          {(session?.state === 'authentication_required' || session?.state === 'authenticating') && <View style={styles.auth}>
            <Text style={styles.permissionTitle}>Sign in to {session.agent}</Text>
            {session.state === 'authenticating' && loginApi && <AgentSignIn challenge={session.loginChallenge} api={loginApi} copy={copyToClipboard} onError={setError} />}
            {session.state === 'authentication_required' && session.authMethods?.map(method => <Pressable key={method.id} testID={`agent-auth-method-${method.id}`} style={styles.option} onPress={() => server && void authenticateAgent(server, workspace.id, method.id).catch(cause => setError(readError(cause)))}><Text style={styles.optionText}>{method.name}</Text>{method.description && <Text style={styles.meta}>{method.description}</Text>}</Pressable>)}
          </View>}
          <ScrollView style={styles.messages} contentContainerStyle={styles.messageContent}>
            {blocks.map(block => {
              if (block.type === 'user') {
                return <View key={block.item.id} style={styles.userBubble}><Text style={styles.userText}>{visibleText(block.item.text)}</Text></View>;
              }
              if (block.type === 'reply') {
                return <View key={block.item.id} style={styles.bubble}><Text style={styles.role}>{selectedAgentName}</Text><Text style={styles.body}>{visibleText(block.item.text)}</Text></View>;
              }
              const live = block.id === openRunId;
              const open = live || Boolean(expandedRuns[block.id]);
              return <View key={block.id} style={styles.run}>
                {!live && <Pressable onPress={() => setExpandedRuns(current => ({ ...current, [block.id]: !open }))} style={styles.runHeader}><Text style={styles.runSummary}>{runSummary(block.items)}</Text><Text style={styles.runSummary}>{open ? '▾' : '▸'}</Text></Pressable>}
                {open && block.items.map((item, index) => {
                  const liveThought = live && item.role === 'thought' && index === block.items.length - 1 && activity.kind === 'thinking';
                  const thoughtOpen = isExpanded(item, liveThought, expanded);
                  return <TranscriptRow key={item.id} item={item} expanded={thoughtOpen} live={liveThought} onToggle={() => setExpanded(current => ({ ...current, [item.id]: !isExpanded(item, liveThought, current) }))} />;
                })}
              </View>;
            })}
          </ScrollView>
          {permission && <View style={styles.permission}>
            <Text style={styles.permissionKicker}>Decision needed</Text>
            <Text style={styles.permissionTitle}>{permission.title}</Text>
            {permission.detail ? <Text style={styles.permissionDetail}>{permission.detail}</Text> : null}
            {permission.locations.map(path => <Text key={path} style={styles.location}>{path}</Text>)}
            <View style={styles.options}>{permission.options.map(option => {
              const tone = permissionOptionTone(option.kind);
              return <Pressable key={option.optionId} style={[styles.option, tone === 'allow' && styles.allow, tone === 'reject' && styles.reject]} onPress={() => void decide(option.optionId)}><Text style={[styles.optionText, tone === 'allow' && styles.allowText, tone === 'reject' && styles.rejectText]}>{permissionOptionLabel(option)}</Text></Pressable>;
            })}</View>
          </View>}
          {error && <Text style={styles.error}>{error}</Text>}
          {session?.sessionId && <View style={styles.composer}>
            <TextInput accessibilityLabel="Message the agent" multiline value={message} onChangeText={setMessage} editable={!waiting} placeholder={permission ? 'Choose an option to continue…' : 'Ask the agent…'} placeholderTextColor={agentUpTheme.colors.textFaint} style={styles.input}/>
            <Pressable accessibilityRole="button" disabled={waiting || !message.trim()} onPress={() => void send()} style={[styles.send, (waiting || !message.trim()) && styles.disabled]}>{session?.state === 'running' && !permission ? <ActivityIndicator color={agentUpTheme.colors.onAccent} /> : <Text style={styles.sendText}>Send</Text>}</Pressable>
          </View>}
        </View>}
    </View>
    {changesPanel ? <View style={[styles.bottomBar, { paddingBottom: insets.bottom + 8 }]}><TabButton label="Chat" active={tab === 'chat'} onPress={() => setTab('chat')} /><TabButton label="Changes" active={tab === 'changes'} onPress={() => setTab('changes')} /></View> : null}
  </View>;
}

function TranscriptRow({ item, expanded, live, onToggle }: { item: TranscriptItem; expanded: boolean; live: boolean; onToggle: () => void }) {
  if (item.role === 'thought') {
    return <Pressable onPress={onToggle} style={styles.thought}>
      <Text style={styles.role}>{live ? 'Thinking' : 'Thought'}</Text>
      {expanded ? <Text style={styles.thoughtBody}>{visibleText(item.text)}</Text> : null}
    </Pressable>;
  }
  if (item.role === 'tool') {
    return <View style={styles.work}><View style={styles.toolHeader}><Text style={styles.role}>Tool</Text><Text style={styles.toolStatus}>{item.status ?? 'pending'}</Text></View><Text style={styles.workBody}>{item.title ?? item.text}</Text>{toolDetail(item) ? <Text style={styles.meta}>{toolDetail(item)}</Text> : null}</View>;
  }
  if (item.role === 'plan') {
    return <View style={styles.work}><Text style={styles.role}>Plan</Text>{item.entries?.map(entry => <Text key={entry.content} style={styles.planEntry}>{statusMark(entry.status)} {entry.content}</Text>) ?? <Text style={styles.workBody}>{item.text}</Text>}</View>;
  }
  return <View style={styles.work}><Text style={styles.role}>Agent</Text><Text style={styles.workBody}>{visibleText(item.text)}</Text></View>;
}

function isExpanded(item: TranscriptItem, live: boolean, expanded: Record<string, boolean>) {
  if (item.role !== 'thought') return true;
  return expanded[item.id] ?? live;
}
function hasContext(context: SessionContext) { return Boolean(context.title || context.mode || context.usage || context.compacting); }
function toolDetail(item: TranscriptItem) { const separator = item.text.indexOf('\n'); return separator < 0 ? '' : item.text.slice(separator + 1); }
function statusMark(status: string) { return status === 'completed' ? '✓' : status === 'in_progress' ? '●' : '○'; }
function readError(value: unknown) { return value instanceof Error ? value.message : String(value); }
function delay(ms: number) { return new Promise(resolve => setTimeout(resolve, ms)); }
const activityDot: Record<AgentActivityKind, { backgroundColor: string }> = {
  idle: { backgroundColor: agentUpTheme.colors.textMuted },
  ready: { backgroundColor: agentUpTheme.colors.accentSoft },
  thinking: { backgroundColor: agentUpTheme.colors.textInfo },
  writing: { backgroundColor: agentUpTheme.colors.accentSoft },
  tool: { backgroundColor: agentUpTheme.colors.textWarning },
  plan: { backgroundColor: agentUpTheme.colors.textWarning },
  permission: { backgroundColor: agentUpTheme.colors.textWarning },
  auth: { backgroundColor: agentUpTheme.colors.textWarning },
  running: { backgroundColor: agentUpTheme.colors.accentSoft },
  compacting: { backgroundColor: agentUpTheme.colors.textInfo },
  stopped: { backgroundColor: agentUpTheme.colors.textMuted },
  error: { backgroundColor: agentUpTheme.colors.statusDanger },
};

function TabButton({ label, active, onPress }: { label: string; active: boolean; onPress: () => void }) {
  return <Pressable accessibilityRole="button" accessibilityState={{ selected: active }} onPress={onPress} style={[styles.tabButton, active && styles.tabButtonActive]}><Text style={[styles.tabLabel, active && styles.tabLabelActive]}>{label}</Text></Pressable>;
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: agentUpTheme.colors.canvas },
  content: { flex: 1 },
  chat: { flex: 1, width: '100%', maxWidth: 720, alignSelf: 'center', padding: agentUpTheme.spacing[4], gap: 10 },
  changesPane: { flex: 1, paddingHorizontal: agentUpTheme.spacing[5], paddingTop: agentUpTheme.spacing[4], paddingBottom: 8 },
  heading: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12 },
  headingCopy: { flex: 1, minWidth: 0 },
  headingActions: { flexDirection: 'row', gap: 8 },
  chatTitle: auText('pageTitle'),
  statusRow: { flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: 4 },
  status: auText('muted'),
  dot: { width: 8, height: 8, borderRadius: 4, backgroundColor: agentUpTheme.colors.textMuted },
  cancel: { ...auBox('button', 'buttonSecondary', 'buttonCompact'), paddingHorizontal: 10 },
  cancelText: auText('buttonSecondary', 'buttonCompact'),
  stop: { ...auBox('button', 'buttonDanger', 'buttonCompact'), paddingHorizontal: 10 },
  stopText: auText('button', 'buttonCompact'),
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: 6 },
  chip: { ...auBox('badge'), ...auText('badge'), overflow: 'hidden' },
  picker: { gap: 8, marginTop: 8 },
  prompt: { ...auText('fieldLabel') },
  agentButton: { ...auBox('choice'), flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  agentText: auText('workspaceName'),
  available: auText('accent'),
  unavailable: auText('muted'),
  disabled: { opacity: 0.4 },
  messages: { flex: 1 },
  messageContent: { gap: 10, paddingVertical: 10 },
  userBubble: { ...auBox('chatUser'), alignSelf: 'flex-end' },
  userText: { ...auText('workspaceName'), lineHeight: 20 },
  run: { gap: 8 },
  runHeader: { ...auBox('chatRun'), flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  runSummary: auText('muted'),
  bubble: auBox('card'),
  thought: auBox('chatThought'),
  thoughtBody: auText('chatThoughtBody'),
  work: { ...auBox('chatWork'), gap: 2 },
  toolHeader: { flexDirection: 'row', justifyContent: 'space-between' },
  toolStatus: { ...auText('fieldLabel'), color: agentUpTheme.colors.textWarning },
  planEntry: { ...auText('muted'), lineHeight: 20 },
  workBody: { ...auText('muted'), lineHeight: 20 },
  role: { ...auText('fieldLabel') },
  body: { ...auText('workspaceName'), lineHeight: 20 },
  meta: { ...auText('muted'), marginTop: 4 },
  permission: { ...auBox('card'), borderColor: agentUpTheme.colors.borderDanger, gap: 8 },
  auth: { ...auBox('card'), gap: 8 },
  permissionKicker: { ...auText('fieldLabel'), color: agentUpTheme.colors.textWarning },
  permissionTitle: { ...auText('pageTitle'), fontSize: agentUpTheme.typography.sizeSm },
  permissionDetail: auText('workspaceName'),
  location: { ...auText('muted'), fontSize: agentUpTheme.typography.sizeXs },
  options: { flexDirection: 'row', flexWrap: 'wrap', gap: 7 },
  option: { ...auBox('button', 'buttonSecondary', 'buttonCompact') },
  allow: auBox('button', 'buttonCompact'),
  reject: auBox('button', 'buttonDanger', 'buttonCompact'),
  optionText: auText('buttonSecondary', 'buttonCompact'),
  allowText: auText('button', 'buttonCompact'),
  rejectText: auText('button', 'buttonCompact'),
  error: auText('badgeDanger'),
  composer: { flexDirection: 'row', alignItems: 'flex-end', gap: 8 },
  input: { flex: 1, maxHeight: 130, ...auBox('input'), ...auText('input') },
  send: { ...auBox('button'), minWidth: 64, alignItems: 'center', justifyContent: 'center' },
  sendText: auText('button'),
  bottomBar: { ...auBox('mobileTabBar'), flexDirection: 'row', gap: 10, paddingHorizontal: 14, paddingTop: 10 },
  tabButton: { ...auBox('subtab'), flex: 1, alignItems: 'center', justifyContent: 'center' },
  tabButtonActive: auBox('subtabSelected'),
  tabLabel: auText('subtab'),
  tabLabelActive: auText('subtabSelected'),
  loginUrl: { ...auText('muted'), color: agentUpTheme.colors.textInfo, textDecorationLine: 'underline' },
  loginCode: { ...auText('mono'), color: agentUpTheme.colors.accentSoft, fontSize: agentUpTheme.typography.sizeUiXl, fontWeight: '600', letterSpacing: 1 },
});
