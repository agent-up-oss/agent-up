import { useCallback, useEffect, useRef, useState } from 'react';
import {
  Animated,
  PanResponder,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
  type ViewStyle,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useServers } from '@/features/servers/controllers/ServersContext';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import { agentUpTheme, auBox } from '@agent-up/design-system/native';
import { useTaskQueue, type TaskQueueState } from '../controllers/TaskQueueContext';
import { useTraining } from '../controllers/TrainingContext';
import { useTeamStanding } from '../controllers/TeamStandingContext';
import {
  dragOffset,
  issueLabel,
  reorderTarget,
  repositoryLabel,
  restingOffset,
  rowShift,
  taskLabel,
  type QueuedTask,
} from '../models/TaskQueue';
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
import {
  decisionExportFileName,
  exportedCount,
  saveDecisionExport,
} from '../providers/DecisionExportProvider';
import { readExport } from '../providers/TrainingProvider';
import { AgentPage } from './AgentPage';
import { TrainScreen } from './TrainScreen';
import { InterfaceToggleRow } from './InterfaceToggleRow';

type Surface = 'bridge' | 'train' | 'tasks' | 'team' | 'settings';

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
  const training = useTraining();
  const { activeServer, logout } = useServers();
  const { workspaces } = useWorkspaces();
  // The phone's own chrome sits over the top and bottom of the screen: the status bar and the
  // island above, the home indicator below. The shell keeps clear of both rather than drawing
  // under them, which is what put the title behind the clock.
  const insets = useSafeAreaInsets();
  const [surface, setSurface] = useState<Surface>('bridge');
  // Which agent's own page is open, if any. The team says what each one is doing; opening one
  // says what they are doing it in.
  const [openAgent, setOpenAgent] = useState<string | null>(null);
  const [menuOpen, setMenuOpen] = useState(false);
  // A drag and a scroll are the same downward swipe, so the list cannot scroll while a row is
  // being carried: the gesture has to mean one thing at a time.
  const [dragging, setDragging] = useState(false);

  const serverLabel = activeServer?.url ?? 'No server';

  // The whole backlog as training material. Judged decisions only: an export is what a training
  // run reads, and a decision nobody has judged carries no lesson.
  const exportDecisions = useCallback(() => {
    if (!activeServer) return;
    void readExport(activeServer, false)
      .then(jsonl => exportedCount(jsonl) === 0
        ? undefined
        : saveDecisionExport(jsonl, decisionExportFileName()))
      .catch(() => undefined);
  }, [activeServer]);

  // An agent's page takes the whole shell: it carries its own head and its own sections, and the
  // tab bar below still moves between surfaces, so leaving it is one press either way.
  const agent = openAgent === null ? null : standing.team.find(item => item.workspaceId === openAgent) ?? null;
  if (agent) {
    return (
      <View style={styles.shell}>
        <AgentPage
          topInset={insets.top}
          standing={agent}
          workspace={workspaces.find(item => item.id === agent.workspaceId) ?? null}
          server={activeServer}
          onBack={() => setOpenAgent(null)}
        />
        <View style={[styles.tabBar, insets.bottom > 0 && { paddingBottom: insets.bottom }]}>
          <TabItem label="Bridge" active={false} onPress={() => { setOpenAgent(null); setSurface('bridge'); }} />
          <TabItem label="Train" active={false} onPress={() => { setOpenAgent(null); setSurface('train'); }} />
          <TabItem label="Tasks" active={false} onPress={() => { setOpenAgent(null); setSurface('tasks'); }} />
          <TabItem label="Team" active onPress={() => setOpenAgent(null)} />
        </View>
      </View>
    );
  }

  return (
    <View style={styles.shell}>
      <View style={[styles.head, { paddingTop: insets.top + agentUpTheme.spacing[3] }]}>
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

      <ScrollView contentContainerStyle={styles.body} scrollEnabled={!dragging}>
        {surface === 'bridge' ? <Bridge standing={standing} /> : null}
        {surface === 'train' ? <TrainScreen training={training} server={activeServer} onExport={exportDecisions} /> : null}
        {surface === 'tasks' ? <Tasks standing={standing} tasks={tasks} onDragging={setDragging} /> : null}
        {surface === 'team' ? <Team standing={standing} onOpen={setOpenAgent} /> : null}
        {surface === 'settings' ? <Settings onSignOut={logout} serverLabel={serverLabel} /> : null}
      </ScrollView>

      <View style={[styles.tabBar, insets.bottom > 0 && { paddingBottom: insets.bottom }]}>
        <TabItem label="Bridge" active={surface === 'bridge'} onPress={() => setSurface('bridge')} />
        <TabItem label="Train" active={surface === 'train'} onPress={() => setSurface('train')} />
        <TabItem label="Tasks" active={surface === 'tasks'} onPress={() => setSurface('tasks')} />
        <TabItem label="Team" active={surface === 'team'} onPress={() => setSurface('team')} />
      </View>

      {menuOpen ? (
        <View style={styles.drawer}>
          <Pressable accessibilityLabel="Close menu" style={styles.scrim} onPress={() => setMenuOpen(false)} />
          <View style={[styles.drawerPanel, { paddingTop: insets.top + agentUpTheme.spacing[3] }]}>
            <View style={styles.drawerServer}>
              <Text style={styles.drawerServerName}>Server</Text>
              <Text style={styles.subtitle} numberOfLines={2}>{serverLabel}</Text>
            </View>
            {(['bridge', 'train', 'tasks', 'team', 'settings'] as Surface[]).map(item => (
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
  if (surface === 'train') return 'Train';
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

function Tasks({
  standing,
  tasks,
  onDragging,
}: {
  standing: Standing;
  tasks: TaskQueueState;
  onDragging: (dragging: boolean) => void;
}) {
  if (standing.error) return <Empty title="Could not reach the Server" note={standing.error} action="Retry" onPress={standing.refresh} />;
  if (!standing.read && standing.loading) return <Empty title="Loading" note="Reading the queue from the Server." />;

  return (
    <>
      {tasks.error ? <Text style={[styles.note, styles.danger]}>{tasks.error}</Text> : null}

      {tasks.working.length ? (
        <>
          <Section title={`IN PROGRESS · ${tasks.working.length}`} tone="muted" />
          <Text style={styles.note}>Taken off the queue and being worked. Stopping one puts it back.</Text>
          {tasks.working.map(task => (
            <View key={`${task.owner}/${task.repository}/${task.id}`} style={styles.queueRow}>
              <View style={styles.queueRowBody}>
                <Text style={styles.cardTitle} numberOfLines={1} ellipsizeMode="tail">{taskLabel(task)}</Text>
                <Text style={styles.note} numberOfLines={1} ellipsizeMode="tail">{repositoryLabel(task)}</Text>
              </View>
              <QueueAction label="Stop" tone="danger" onPress={() => tasks.stop(task)} />
            </View>
          ))}
        </>
      ) : null}

      <Section title={`QUEUE · ${tasks.queue.length}`} tone="muted" />
      <Text style={styles.note}>The agents work these, in this order. Nothing else on the host is touched.</Text>
      {tasks.queue.length
        ? (
          <QueueList
            queue={tasks.queue}
            onMove={tasks.moveTo}
            onRemove={tasks.remove}
            onDragging={onDragging}
          />
        )
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

/**
 * The gap the body puts under each row, which the pitch between two rows includes. A row's own
 * height is measured rather than assumed; only the gap is a constant, because it is one.
 */
const queueRowGap = 10;

/** What a row falls back to before it has been measured, matching the row's own minimum height. */
const queueRowMinHeight = 64;

/**
 * How far a finger travels before the gesture counts as a drag rather than a touch. Below this a
 * press is still a press, so resting a thumb on the grip does not pick the row up.
 */
const dragThreshold = 4;

type Drag = {
  id: string;
  from: number;
  to: number;
  /** Set once the finger has lifted: the row rests in its chosen slot until the new order lands. */
  settled: boolean;
};

/**
 * The queue, reorderable by dragging a row's grip.
 *
 * The drag lives here rather than in the row because reordering is a fact about the list: the row
 * under the finger follows it, and the rows it passes step aside to open the slot it would land
 * in. A row that owned its own drag could only move itself, which is what made the order impossible
 * to predict before letting go.
 *
 * The queue is told once, on release, so a drag costs one write rather than one per position
 * crossed, and the Server stays the one that decides the order.
 */
function QueueList({
  queue,
  onMove,
  onRemove,
  onDragging,
}: {
  queue: QueuedTask[];
  onMove: (task: QueuedTask, index: number) => void;
  onRemove: (task: QueuedTask) => void;
  onDragging: (dragging: boolean) => void;
}) {
  const [rowHeight, setRowHeight] = useState(queueRowMinHeight);
  const [drag, setDrag] = useState<Drag | null>(null);
  const pitch = rowHeight + queueRowGap;

  // The gesture ends outside React's own flow, so the drag is mirrored here and read back on
  // release. Deciding the move inside a state updater instead would put the write to the Server in
  // a function React is free to call more than once, or not when the finger actually lifted.
  const live = useRef<Drag | null>(null);

  // The queue this row was dropped against. Holding it lets the gap stay open until the Server
  // answers: clearing the drag on release instead would snap the row back to where it started and
  // move it a second time when the re-read lands.
  const settledAgainst = useRef<QueuedTask[] | null>(null);

  useEffect(() => {
    if (settledAgainst.current === null || settledAgainst.current === queue) return;
    settledAgainst.current = null;
    live.current = null;
    setDrag(null);
  }, [queue]);

  // The shell stops scrolling while a row is carried. Reporting it in an effect keeps that to a
  // state change the shell can act on, rather than a write during the gesture's own render.
  const carrying = drag !== null && !drag.settled;
  useEffect(() => {
    onDragging(carrying);
    return () => onDragging(false);
  }, [carrying, onDragging]);

  const cancel = useCallback(() => {
    settledAgainst.current = null;
    live.current = null;
    setDrag(null);
  }, []);

  const start = useCallback((index: number) => {
    const next = { id: queue[index].id, from: index, to: index, settled: false };
    live.current = next;
    setDrag(next);
  }, [queue]);

  // The queue is re-read while a finger is down, and a task a workspace takes leaves it, so the
  // row being carried is found again by its id. Trusting the position it had when the drag began
  // would move whichever task had since slid into it, or none at all.
  const carriedNow = useCallback((drag: Drag) => {
    const from = queue.findIndex(item => item.id === drag.id);
    return from < 0 ? null : { from, task: queue[from] };
  }, [queue]);

  const move = useCallback((dy: number) => {
    const current = live.current;
    if (current === null || current.settled) return;
    const here = carriedNow(current);
    if (here === null) return cancel();
    const to = reorderTarget(here.from, dy, pitch, queue.length);
    if (to === current.to && here.from === current.from) return;
    const next = { ...current, from: here.from, to };
    live.current = next;
    setDrag(next);
  }, [cancel, carriedNow, pitch, queue.length]);

  const drop = useCallback((dy: number) => {
    const current = live.current;
    if (current === null) return;
    const here = carriedNow(current);
    if (here === null) return cancel();
    const to = reorderTarget(here.from, dy, pitch, queue.length);
    if (to === here.from) {
      live.current = null;
      setDrag(null);
      return;
    }
    const settled = { ...current, from: here.from, to, settled: true };
    live.current = settled;
    settledAgainst.current = queue;
    setDrag(settled);
    onMove(here.task, to);
  }, [cancel, carriedNow, onMove, pitch, queue]);

  return (
    <>
      {queue.map((task, index) => (
        <QueueRow
          key={`${task.owner}/${task.repository}/${task.id}`}
          task={task}
          index={index}
          count={queue.length}
          pitch={pitch}
          drag={drag}
          onMeasure={index === 0 ? setRowHeight : undefined}
          onStart={start}
          onMove={move}
          onDrop={drop}
          onCancel={cancel}
          onStep={onMove}
          onRemove={onRemove}
        />
      ))}
    </>
  );
}

/**
 * One queued task: its number and title on one line, the repository under it, and the grip that
 * reorders it.
 *
 * The grip is dragged rather than tapped because the order is the point of this list and stepping
 * a task through it one press at a time says nothing about where it will land. It still answers
 * the accessibility actions, because a drag is reachable by neither a screen reader nor a keyboard
 * and the order must not be something only a pointer can change.
 */
function QueueRow({
  task,
  index,
  count,
  pitch,
  drag,
  onMeasure,
  onStart,
  onMove,
  onDrop,
  onCancel,
  onStep,
  onRemove,
}: {
  task: QueuedTask;
  index: number;
  count: number;
  pitch: number;
  drag: Drag | null;
  onMeasure?: (height: number) => void;
  onStart: (index: number) => void;
  onMove: (dy: number) => void;
  onDrop: (dy: number) => void;
  onCancel: () => void;
  onStep: (task: QueuedTask, index: number) => void;
  onRemove: (task: QueuedTask) => void;
}) {
  const carried = drag !== null && drag.id === task.id;
  const lift = useRef(new Animated.Value(0)).current;
  const shift = useRef(new Animated.Value(0)).current;

  // The gesture handlers are built once and must not close over this render's values: after a
  // reorder the row's index changes, and a captured one would drop the task at a position it no
  // longer occupies.
  const latest = useRef({ index, count, pitch, onStart, onMove, onDrop, onCancel });
  useEffect(() => {
    latest.current = { index, count, pitch, onStart, onMove, onDrop, onCancel };
  });

  const responder = useRef(
    PanResponder.create({
      // Claimed on movement, never on touch alone, so the grip can still be pressed and a gesture
      // that turns out to be a scroll is left to the list.
      onStartShouldSetPanResponder: () => false,
      onMoveShouldSetPanResponder: (_, gesture) => isDrag(gesture.dx, gesture.dy),
      // Captured as well: the enclosing scroll view would otherwise take the same swipe first.
      onMoveShouldSetPanResponderCapture: (_, gesture) => isDrag(gesture.dx, gesture.dy),
      onPanResponderGrant: () => {
        lift.setValue(0);
        latest.current.onStart(latest.current.index);
      },
      onPanResponderMove: (_, gesture) => {
        const { index: from, pitch: step, count: rows } = latest.current;
        lift.setValue(dragOffset(from, gesture.dy, step, rows));
        latest.current.onMove(gesture.dy);
      },
      onPanResponderRelease: (_, gesture) => {
        latest.current.onDrop(gesture.dy);
      },
      onPanResponderTerminate: () => {
        lift.setValue(0);
        latest.current.onCancel();
      },
      // Once a row is being carried nothing may take the gesture away mid-drag, or the row is left
      // somewhere nobody chose.
      onPanResponderTerminationRequest: () => false,
      onShouldBlockNativeResponder: () => true,
    }),
  ).current;

  const settling = carried && drag.settled;
  const target = drag === null ? 0 : rowShift(index, drag.from, drag.to, pitch);

  useEffect(() => {
    if (carried) return;
    Animated.timing(shift, { toValue: target, duration: 140, useNativeDriver: true }).start();
  }, [carried, shift, target]);

  // The finger has lifted and the queue has not answered yet, so the row eases into the slot it
  // chose instead of springing back to where the drag began and moving a second time.
  useEffect(() => {
    if (!settling || drag === null) return;
    Animated.timing(lift, {
      toValue: restingOffset(drag.from, drag.to, pitch),
      duration: 140,
      useNativeDriver: true,
    }).start();
  }, [settling, drag?.from, drag?.to, lift, pitch]);

  const step = (delta: number) => {
    const to = Math.min(Math.max(index + delta, 0), count - 1);
    if (to !== index) onStep(task, to);
  };

  // A drag is reachable by a pointer and nothing else. On the web the grip therefore takes focus
  // and answers the arrow keys, because the buttons that used to step a task through the queue are
  // gone and the order must not be something only a mouse or a finger can change.
  const keyboard = Platform.OS === 'web'
    ? ({
      tabIndex: 0,
      onKeyDown: (event: { key: string; preventDefault: () => void }) => {
        const delta = event.key === 'ArrowDown' ? 1 : event.key === 'ArrowUp' ? -1 : 0;
        if (delta === 0) return;
        event.preventDefault();
        step(delta);
      },
    } as Record<string, unknown>)
    : null;

  return (
    <Animated.View
      onLayout={onMeasure ? event => onMeasure(event.nativeEvent.layout.height) : undefined}
      style={[
        styles.queueRow,
        carried && styles.queueRowCarried,
        { transform: [{ translateY: carried ? lift : shift }] },
      ]}
    >
      <View
        accessibilityRole="adjustable"
        accessibilityLabel={`Reorder ${taskLabel(task)}`}
        accessibilityActions={reorderActions}
        onAccessibilityAction={event => step(event.nativeEvent.actionName === 'increment' ? 1 : -1)}
        style={[styles.grip, gripTouchAction]}
        {...keyboard}
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
      <QueueAction label="Dequeue" tone="danger" onPress={() => onRemove(task)} />
    </Animated.View>
  );
}

/**
 * On the web the browser scrolls a touch drag itself, and the gesture system here never sees it.
 * Only `touch-action: none` on the grip stops that, so a drag on the grip is a drag rather than a
 * scrolled page. The grip exists to be dragged, so giving up scrolling on it costs nothing; the
 * rest of the row still scrolls the list normally. The property is web-only, so native is left
 * alone rather than handed a style it does not know, and React Native's own style type has no name
 * for it either: the cast is what says this is meant for the web renderer rather than for that one.
 */
const gripTouchAction = Platform.OS === 'web'
  ? ({ touchAction: 'none' } as unknown as ViewStyle)
  : null;

const reorderActions = [
  { name: 'increment', label: 'Move later' },
  { name: 'decrement', label: 'Move sooner' },
] as const;

/**
 * Whether a gesture is a reorder. It has to travel far enough to be deliberate and be more down
 * the screen than across it, so a swipe that was meant for something else is not read as a drag.
 */
function isDrag(dx: number, dy: number): boolean {
  return Math.abs(dy) > dragThreshold && Math.abs(dy) > Math.abs(dx);
}

function QueueAction({ label, tone, onPress }: { label: string; tone?: 'danger'; onPress: () => void }) {
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={label} style={styles.rowAction} onPress={onPress}>
      <Text style={tone === 'danger' ? styles.dangerText : styles.rowActionText}>{label}</Text>
    </Pressable>
  );
}

function Team({ standing, onOpen }: { standing: Standing; onOpen: (workspaceId: string) => void }) {
  if (standing.error) return <Empty title="Could not reach the Server" note={standing.error} action="Retry" onPress={standing.refresh} />;
  if (!standing.read && standing.loading) return <Empty title="Loading" note="Reading the team from the Server." />;
  if (!standing.team.length) return <Empty title="No agents yet" note="Each workspace on this Server is one agent's own space." />;
  return (
    <>
      {standing.team.map(item => (
        <Pressable
          key={item.workspaceId}
          accessibilityRole="button"
          accessibilityLabel={`Open ${item.displayName}`}
          onPress={() => onOpen(item.workspaceId)}
        >
          <StandingCard standing={item} />
        </Pressable>
      ))}
    </>
  );
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
  rowAction: {
    // The compact variant, because the full button's 44pt minimum is taller than the row it sits
    // in. Centred and sized to itself, or the row's cross-axis stretch makes a slab of it.
    ...auBox('button', 'buttonSecondary', 'buttonCompact'),
    alignSelf: 'center',
    alignItems: 'center',
    justifyContent: 'center',
  },
  danger: { color: agentUpTheme.colors.statusDanger },
  // 12, to match the compact box these two labels sit in.
  dangerText: { color: agentUpTheme.colors.statusDanger, fontSize: 12, fontWeight: '600' },
  rowActionText: { color: agentUpTheme.colors.accentSoft, fontSize: 12, fontWeight: '600' },
  queueRow: {
    ...auBox('card'),
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    minHeight: 64,
  },
  queueRowCarried: { borderColor: agentUpTheme.colors.accentSoft, zIndex: 1 },
  queueRowBody: { flexGrow: 1, flexShrink: 1, minWidth: 0, gap: 2 },
  grip: { alignSelf: 'stretch', paddingVertical: 10, paddingRight: 4, gap: 3, justifyContent: 'center' },
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
