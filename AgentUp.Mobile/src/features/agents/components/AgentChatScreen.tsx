import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { GitChangesPanel } from '@/features/git/components/GitChangesPanel';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import type { Workspace } from '@/features/workspaces/models/Workspace';
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
    void getAgent(server, workspace.id).then(value => { if (!disposed) setSession(value); }).catch(cause => setError(readError(cause)));
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
      ? <ScrollView contentContainerStyle={styles.changesContent}><GitChangesPanel /></ScrollView>
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
              {session?.state === 'running' && <Pressable accessibilityRole="button" style={styles.cancel} onPress={() => { if (!server) return; void cancelAgent(server, workspace.id).catch(cause => setError(readError(cause))); }}><Text style={styles.cancelText}>Cancel</Text></Pressable>}
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
            <TextInput accessibilityLabel="Message the agent" multiline value={message} onChangeText={setMessage} editable={!waiting} placeholder={permission ? 'Choose an option to continue…' : 'Ask the agent…'} placeholderTextColor="#65736a" style={styles.input}/>
            <Pressable accessibilityRole="button" disabled={waiting || !message.trim()} onPress={() => void send()} style={[styles.send, (waiting || !message.trim()) && styles.disabled]}>{session?.state === 'running' && !permission ? <ActivityIndicator color="#000"/> : <Text style={styles.sendText}>Send</Text>}</Pressable>
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
  idle: { backgroundColor: '#789085' },
  ready: { backgroundColor: '#2bf27a' },
  thinking: { backgroundColor: '#8fd4ff' },
  writing: { backgroundColor: '#2bf27a' },
  tool: { backgroundColor: '#e6a84a' },
  plan: { backgroundColor: '#e6a84a' },
  permission: { backgroundColor: '#e6a84a' },
  auth: { backgroundColor: '#e6a84a' },
  running: { backgroundColor: '#2bf27a' },
  compacting: { backgroundColor: '#8fd4ff' },
  stopped: { backgroundColor: '#789085' },
  error: { backgroundColor: '#e48989' },
};

function TabButton({ label, active, onPress }: { label: string; active: boolean; onPress: () => void }) {
  return <Pressable accessibilityRole="button" accessibilityState={{ selected: active }} onPress={onPress} style={[styles.tabButton, active && styles.tabButtonActive]}><Text style={[styles.tabLabel, active && styles.tabLabelActive]}>{label}</Text></Pressable>;
}

const styles = StyleSheet.create({
  screen:{flex:1,backgroundColor:'#000'},content:{flex:1},chat:{flex:1,padding:16,gap:10},changesContent:{padding:20,paddingBottom:32},
  heading:{flexDirection:'row',justifyContent:'space-between',alignItems:'flex-start',gap:12},headingCopy:{flex:1,minWidth:0},headingActions:{flexDirection:'row',gap:8},
  chatTitle:{color:'#f5fbf7',fontSize:22,fontWeight:'800'},statusRow:{flexDirection:'row',alignItems:'center',gap:8,marginTop:4},
  status:{color:'#aebcb3'},dot:{width:8,height:8,borderRadius:4,backgroundColor:'#789085'},
  cancel:{borderWidth:1,borderColor:'#287038',borderRadius:8,paddingHorizontal:10,paddingVertical:8},cancelText:{color:'#aebcb3'},
  stop:{borderWidth:1,borderColor:'#8d3c3c',borderRadius:8,paddingHorizontal:10,paddingVertical:8},stopText:{color:'#e48989'},
  chips:{flexDirection:'row',flexWrap:'wrap',gap:6},chip:{color:'#aebcb3',borderWidth:1,borderColor:'#20382a',borderRadius:8,paddingHorizontal:8,paddingVertical:4,overflow:'hidden',fontSize:12},
  picker:{gap:8,marginTop:8},prompt:{color:'#aebcb3',fontWeight:'700'},agentButton:{borderWidth:1,borderColor:'#287038',borderRadius:8,padding:14,flexDirection:'row',justifyContent:'space-between'},agentText:{color:'#f5fbf7',fontWeight:'800'},availability:{color:'#2bf27a'},disabled:{opacity:.4},
  messages:{flex:1},messageContent:{gap:10,paddingVertical:10},
  bubble:{borderRadius:8,padding:12,maxWidth:'92%'},user:{backgroundColor:'#0f5630',alignSelf:'flex-end'},agent:{backgroundColor:'#101712',alignSelf:'flex-start'},
  thought:{backgroundColor:'transparent',borderLeftWidth:2,borderLeftColor:'#20382a',paddingVertical:6,paddingHorizontal:10},
  thoughtBody:{color:'#789085',lineHeight:20,fontStyle:'italic'},
  tool:{backgroundColor:'#12100a',borderWidth:1,borderColor:'#554a22',borderRadius:8,padding:12,gap:4},toolHeader:{flexDirection:'row',justifyContent:'space-between'},toolStatus:{color:'#e6a84a',fontSize:10,textTransform:'uppercase'},
  plan:{borderWidth:1,borderColor:'#20382a',borderRadius:8,padding:12,gap:4},planEntry:{color:'#aebcb3',lineHeight:20},
  role:{color:'#789085',fontSize:10,textTransform:'uppercase',marginBottom:4},body:{color:'#e4eee8',lineHeight:20},meta:{color:'#789085',marginTop:4},
  permission:{borderWidth:1,borderColor:'#e6a84a',backgroundColor:'#181206',borderRadius:8,padding:12,gap:8},auth:{borderWidth:1,borderColor:'#e6a84a',backgroundColor:'#181206',borderRadius:8,padding:12,gap:8},
  permissionKicker:{color:'#e6a84a',fontSize:10,textTransform:'uppercase',fontWeight:'700'},permissionTitle:{color:'#ffd58e',fontWeight:'700'},permissionDetail:{color:'#e4eee8'},location:{color:'#aebcb3',fontSize:12},
  options:{flexDirection:'row',flexWrap:'wrap',gap:7},option:{borderWidth:1,borderColor:'#e6a84a',borderRadius:8,padding:8},
  allow:{borderColor:'#2bf27a',backgroundColor:'#08150d'},reject:{borderColor:'#8d3c3c',backgroundColor:'#160808'},
  optionText:{color:'#ffd58e'},allowText:{color:'#2bf27a'},rejectText:{color:'#e48989'},
  error:{color:'#e48989'},composer:{flexDirection:'row',alignItems:'flex-end',gap:8},input:{flex:1,minHeight:44,maxHeight:130,borderWidth:1,borderColor:'#287038',borderRadius:8,color:'#f5fbf7',padding:11},
  send:{height:44,minWidth:64,borderRadius:8,backgroundColor:'#2bf27a',alignItems:'center',justifyContent:'center'},sendText:{color:'#001a09',fontWeight:'800'},
  bottomBar:{flexDirection:'row',gap:10,paddingHorizontal:14,paddingTop:10,borderTopWidth:1,borderTopColor:'#287038'},
  tabButton:{flex:1,minHeight:44,alignItems:'center',justifyContent:'center',borderRadius:8,borderWidth:1,borderColor:'#287038'},tabButtonActive:{borderColor:'#2bf27a',backgroundColor:'#08150d'},
  tabLabel:{color:'#aebcb3',fontWeight:'700'},tabLabelActive:{color:'#2bf27a'},
});
