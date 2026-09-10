import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { GitChangesPanel } from '@/features/git/components/GitChangesPanel';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import type { Workspace } from '@/features/workspaces/models/Workspace';
import type { AgentEvent, AgentKind, AgentPermission, AgentSession } from '../models/AgentSession';
import { authenticateAgent, decideAgentPermission, getAgent, scheduleAgent, sendAgentMessage, stopAgent, streamAgentEvents } from '../providers/AgentApiProvider';
import { agentEventRole, agentEventText } from '../providers/AgentEventPresentationProvider';

type AgentTab = 'chat' | 'changes';
type ChatItem = { id: string; role: 'user' | 'agent' | 'thought' | 'tool' | 'system'; text: string };

export function AgentChatScreen({ workspace }: { workspace: Workspace }) {
  const insets = useSafeAreaInsets();
  const { server } = useWorkspaces();
  const [tab, setTab] = useState<AgentTab>('chat');
  const [session, setSession] = useState<AgentSession | null>(null);
  const [items, setItems] = useState<ChatItem[]>([]);
  const [permission, setPermission] = useState<AgentPermission | null>(null);
  const [message, setMessage] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const lastSequence = useRef(0);
  useShellConfig(useMemo(() => ({ title: 'Workspace agent', rightAction: null, sidebarContent: null }), []));

  const applyEvent = useCallback((event: AgentEvent) => {
    lastSequence.current = Math.max(lastSequence.current, event.sequence);
    if (event.type === 'state') { setSession(event.payload as AgentSession); return; }
    if (event.type === 'permission_request') { setPermission(event.payload as AgentPermission); return; }
    if (event.type === 'user_message') { setItems(current => [...current, { id: `${event.sequence}`, role: 'user', text: agentEventText(event.payload) }]); return; }
    if (event.type === 'session_update') {
      const update = (event.payload as { update?: unknown }).update ?? event.payload;
      const role = agentEventRole(update);
      const text = agentEventText(update);
      if (text) setItems(current => appendChunk(current, `${event.sequence}`, role, text));
    }
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

  const choose = async (agent: AgentKind) => {
    if (!server) return; setBusy(true); setError(null);
    try { setSession(await scheduleAgent(server, workspace.id, agent)); }
    catch (cause) { setError(readError(cause)); }
    finally { setBusy(false); }
  };
  const send = async () => {
    const text = message.trim(); if (!server || !text || busy) return;
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
          <View style={styles.heading}><View><Text style={styles.chatTitle}>{workspace.displayName}</Text><Text style={styles.status}>{session?.agent ?? 'No agent'} · {session?.state ?? 'loading'}</Text></View>
            {session?.agent && <Pressable accessibilityRole="button" style={styles.stop} onPress={() => { if (!server) return; void stopAgent(server, workspace.id).then(() => setSession(null)).catch(cause => setError(readError(cause))); }}><Text style={styles.stopText}>Stop</Text></Pressable>}
          </View>
          {!session?.agent && <View style={styles.picker}><Text style={styles.prompt}>Choose an ACP agent</Text>{session?.agents?.map(agent =>
            <Pressable key={agent.agent} disabled={!agent.available || busy} onPress={() => void choose(agent.agent)} style={[styles.agentButton, !agent.available && styles.disabled]}><Text style={styles.agentText}>{agent.displayName}</Text><Text style={styles.availability}>{agent.available ? 'Available' : 'Not installed'}</Text></Pressable>)}</View>}
          {session?.state === 'authentication_required' && <View style={styles.permission}><Text style={styles.permissionTitle}>Sign in to {session.agent}</Text>{session.authMethods?.map(method => <Pressable key={method.id} style={styles.option} onPress={() => server && void authenticateAgent(server, workspace.id, method.id).catch(cause => setError(readError(cause)))}><Text style={styles.optionText}>{method.name}</Text>{method.description && <Text style={styles.availability}>{method.description}</Text>}</Pressable>)}</View>}
          <ScrollView style={styles.messages} contentContainerStyle={styles.messageContent}>{items.map(item => <View key={item.id} style={[styles.bubble, styles[item.role]]}><Text style={styles.role}>{item.role}</Text><Text style={styles.body}>{item.text}</Text></View>)}</ScrollView>
          {permission && <View style={styles.permission}><Text style={styles.permissionTitle}>{permission.request.toolCall?.title ?? 'Agent requests permission'}</Text><View style={styles.options}>{permission.request.options?.map(option => <Pressable key={option.optionId} style={styles.option} onPress={() => void decide(option.optionId)}><Text style={styles.optionText}>{option.name ?? option.kind ?? option.optionId}</Text></Pressable>)}</View></View>}
          {error && <Text style={styles.error}>{error}</Text>}
          {session?.sessionId && <View style={styles.composer}><TextInput accessibilityLabel="Message the agent" multiline value={message} onChangeText={setMessage} placeholder="Ask the agent…" placeholderTextColor="#65736a" style={styles.input}/><Pressable accessibilityRole="button" disabled={busy || !message.trim()} onPress={() => void send()} style={[styles.send, (busy || !message.trim()) && styles.disabled]}>{busy ? <ActivityIndicator color="#000"/> : <Text style={styles.sendText}>Send</Text>}</Pressable></View>}
        </View>}
    </View>
    <View style={[styles.bottomBar, { paddingBottom: insets.bottom + 8 }]}><TabButton label="Chat" active={tab === 'chat'} onPress={() => setTab('chat')} /><TabButton label="Changes" active={tab === 'changes'} onPress={() => setTab('changes')} /></View>
  </View>;
}

function appendChunk(items: ChatItem[], id: string, role: ChatItem['role'], text: string): ChatItem[] {
  const last = items.at(-1); if (last?.role === role && role !== 'tool') return [...items.slice(0, -1), { ...last, text: last.text + text }];
  return [...items, { id, role, text }];
}
function readError(value: unknown) { return value instanceof Error ? value.message : String(value); }
function delay(ms: number) { return new Promise(resolve => setTimeout(resolve, ms)); }
function TabButton({ label, active, onPress }: { label: string; active: boolean; onPress: () => void }) { return <Pressable accessibilityRole="button" accessibilityState={{ selected: active }} onPress={onPress} style={[styles.tabButton, active && styles.tabButtonActive]}><Text style={[styles.tabLabel, active && styles.tabLabelActive]}>{label}</Text></Pressable>; }

const styles = StyleSheet.create({
  screen:{flex:1,backgroundColor:'#000'},content:{flex:1},chat:{flex:1,padding:16,gap:10},changesContent:{padding:20,paddingBottom:32},heading:{flexDirection:'row',justifyContent:'space-between',alignItems:'center'},chatTitle:{color:'#f5fbf7',fontSize:22,fontWeight:'800'},status:{color:'#789085',marginTop:3},stop:{borderWidth:1,borderColor:'#8d3c3c',borderRadius:7,padding:8},stopText:{color:'#e48989'},picker:{gap:8,marginTop:20},prompt:{color:'#aebcb3',fontWeight:'700'},agentButton:{borderWidth:1,borderColor:'#287038',borderRadius:9,padding:14,flexDirection:'row',justifyContent:'space-between'},agentText:{color:'#f5fbf7',fontWeight:'800'},availability:{color:'#2bf27a'},disabled:{opacity:.4},messages:{flex:1},messageContent:{gap:10,paddingVertical:10},bubble:{borderRadius:10,padding:12,maxWidth:'92%'},user:{backgroundColor:'#0f5630',alignSelf:'flex-end'},agent:{backgroundColor:'#101712',alignSelf:'flex-start'},thought:{backgroundColor:'#080d09',borderWidth:1,borderColor:'#20382a'},tool:{backgroundColor:'#12100a',borderWidth:1,borderColor:'#554a22'},system:{backgroundColor:'#111'},role:{color:'#789085',fontSize:10,textTransform:'uppercase',marginBottom:4},body:{color:'#e4eee8',lineHeight:20},permission:{borderWidth:1,borderColor:'#e6a84a',backgroundColor:'#181206',borderRadius:9,padding:12,gap:8},permissionTitle:{color:'#ffd58e',fontWeight:'700'},options:{flexDirection:'row',flexWrap:'wrap',gap:7},option:{borderWidth:1,borderColor:'#e6a84a',borderRadius:6,padding:8},optionText:{color:'#ffd58e'},error:{color:'#e48989'},composer:{flexDirection:'row',alignItems:'flex-end',gap:8},input:{flex:1,minHeight:44,maxHeight:130,borderWidth:1,borderColor:'#287038',borderRadius:9,color:'#f5fbf7',padding:11},send:{height:44,minWidth:64,borderRadius:8,backgroundColor:'#2bf27a',alignItems:'center',justifyContent:'center'},sendText:{color:'#001a09',fontWeight:'800'},bottomBar:{flexDirection:'row',gap:10,paddingHorizontal:14,paddingTop:10,borderTopWidth:1,borderTopColor:'#287038'},tabButton:{flex:1,minHeight:44,alignItems:'center',justifyContent:'center',borderRadius:8,borderWidth:1,borderColor:'#287038'},tabButtonActive:{borderColor:'#2bf27a',backgroundColor:'#08150d'},tabLabel:{color:'#aebcb3',fontWeight:'700'},tabLabelActive:{color:'#2bf27a'}
});
