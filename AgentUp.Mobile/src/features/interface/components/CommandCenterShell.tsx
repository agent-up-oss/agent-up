import { useRef, useState } from 'react';
import { Animated, PanResponder, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { agentUpTheme, auBox } from '@agent-up/design-system/native';
import { useTaskQueue, type TaskQueueState } from '../controllers/TaskQueueContext';
import { useTeamStanding } from '../controllers/TeamStandingContext';
import { issueLabel, repositoryLabel, reorderTarget, taskLabel, type QueuedTask } from '../models/TaskQueue';
import type { TeamStanding } from '../models/TeamStanding';
import {
  checksCount,
  hasStatusRow,
  headlineFor,
  roleLabel,
  showProposalBesideTask,
  slotGlyph,
  stateLabel,
  threadSummary,
  whoIs,
  type TeamTriage,
} from '../services/TeamStandingService';
import { InterfaceToggleRow } from './InterfaceToggleRow';

type Surface = 'bridge' | 'tasks' | 'team' | 'settings';

/**
 * The command center, on the phone.
 *
 * An agent and its workspace are the same thing here, so the team is the workspace list and a
 * workspace's own page is that agent's page. Everything drawn comes from two reads the Server
 * answers per workspace - the agent session and the forge - so the same screens fill against the
 * Demo and against a real Server. Where a read said nothing, the screen says so.
 */
export function CommandCenterShell() {
  const standing = useTeamStanding();
  const tasks = useTaskQueue();
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
          <Text style={styles.subtitle} numberOfLines={1}>
            {standing.read ? standing.headline : serverLabel}
          </Text>
        </View>
      </View>

      <ScrollView contentContainerStyle={styles.body}>
        {surface === 'bridge' ? <Bridge standing={standing} /> : null}
        {surface === 'tasks' ? <Tasks standing={standing} tasks={tasks} /> : null}
        {surface === 'team' ? <Team standing={standing} /> : null}
        {surface === 'settings' ? <Settings onSignOut={logout} serverLabel={serverLabel} /> : null}
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

type Standing = TeamTriage & { loading: boolean; read: boolean; error: string | null; refresh: () => void };

function titleFor(surface: Surface): string {
  if (surface === 'bridge') return 'Bridge';
  if (surface === 'tasks') return 'Tasks';
  if (surface === 'team') return 'Team';
  return 'Settings';
}

function Bridge({ standing }: { standing: Standing }) {
  if (standing.error) return <Empty title="Could not reach the Server" note={standing.error} action="Retry" onPress={standing.refresh} />;
  if (!standing.read && standing.loading) return <Empty title="Loading" note="Reading the team from the Server." />;
  if (!standing.team.length) return <Empty title="No agents yet" note="Register a workspace and it becomes an agent's own space." />;

  return (
    <>
      <Section title={`WAITING ON YOU · ${standing.waiting.length}`} tone={standing.waiting.length ? 'warning' : 'muted'} />
      {standing.waiting.length
        ? standing.waiting.map(item => <StandingCard key={item.workspaceId} standing={item} />)
        : <Text style={styles.note}>Nothing is waiting on you.</Text>}

      <Section title={`HALTED · ${standing.halted.length}`} tone={standing.halted.length ? 'danger' : 'muted'} />
      {standing.halted.length
        ? standing.halted.map(item => <StandingCard key={item.workspaceId} standing={item} />)
        : <Text style={styles.note}>Nothing halted.</Text>}

      <Section title={`NOTHING NEEDED · ${standing.settled.length}`} tone="muted" />
      {standing.settled.length
        ? standing.settled.map(item => <StandingCard key={item.workspaceId} standing={item} />)
        : <Text style={styles.note}>Every agent needs something from you.</Text>}
    </>
  );
}

function Tasks({ standing, tasks }: { standing: Standing; tasks: TaskQueueState }) {
  if (standing.error) return <Empty title="Could not reach the Server" note={standing.error} action="Retry" onPress={standing.refresh} />;
  if (!standing.read && standing.loading) return <Empty title="Loading" note="Reading the queue from the Server." />;

  return (
    <>
      <Section title={`QUEUE · ${tasks.queue.length}`} tone="muted" />
      <Text style={styles.note}>The agents work these, in this order. Nothing else on the host is touched.</Text>
      {tasks.error ? <Text style={[styles.note, styles.danger]}>{tasks.error}</Text> : null}
      {tasks.queue.length
        ? tasks.queue.map((task, index) => (
          <QueueRow
            key={`${task.owner}/${task.repository}/${task.id}`}
            task={task}
            index={index}
            count={tasks.queue.length}
            onDrop={tasks.moveTo}
            onRemove={tasks.remove}
            onRelease={tasks.release}
          />
        ))
        : <Text style={styles.note}>Nothing is queued, so the agents have nothing to take. Pick an issue below.</Text>}

      <Section title={`OPEN ISSUES · ${tasks.offered.length}`} tone="muted" />
      {tasks.offered.length
        ? tasks.offered.map(issue => (
          <View key={issue.id} style={styles.queueRow}>
            <View style={styles.queueRowBody}>
              <Text style={styles.cardTitle} numberOfLines={1} ellipsizeMode="tail">{issueLabel(issue)}</Text>
            </View>
            <QueueAction label="Queue" onPress={() => tasks.pick(issue)} />
          </View>
        ))
        : <Text style={styles.note}>Every open issue on this repository is already queued.</Text>}

      <Section title={`REPORTED · ${standing.tasks.length}`} tone="muted" />
      <Text style={styles.note}>What each agent says it is holding right now.</Text>
      {standing.tasks.length
        ? standing.tasks.map(task => (
          <View key={task.number} style={styles.card}>
            <Text style={styles.cardTitle}>{task.label}</Text>
            <Text style={[styles.note, kindTone(task.kind)]}>{stateLabel(task.kind)}</Text>
            {task.agents.map(agent => (
              <Text key={agent.workspaceId} style={styles.note}>
                {agent.displayName}{roleLabel(agent) ? ` · ${roleLabel(agent)}` : ''}
              </Text>
            ))}
          </View>
        ))
        : <Text style={styles.note}>No agent reports a task in hand.</Text>}
    </>
  );
}

/** The height one queue row occupies, which is what turns a drag distance into whole positions. */
const queueRowHeight = 64;

/**
 * One queued task: its number and title on one line, the repository under it, and the grip that
 * reorders it.
 *
 * The grip is dragged rather than tapped because the order is the point of this list and stepping
 * a task through it one press at a time says nothing about where it will land. Dragging shows the
 * row moving; the queue is only told once, on release, so a drag costs one write rather than one
 * per position crossed.
 */
function QueueRow({
  task,
  index,
  count,
  onDrop,
  onRemove,
  onRelease,
}: {
  task: QueuedTask;
  index: number;
  count: number;
  onDrop: (task: QueuedTask, index: number) => void;
  onRemove: (task: QueuedTask) => void;
  onRelease: (task: QueuedTask) => void;
}) {
  const offset = useRef(new Animated.Value(0)).current;
  const [dragging, setDragging] = useState(false);
  const responder = useRef(
    PanResponder.create({
      onStartShouldSetPanResponder: () => true,
      onPanResponderGrant: () => setDragging(true),
      onPanResponderMove: (_, gesture) => offset.setValue(gesture.dy),
      onPanResponderRelease: (_, gesture) => {
        setDragging(false);
        offset.setValue(0);
        const target = reorderTarget(index, gesture.dy, queueRowHeight, count);
        if (target !== index) onDrop(task, target);
      },
      onPanResponderTerminate: () => {
        setDragging(false);
        offset.setValue(0);
      },
    }),
  ).current;

  return (
    <Animated.View
      style={[styles.queueRow, dragging && styles.queueRowDragging, { transform: [{ translateY: offset }] }]}
    >
      <View
        accessibilityRole="adjustable"
        accessibilityLabel={`Reorder ${taskLabel(task)}`}
        style={styles.grip}
        {...responder.panHandlers}
      >
        <View style={styles.gripBar} />
        <View style={styles.gripBar} />
        <View style={styles.gripBar} />
      </View>
      <View style={styles.queueRowBody}>
        <Text style={styles.cardTitle} numberOfLines={1} ellipsizeMode="tail">{taskLabel(task)}</Text>
        <Text style={styles.note} numberOfLines={1} ellipsizeMode="tail">{repositoryLabel(task)}</Text>
      </View>
      {task.heldByWorkspaceId === null
        ? <QueueAction label="Dequeue" tone="danger" onPress={() => onRemove(task)} />
        : <QueueAction label="Release" onPress={() => onRelease(task)} />}
    </Animated.View>
  );
}

function QueueAction({ label, tone, onPress }: { label: string; tone?: 'danger'; onPress: () => void }) {
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={label} style={styles.rowAction} onPress={onPress}>
      <Text style={tone === 'danger' ? styles.dangerText : styles.buttonText}>{label}</Text>
    </Pressable>
  );
}

function Team({ standing }: { standing: Standing }) {
  if (standing.error) return <Empty title="Could not reach the Server" note={standing.error} action="Retry" onPress={standing.refresh} />;
  if (!standing.read && standing.loading) return <Empty title="Loading" note="Reading the team from the Server." />;
  if (!standing.team.length) return <Empty title="No agents yet" note="Each workspace on this Server is one agent's own space." />;
  return <>{standing.team.map(item => <StandingCard key={item.workspaceId} standing={item} />)}</>;
}

/** One agent as a card: who, what they are on, and what the host reports about it. */
function StandingCard({ standing }: { standing: TeamStanding }) {
  const tone = standing.kind === 'halted' ? styles.cardDanger : standing.kind === 'waiting' ? styles.cardWarning : null;
  return (
    <View style={[styles.card, tone]}>
      <Text style={styles.cardTitle}>{headlineFor(standing)}</Text>
      <Text style={styles.note}>{whoIs(standing)}</Text>
      <Text style={[styles.note, toneStyle(standing)]}>{stateLabel(standing.kind)} · {standing.branch}</Text>
      {standing.reason ? <Text style={styles.note}>{standing.reason}</Text> : null}
      {standing.kind === 'halted' && standing.diagnosticCode ? (
        <Text style={styles.code}>{standing.diagnosticCode}</Text>
      ) : null}
      {showProposalBesideTask(standing) ? (
        <Text style={styles.note}>#{standing.forge.proposalNumber} {standing.forge.proposalTitle}</Text>
      ) : null}
      {hasStatusRow(standing) ? <StatusRow standing={standing} /> : null}
      {standing.forge.unresolvedThreads > 0 ? <Text style={styles.note}>{threadSummary(standing)}</Text> : null}
    </View>
  );
}

/** Where the proposal stands, in one row: the checks as a dot and a count, then the two slots. */
function StatusRow({ standing }: { standing: TeamStanding }) {
  return (
    <View style={styles.statusRow}>
      <Text style={[styles.statusDot, checksTone(standing)]}>●</Text>
      <Text style={styles.statusCount}>{checksCount(standing)}</Text>
      <Text style={styles.statusDash}>—</Text>
      {standing.slots.map(slot => (
        <View key={slot.kind} style={styles.slot}>
          <Text style={styles.slotLabel}>{slot.kind}</Text>
          <Text style={[styles.slotGlyph, slotTone(slot.mark)]}>{slotGlyph(slot)}</Text>
        </View>
      ))}
    </View>
  );
}

function checksTone(standing: TeamStanding) {
  if (standing.forge.checks === 'failed') return styles.toneDanger;
  if (standing.forge.checks === 'running') return styles.toneWarning;
  if (standing.forge.checks === 'passed') return styles.toneLive;
  return styles.toneFaint;
}

function slotTone(mark: TeamStanding['slots'][number]['mark']) {
  if (mark === 'approved') return styles.toneLive;
  if (mark === 'warning') return styles.toneWarning;
  if (mark === 'blocked') return styles.toneDanger;
  return styles.toneFaint;
}

function toneStyle(standing: TeamStanding) {
  return kindTone(standing.kind);
}

function kindTone(kind: TeamStanding['kind']) {
  if (kind === 'halted') return styles.toneDanger;
  if (kind === 'waiting') return styles.toneWarning;
  if (kind === 'working') return styles.toneLive;
  return undefined;
}

function Settings({ onSignOut, serverLabel }: { onSignOut: () => void; serverLabel: string }) {
  return (
    <>
      <Section title="INTERFACE" tone="muted" />
      <InterfaceToggleRow />
      <Section title="SERVER" tone="muted" />
      <View style={styles.card}>
        <Text style={styles.cardTitle}>Connected to</Text>
        <Text style={styles.note}>{serverLabel}</Text>
      </View>
      <Pressable accessibilityRole="button" style={[styles.card, styles.cardAction]} onPress={onSignOut}>
        <Text style={styles.cardTitleDanger}>Sign out of this server</Text>
        <Text style={styles.note}>Disconnects this client. Agents keep running on the Server.</Text>
      </Pressable>
    </>
  );
}

function Section({ title, tone }: { title: string; tone: 'muted' | 'warning' | 'danger' }) {
  return (
    <Text style={[
      styles.section,
      tone === 'danger' && styles.sectionDanger,
      tone === 'warning' && styles.sectionWarning,
    ]}>
      {title}
    </Text>
  );
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
  sectionWarning: { color: agentUpTheme.colors.textWarning },
  card: { ...auBox('card'), gap: 4 },
  rowAction: { ...auBox('button', 'buttonSecondary'), paddingHorizontal: 12, paddingVertical: 6 },
  danger: { color: agentUpTheme.colors.statusDanger },
  dangerText: { color: agentUpTheme.colors.statusDanger, fontSize: 13, fontWeight: '600' },
  queueRow: {
    ...auBox('card'),
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    minHeight: 64,
  },
  queueRowDragging: { borderColor: agentUpTheme.colors.accentSoft },
  queueRowBody: { flexGrow: 1, flexShrink: 1, minWidth: 0, gap: 2 },
  grip: { paddingVertical: 10, paddingRight: 4, gap: 3, justifyContent: 'center' },
  gripBar: { width: 18, height: 2, borderRadius: 1, backgroundColor: agentUpTheme.colors.textMuted },
  cardDanger: auBox('cardDanger'),
  cardWarning: { borderColor: agentUpTheme.colors.borderStrong },
  code: { color: agentUpTheme.colors.textMuted, fontSize: 12, fontFamily: agentUpTheme.fonts.mono },
  toneDanger: { color: agentUpTheme.colors.textDanger },
  toneWarning: { color: agentUpTheme.colors.textWarning },
  toneLive: { color: agentUpTheme.colors.accentBright },
  toneFaint: { color: agentUpTheme.colors.textFaint },
  statusRow: { flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: 4, flexWrap: 'wrap' },
  statusDot: { fontSize: 12 },
  statusCount: { color: agentUpTheme.colors.textSecondary, fontSize: 13 },
  statusDash: { color: agentUpTheme.colors.textFaint, fontSize: 13 },
  slot: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    paddingHorizontal: 7,
    paddingVertical: 2,
    borderRadius: 4,
    borderWidth: 1,
    borderColor: agentUpTheme.colors.borderSubtle,
  },
  slotLabel: { color: agentUpTheme.colors.textMuted, fontSize: 11 },
  slotGlyph: { fontSize: 11, fontWeight: '700' },
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
    ...auBox('button', 'buttonSecondary'),
    marginTop: 10,
    alignItems: 'center',
    justifyContent: 'center',
  },
  buttonText: { color: agentUpTheme.colors.accentSoft, fontSize: 13, fontWeight: '600' },
  tabBar: {
    ...auBox('panel'),
    flexDirection: 'row',
    borderRadius: 0,
    borderWidth: 0,
    borderTopWidth: 1,
    borderTopColor: agentUpTheme.colors.borderSubtle,
  },
  tabItem: { flex: 1, minHeight: 52, alignItems: 'center', justifyContent: 'center' },
  tabText: { color: agentUpTheme.colors.textFaint, fontSize: 12 },
  tabTextOn: { color: agentUpTheme.colors.textPrimary, fontSize: 12, fontWeight: '600' },
  drawer: { position: 'absolute', top: 0, right: 0, bottom: 0, left: 0, flexDirection: 'row' },
  scrim: { position: 'absolute', top: 0, right: 0, bottom: 0, left: 0, backgroundColor: agentUpTheme.colors.scrim },
  drawerPanel: { ...auBox('drawer'), paddingVertical: agentUpTheme.spacing[3], gap: 2 },
  drawerServer: {
    ...auBox('card'),
    paddingHorizontal: agentUpTheme.spacing[3],
    paddingVertical: agentUpTheme.spacing[3],
    marginBottom: agentUpTheme.spacing[3],
    gap: 2,
  },
  drawerServerName: { color: agentUpTheme.colors.textPrimary, fontSize: 13, fontWeight: '700' },
  drawerLink: { minHeight: 44, justifyContent: 'center', paddingHorizontal: agentUpTheme.spacing[3], borderRadius: 8 },
  drawerLinkOn: auBox('cardSelected'),
  drawerLinkText: { color: agentUpTheme.colors.textMuted, fontSize: 15 },
  drawerLinkTextOn: { color: agentUpTheme.colors.textPrimary, fontSize: 15, fontWeight: '600' },
});
