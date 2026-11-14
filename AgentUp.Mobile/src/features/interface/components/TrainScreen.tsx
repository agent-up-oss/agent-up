import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  Animated,
  Image,
  PanResponder,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
  type ViewStyle,
  Platform,
} from 'react-native';
import { agentUpTheme, auBox } from '@agent-up/design-system/native';
import type { ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import {
  decisionCategories,
  firstUnjudged,
  inCategory,
  nextCard,
  pendingCount,
  recordedBy,
  repositoryKey,
  swipeTaken,
  type DecisionCategoryId,
  type DecisionRepository,
  type DecisionVerdict,
  type ProductDecision,
} from '../models/ProductDecision';
import type { TrainingState } from '../controllers/TrainingContext';
import { readEvidenceImage } from '../providers/TrainingProvider';

/** How far a card must be thrown before it counts as moving off it rather than reading it. */
const swipeThreshold = 80;

/**
 * The backlog of product decisions the agents have taken, for a product owner to judge.
 *
 * It opens on the repositories rather than on the decisions: which product this is about is the
 * first thing a reader needs, and the counts say where the backlog actually is before anything
 * is opened.
 */
export function TrainScreen({
  training,
  server,
  onExport,
}: {
  training: TrainingState;
  server: ServerSession | null;
  onExport: () => void;
}) {
  if (training.error && !training.read) {
    return <Empty title="Could not reach the Server" note={training.error} />;
  }

  if (!training.read && training.loading) {
    return <Empty title="Loading" />;
  }

  if (training.opened) {
    return <RepositoryBacklog training={training} server={server} />;
  }

  if (training.repositories.length === 0) {
    return <Empty title="No decisions yet" />;
  }

  return <RepositoryTiles training={training} onExport={onExport} />;
}

/** Which product a reader wants to think about, and how much of it is waiting. */
function RepositoryTiles({ training, onExport }: { training: TrainingState; onExport: () => void }) {
  return (
    <ScrollView contentContainerStyle={styles.body}>
      <View style={styles.headRow}>
        <Text style={styles.heading}>REPOSITORIES</Text>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Export decisions"
          onPress={onExport}
          style={styles.exportButton}
        >
          <Text style={styles.exportText}>Export</Text>
        </Pressable>
      </View>
      <View style={styles.tiles}>
        {training.repositories.map(repository => (
          <Pressable
            key={repositoryKey(repository)}
            accessibilityRole="button"
            accessibilityLabel={`Open ${repositoryKey(repository)}`}
            style={styles.tile}
            onPress={() => training.open(repository)}
          >
            <Text style={styles.tileName} numberOfLines={1} ellipsizeMode="middle">
              {repositoryKey(repository)}
            </Text>
            <Text style={[styles.tileCount, repository.pending > 0 ? styles.waiting : null]}>
              {repository.pending > 0 ? `${repository.pending} waiting` : 'All judged'}
            </Text>
            <Text style={styles.note}>{repository.reviewed} judged</Text>
          </Pressable>
        ))}
      </View>
    </ScrollView>
  );
}

/** One repository's decisions, split the three ways a reader thinks about them. */
function RepositoryBacklog({ training, server }: { training: TrainingState; server: ServerSession | null }) {
  const [category, setCategory] = useState<DecisionCategoryId>('product-vision');
  const decisions = useMemo(() => inCategory(training.decisions, category), [training.decisions, category]);

  return (
    <View style={styles.fill}>
      <View style={styles.backRow}>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Back to repositories"
          onPress={() => { training.open(null); training.refresh(); }}
          style={styles.backButton}
        >
          <Text style={styles.backGlyph}>‹</Text>
        </Pressable>
        <Text style={styles.tileName} numberOfLines={1} ellipsizeMode="middle">
          {repositoryKey(training.opened!)}
        </Text>
      </View>

      <View style={styles.tabs}>
        {decisionCategories.map(item => {
          // Counted from what is loaded rather than from the tile that was read on the way in,
          // so judging a card changes the number beside its category straight away.
          const waiting = pendingCount(inCategory(training.decisions, item.id));
          const selected = item.id === category;
          return (
            <Pressable
              key={item.id}
              accessibilityRole="tab"
              accessibilityLabel={item.label}
              accessibilityState={{ selected }}
              style={[styles.tab, selected && styles.tabOn]}
              onPress={() => setCategory(item.id)}
            >
              <Text style={[styles.tabText, selected && styles.tabTextOn]} numberOfLines={1}>
                {item.label}
              </Text>
              {waiting > 0 ? <Text style={styles.tabCount}>{waiting}</Text> : null}
            </Pressable>
          );
        })}
      </View>

      <DecisionDeck decisions={decisions} training={training} server={server} />
    </View>
  );
}

/**
 * One decision at a time, swiped up and down.
 *
 * The deck is keyed by the category so that moving between categories starts at the top of the
 * new one rather than wherever the last one was left.
 */
function DecisionDeck({
  decisions,
  training,
  server,
}: {
  decisions: ProductDecision[];
  training: TrainingState;
  server: ServerSession | null;
}) {
  const [index, setIndex] = useState(0);
  const slide = useRef(new Animated.Value(0)).current;
  // The responder is built once, so it reads the live index through a ref rather than the value
  // that was captured when the gesture handlers were made.
  const position = useRef(index);
  position.current = index;
  const count = decisions.length;
  const total = useRef(count);
  total.current = count;

  useEffect(() => {
    setIndex(firstUnjudged(decisions));
    // Only when the deck itself changes: re-seeking on every verdict would fight the reader.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [decisions.length, decisions[0]?.id]);

  const move = useCallback((direction: 1 | -1) => {
    setIndex(current => nextCard(current, direction, total.current));
  }, []);

  const responder = useMemo(() => PanResponder.create({
    onMoveShouldSetPanResponder: (_event, gesture) =>
      Math.abs(gesture.dy) > 12 && Math.abs(gesture.dy) > Math.abs(gesture.dx),
    onPanResponderMove: (_event, gesture) => slide.setValue(gesture.dy / 6),
    onPanResponderRelease: (_event, gesture) => {
      const taken = swipeTaken(gesture.dy, swipeThreshold);
      if (taken !== 0) move(taken);
      Animated.spring(slide, { toValue: 0, useNativeDriver: true, speed: 20 }).start();
    },
    onPanResponderTerminationRequest: () => false,
  }), [slide, move]);

  if (count === 0) {
    return <Empty title="Nothing here" />;
  }

  const decision = decisions[Math.min(index, count - 1)];

  return (
    <View style={styles.fill}>
      <View style={styles.deckHead}>
        <Text style={styles.note}>{index + 1} of {count}</Text>
        <View style={styles.deckSteps}>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Previous decision"
            disabled={index === 0}
            onPress={() => move(-1)}
            style={styles.stepButton}
          >
            <Text style={[styles.stepGlyph, index === 0 && styles.stepOff]}>↑</Text>
          </Pressable>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Next decision"
            disabled={index >= count - 1}
            onPress={() => move(1)}
            style={styles.stepButton}
          >
            <Text style={[styles.stepGlyph, index >= count - 1 && styles.stepOff]}>↓</Text>
          </Pressable>
        </View>
      </View>

      <Animated.View
        style={[styles.fill, swipeTouchAction, { transform: [{ translateY: slide }] }]}
        {...responder.panHandlers}
      >
        <DecisionCard
          key={decision.id}
          decision={decision}
          server={server}
          onJudged={(verdict, note) => {
            void training.judge(decision, verdict, note);
            move(1);
          }}
        />
      </Animated.View>
    </View>
  );
}

/** One decision, with the way to judge it. */
function DecisionCard({
  decision,
  server,
  onJudged,
}: {
  decision: ProductDecision;
  server: ServerSession | null;
  onJudged: (verdict: DecisionVerdict, note: string | null) => void;
}) {
  const [verdict, setVerdict] = useState<DecisionVerdict>(decision.verdict);
  const [note, setNote] = useState(decision.note ?? '');

  return (
    <ScrollView contentContainerStyle={styles.cardBody} keyboardShouldPersistTaps="handled">
      <Text style={styles.decidedBy}>{recordedBy(decision)}</Text>
      <Text style={styles.headline}>{decision.headline}</Text>

      <Text style={styles.detail}>{decision.detail}</Text>

      <Text style={styles.heading}>BASED ON</Text>
      <Text style={styles.detail}>{decision.evidence}</Text>

      {decision.evidenceImages.map(image => (
        <EvidenceImage key={image.evidenceId} server={server} evidenceId={image.evidenceId} />
      ))}

      <View style={styles.verdictRow}>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Good call"
          accessibilityState={{ selected: verdict === 'good' }}
          onPress={() => setVerdict('good')}
          style={[styles.verdictButton, verdict === 'good' && styles.verdictGoodOn]}
        >
          <Text style={[styles.verdictText, verdict === 'good' ? styles.goodOn : styles.good]}>
            👍 Good call
          </Text>
        </Pressable>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Bad call"
          accessibilityState={{ selected: verdict === 'bad' }}
          onPress={() => setVerdict('bad')}
          style={[styles.verdictButton, verdict === 'bad' && styles.verdictBadOn]}
        >
          <Text style={[styles.verdictText, verdict === 'bad' ? styles.badOn : styles.bad]}>
            👎 Bad call
          </Text>
        </Pressable>
      </View>

      <TextInput
        accessibilityLabel="Note"
        placeholder="Why, if it is worth saying (optional)"
        placeholderTextColor={agentUpTheme.colors.textFaint}
        value={note}
        onChangeText={setNote}
        multiline
        style={styles.noteInput}
      />

      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Submit verdict"
        disabled={verdict === 'pending'}
        onPress={() => onJudged(verdict, note.trim().length > 0 ? note.trim() : null)}
        style={[styles.submit, verdict === 'pending' && styles.submitOff]}
      >
        <Text style={[styles.submitText, verdict === 'pending' && styles.submitTextOff]}>Submit</Text>
      </Pressable>

      {decision.verdict !== 'pending' ? (
        <Text style={styles.note}>
          You judged this {decision.verdict === 'good' ? 'a good call' : 'a bad call'}.
        </Text>
      ) : null}
    </ScrollView>
  );
}

/**
 * One picture behind a decision. It is read rather than linked, because an image tag cannot
 * carry the Server's credentials, and dropped quietly when it cannot be read: a missing picture
 * should not take the decision off the screen.
 */
function EvidenceImage({ server, evidenceId }: { server: ServerSession | null; evidenceId: string }) {
  const [uri, setUri] = useState<string | null>(null);

  useEffect(() => {
    if (!server) return;
    let current = true;
    readEvidenceImage(server, evidenceId)
      .then(found => { if (current) setUri(found); })
      .catch(() => undefined);
    return () => { current = false; };
  }, [server, evidenceId]);

  if (!uri) return null;
  return (
    <Image
      accessibilityLabel="Evidence"
      source={{ uri }}
      style={styles.evidenceImage}
      resizeMode="contain"
    />
  );
}

function Empty({ title, note }: { title: string; note?: string }) {
  return (
    <View style={styles.empty}>
      <Text style={styles.emptyTitle}>{title}</Text>
      {note ? <Text style={styles.note}>{note}</Text> : null}
    </View>
  );
}

/** On the web a vertical drag is the page's to scroll unless the card claims it first. */
const swipeTouchAction = Platform.OS === 'web'
  ? ({ touchAction: 'none' } as unknown as ViewStyle)
  : null;

const styles = StyleSheet.create({
  fill: { flex: 1 },
  body: { padding: agentUpTheme.spacing[4], gap: 10, paddingBottom: agentUpTheme.spacing[8] },
  headRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  heading: {
    color: agentUpTheme.colors.textMuted,
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 1,
    marginTop: 6,
  },
  note: { color: agentUpTheme.colors.textMuted, fontSize: 12 },
  exportButton: {
    ...auBox('button', 'buttonSecondary', 'buttonCompact'),
    alignItems: 'center',
    justifyContent: 'center',
  },
  exportText: { color: agentUpTheme.colors.textSecondary, fontSize: 13, fontWeight: '600' },
  tiles: { flexDirection: 'row', flexWrap: 'wrap', gap: 10 },
  tile: { ...auBox('card'), flexGrow: 1, flexBasis: '45%', gap: 4, minHeight: 92 },
  tileName: { color: agentUpTheme.colors.textPrimary, fontSize: 15, fontWeight: '700', flexShrink: 1 },
  tileCount: { color: agentUpTheme.colors.textSecondary, fontSize: 13, fontWeight: '600' },
  waiting: { color: agentUpTheme.colors.accentBright },
  backRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    paddingHorizontal: agentUpTheme.spacing[4],
    paddingTop: agentUpTheme.spacing[3],
  },
  backButton: { width: 32, height: 40, alignItems: 'flex-start', justifyContent: 'center', marginLeft: -6 },
  backGlyph: { color: agentUpTheme.colors.textSecondary, fontSize: 28, lineHeight: 30 },
  tabs: {
    ...auBox('tabs'),
    flexDirection: 'row',
    gap: 6,
    marginHorizontal: agentUpTheme.spacing[4],
    marginTop: agentUpTheme.spacing[3],
  },
  tab: { ...auBox('tab'), flexDirection: 'row', alignItems: 'center', gap: 6 },
  tabOn: auBox('tab', 'tabSelected'),
  tabText: { color: agentUpTheme.colors.textMuted, fontSize: 12, fontWeight: '600', flexShrink: 1 },
  tabTextOn: { color: agentUpTheme.colors.textPrimary },
  tabCount: { color: agentUpTheme.colors.accentBright, fontSize: 12, fontWeight: '700' },
  deckHead: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: agentUpTheme.spacing[4],
    paddingTop: agentUpTheme.spacing[3],
  },
  deckSteps: { flexDirection: 'row', gap: 4 },
  stepButton: { width: 40, height: 36, alignItems: 'center', justifyContent: 'center' },
  stepGlyph: { color: agentUpTheme.colors.textSecondary, fontSize: 18 },
  stepOff: { color: agentUpTheme.colors.textFaint },
  cardBody: { padding: agentUpTheme.spacing[4], gap: 10, paddingBottom: agentUpTheme.spacing[8] },
  decidedBy: { color: agentUpTheme.colors.textFaint, fontSize: 12 },
  headline: { color: agentUpTheme.colors.textPrimary, fontSize: 22, fontWeight: '700', lineHeight: 28 },
  detail: { color: agentUpTheme.colors.textSecondary, fontSize: 14, lineHeight: 20 },
  evidenceImage: {
    ...auBox('card'),
    width: '100%',
    height: 200,
  },
  verdictRow: { flexDirection: 'row', gap: 10, marginTop: 6 },
  // Unchosen, both read as plain buttons; the one you pick becomes solid, so a glance at the
  // card says what you decided rather than making you read two labels to find out.
  verdictButton: {
    ...auBox('button', 'buttonSecondary'),
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  verdictGoodOn: auBox('button'),
  verdictBadOn: auBox('button', 'buttonDanger'),
  verdictText: { fontSize: 14, fontWeight: '700' },
  good: { color: agentUpTheme.colors.accentBright },
  bad: { color: agentUpTheme.colors.statusDanger },
  goodOn: { color: auBox('button').color },
  badOn: { color: auBox('button', 'buttonDanger').color },
  noteInput: {
    ...auBox('input'),
    minHeight: 72,
    textAlignVertical: 'top',
  },
  submit: { ...auBox('button'), alignItems: 'center', justifyContent: 'center' },
  submitOff: auBox('buttonDisabled'),
  submitText: { color: auBox('button').color, fontSize: 15, fontWeight: '700' },
  submitTextOff: { color: auBox('button').color },
  empty: { flex: 1, alignItems: 'center', justifyContent: 'center', padding: agentUpTheme.spacing[6], gap: 6 },
  emptyTitle: { color: agentUpTheme.colors.textPrimary, fontSize: 16, fontWeight: '700' },
});
