import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { GitChangesPanel } from '@/features/git/components/GitChangesPanel';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import type { Workspace } from '@/features/workspaces/models/Workspace';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import type { AgentActivityKind, AgentEvent, AgentKind, AgentPermission, AgentSession, SessionContext, TranscriptItem } from '../models/AgentSession';
import { authenticateAgent, cancelAgent, decideAgentPermission, getAgent, scheduleAgent, sendAgentMessage, stopAgent, streamAgentEvents } from '../providers/AgentApiProvider';
import {
  activityHint,
  agentEventText,
  applyPresentedUpdate,
  mergeContext,
  parsePermission,
  permissionOptionLabel,
  permissionOptionTone,
  presentSessionUpdate,
  resolveActivity,
  unwrapSessionUpdate,
  visibleText,
} from '../providers/AgentEventPresentationProvider';

type AgentTab = 'chat' | 'changes';

export function AgentChatScreen({ workspace }: { workspace: Workspace }) {
  const insets = useSafeAreaInsets();
  const { server } = useWorkspaces();
  const [tab, setTab] = useState<AgentTab>('chat');
  const [session, setSession] = useState<AgentSession | null>(null);
  const [items, setItems] = useState<TranscriptItem[]>([]);
  const [context, setContext] = useState<SessionContext>({});
  const [permission, setPermission] = useState<AgentPermission | null>(null);
  const [hint, setHint] = useState<{ kind: AgentActivityKind; toolTitle?: string } | null>(null);
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});
  const [message, setMessage] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const lastSequence = useRef(0);
  useShellConfig(useMemo(() => ({ title: 'Workspace agent', rightAction: null, sidebarContent: null }), []));

  const applyEvent = useCallback((event: AgentEvent) => {
    if (typeof event.sequence === 'number') lastSequence.current = Math.max(lastSequence.current, event.sequence);
    if (event.type === 'state') {
      const next = event.payload as AgentSession;
      setSession(next);
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
    let disposed = false;
    lastSequence.current = 0;
    void getAgent(server, workspace.id).then(value => { if (!disposed && lastSequence.current === 0) setSession(value); }).catch(cause => { if (!disposed) setError(readError(cause)); });
    const controller = new AbortController();
    const connect = async () => {
      while (!controller.signal.aborted) {
        try { await streamAgentEvents(server, workspace.id, lastSequence.current, applyEvent, controller.signal); if (!controller.signal.aborted) await delay(500); }
        catch (cause) { if (!controller.signal.aborted) { setError(readError(cause)); await delay(1500); } }
      }
    };
    void connect();
    return () => { disposed = true; controller.abort(); };
  }, [server, workspace.id, applyEvent]);

  const activity = resolveActivity({ state: session?.state, error: session?.error, hasPermission: Boolean(permission), hint });
  const waiting = busy || session?.state === 'running' || Boolean(permission) || session?.state === 'authentication_required';
  const choose = async (agent: AgentKind) => {
    if (!server) return; setBusy(true); setError(null);
    try { setSession(await scheduleAgent(server, workspace.id, agent)); }
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
    <View style={styles.content}>{tab === 'changes'
      ? <View style={styles.changesPane}><GitChangesPanel workspaceId={workspace.id} /></View>
      : <View style={styles.chat}>
          <View style={styles.heading}>
            <View style={styles.headingCopy}>
              <Text style={styles.chatTitle}>{workspace.displayName}</Text>
              <View style={styles.statusRow}>
                <View style={[styles.dot, activityDot[activity.kind]]} />
                <Text style={styles.status}>{session?.agent ?? 'No agent'} · {activity.label}</Text>
              </View>
            </View>
            <View style={styles.headingActions}>
              {session?.state === 'running' && <Pressable accessibilityRole="button" style={styles.cancel} onPress={() => { if (!server) return; void cancelAgent(server, workspace.id).then(() => { setPermission(null); setHint(null); }).catch(cause => setError(readError(cause))); }}><Text style={styles.cancelText}>Cancel</Text></Pressable>}
              {session?.agent && <Pressable accessibilityRole="button" style={styles.stop} onPress={() => { if (!server) return; void stopAgent(server, workspace.id).then(() => { setSession(null); setItems([]); setContext({}); setPermission(null); setHint(null); }).catch(cause => setError(readError(cause))); }}><Text style={styles.stopText}>Stop</Text></Pressable>}
            </View>
          </View>
          {hasContext(context) && <View style={styles.chips}>
            {context.title ? <Text style={styles.chip}>{context.title}</Text> : null}
            {context.mode ? <Text style={styles.chip}>Mode · {context.mode}</Text> : null}
            {context.usage ? <Text style={styles.chip}>{context.usage}</Text> : null}
            {context.compacting ? <Text style={styles.chip}>Compacting context</Text> : null}
          </View>}
          {!session?.agent && <View style={styles.picker}><Text style={styles.prompt}>Choose an ACP agent</Text>{session?.agents?.map(agent =>
            <Pressable key={agent.agent} disabled={!agent.available || waiting} onPress={() => void choose(agent.agent)} style={[styles.agentButton, !agent.available && styles.disabled]}><Text style={styles.agentText}>{agent.displayName}</Text><Text style={styles.availability}>{agent.available ? 'Available' : 'Not installed'}</Text></Pressable>)}</View>}
          {session?.state === 'authentication_required' && <View style={styles.auth}><Text style={styles.permissionTitle}>Sign in to {session.agent}</Text>{session.authMethods?.map(method => <Pressable key={method.id} style={styles.option} onPress={() => server && void authenticateAgent(server, workspace.id, method.id).catch(cause => setError(readError(cause)))}><Text style={styles.optionText}>{method.name}</Text>{method.description && <Text style={styles.meta}>{method.description}</Text>}</Pressable>)}</View>}
          <ScrollView style={styles.messages} contentContainerStyle={styles.messageContent}>
            {items.map(item => <TranscriptRow key={item.id} item={item} expanded={isExpanded(item, items.at(-1)?.id === item.id && activity.kind === 'thinking', expanded)} onToggle={() => setExpanded(current => ({ ...current, [item.id]: !isExpanded(item, items.at(-1)?.id === item.id && activity.kind === 'thinking', current) }))} />)}
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
    <View style={[styles.bottomBar, { paddingBottom: insets.bottom + 8 }]}><TabButton label="Chat" active={tab === 'chat'} onPress={() => setTab('chat')} /><TabButton label="Changes" active={tab === 'changes'} onPress={() => setTab('changes')} /></View>
  </View>;
}

function TranscriptRow({ item, expanded, onToggle }: { item: TranscriptItem; expanded: boolean; onToggle: () => void }) {
  if (item.role === 'thought') {
    return <Pressable onPress={onToggle} style={styles.thought}>
      <Text style={styles.role}>{expanded ? 'Thinking' : 'Thought'}</Text>
      <Text style={styles.thoughtBody} numberOfLines={expanded ? undefined : 2}>{visibleText(item.text)}</Text>
    </Pressable>;
  }
  if (item.role === 'tool') {
    return <View style={styles.tool}><View style={styles.toolHeader}><Text style={styles.role}>Tool</Text><Text style={styles.toolStatus}>{item.status ?? 'pending'}</Text></View><Text style={styles.body}>{item.title ?? item.text}</Text>{toolDetail(item) ? <Text style={styles.meta}>{toolDetail(item)}</Text> : null}</View>;
  }
  if (item.role === 'plan') {
    return <View style={styles.plan}><Text style={styles.role}>Plan</Text>{item.entries?.map(entry => <Text key={entry.content} style={styles.planEntry}>{statusMark(entry.status)} {entry.content}</Text>) ?? <Text style={styles.body}>{item.text}</Text>}</View>;
  }
  return <View style={[styles.bubble, item.role === 'user' ? styles.user : styles.agent]}><Text style={styles.role}>{item.role === 'user' ? 'You' : 'Agent'}</Text><Text style={styles.body}>{visibleText(item.text)}</Text></View>;
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
  chat: { flex: 1, padding: agentUpTheme.spacing[4], gap: 10 },
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
  agentButton: { ...auBox('card'), flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  agentText: auText('workspaceName'),
  availability: auText('accent'),
  disabled: { opacity: 0.4 },
  messages: { flex: 1 },
  messageContent: { gap: 10, paddingVertical: 10 },
  bubble: { borderRadius: agentUpTheme.radii.md, padding: 12, maxWidth: '92%' },
  user: { backgroundColor: agentUpTheme.colors.surfaceSelectedStrong, alignSelf: 'flex-end' },
  agent: { ...auBox('card'), alignSelf: 'flex-start' },
  thought: { backgroundColor: 'transparent', borderLeftWidth: 2, borderLeftColor: agentUpTheme.colors.borderSubtle, paddingVertical: 6, paddingHorizontal: 10 },
  thoughtBody: { ...auText('muted'), lineHeight: 20, fontStyle: 'italic' },
  tool: { ...auBox('card'), gap: 4 },
  toolHeader: { flexDirection: 'row', justifyContent: 'space-between' },
  toolStatus: { ...auText('fieldLabel'), color: agentUpTheme.colors.textWarning },
  plan: { ...auBox('card'), gap: 4 },
  planEntry: { ...auText('muted'), lineHeight: 20 },
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
});
