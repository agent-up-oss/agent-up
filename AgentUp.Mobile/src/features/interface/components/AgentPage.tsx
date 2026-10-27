import { useEffect, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { AgentChatScreen } from '@agent-up/chat';
import { getChanges } from '@/features/git/providers/GitApiProvider';
import { flattenChangeTree } from '@/features/git/providers/GitChangeTreeProvider';
import type { GitChangeNode } from '@/features/git/models/GitChanges';
import type { ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { Workspace } from '@/features/workspaces/models/Workspace';
import { agentUpTheme, auBox } from '@agent-up/design-system/native';
import {
  agentSubtitle,
  applicationAddress,
  checkSummary,
  findingSummary,
  taskFacts,
  type AgentSection,
} from '../models/AgentWorkspace';
import type { TeamStanding } from '../models/TeamStanding';
import { stateLabel, whoIs } from '../services/TeamStandingService';

/**
 * One agent's own page: their workspace, read from the inside.
 *
 * The team says what each agent is doing; this says what they are doing it in. It is the same
 * workspace the classic shell opens — the same applications, the same chat, the same changes —
 * arranged around the agent rather than around the repository, because here a workspace is a
 * person doing a job rather than a checkout.
 */
export function AgentPage({
  standing,
  workspace,
  server,
  onBack,
}: {
  standing: TeamStanding;
  workspace: Workspace | null;
  server: ServerSession | null;
  onBack: () => void;
}) {
  const [section, setSection] = useState<AgentSection>('overview');

  return (
    <View style={styles.page}>
      <View style={styles.head}>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Back to the team"
          onPress={onBack}
          style={styles.backButton}
        >
          <Text style={styles.backGlyph}>‹</Text>
        </Pressable>
        <View style={styles.headBody}>
          <Text style={styles.title} numberOfLines={1}>{standing.displayName}</Text>
          <Text style={styles.subtitle} numberOfLines={1}>{agentSubtitle(standing)}</Text>
        </View>
      </View>

      <View style={styles.sections}>
        {(['overview', 'chat', 'git'] as AgentSection[]).map(item => (
          <Pressable
            key={item}
            accessibilityRole="tab"
            accessibilityLabel={sectionLabel(item)}
            accessibilityState={{ selected: section === item }}
            style={[styles.section, section === item && styles.sectionOn]}
            onPress={() => setSection(item)}
          >
            <Text style={[styles.sectionText, section === item && styles.sectionTextOn]}>
              {sectionLabel(item)}
            </Text>
          </Pressable>
        ))}
      </View>

      {section === 'overview' ? <Overview standing={standing} workspace={workspace} /> : null}
      {section === 'chat' ? (
        <View style={styles.fill}>
          <AgentChatScreen
            workspace={{ id: standing.workspaceId, displayName: standing.displayName }}
            server={server}
          />
        </View>
      ) : null}
      {section === 'git' ? <GitSection standing={standing} server={server} /> : null}
    </View>
  );
}

function sectionLabel(section: AgentSection): string {
  return section === 'overview' ? 'Overview' : section === 'chat' ? 'Chat' : 'Git';
}

/** What the agent is working on, and what is running in the workspace while they do it. */
function Overview({ standing, workspace }: { standing: TeamStanding; workspace: Workspace | null }) {
  const facts = taskFacts(standing, workspace);
  const applications = workspace?.applications ?? [];
  return (
    <ScrollView contentContainerStyle={styles.body}>
      {standing.kind === 'waiting' || standing.kind === 'halted' ? (
        <View style={[styles.card, standing.kind === 'halted' ? styles.cardDanger : styles.cardWarning]}>
          <Text style={styles.cardTitle}>{stateLabel(standing.kind)}</Text>
          <Text style={styles.note}>{whoIs(standing)}</Text>
          {standing.reason ? <Text style={styles.note}>{standing.reason}</Text> : null}
          {standing.diagnosticCode ? <Text style={styles.code}>{standing.diagnosticCode}</Text> : null}
        </View>
      ) : null}

      <Heading text="CURRENT TASK" />
      {facts.length ? (
        <View style={styles.card}>
          {facts.map(fact => (
            <View key={fact.label} style={styles.factRow}>
              <Text style={styles.factKey}>{fact.label}</Text>
              <Text style={styles.factValue} numberOfLines={1} ellipsizeMode="tail">{fact.value}</Text>
            </View>
          ))}
        </View>
      ) : (
        <Text style={styles.note}>Nothing in hand. This agent is not working a task right now.</Text>
      )}

      <Heading text={`APPLICATIONS · ${applications.length}`} />
      {applications.length ? (
        applications.map(application => (
          <View key={application.name} style={styles.row}>
            <View style={styles.rowBody}>
              <Text style={styles.cardTitle} numberOfLines={1} ellipsizeMode="tail">{application.name}</Text>
              <Text style={styles.code} numberOfLines={1}>{applicationAddress(application)}</Text>
            </View>
            <Text style={[styles.note, application.state === 'Running' ? styles.live : null]}>
              {application.state}
            </Text>
          </View>
        ))
      ) : (
        <Text style={styles.note}>Nothing is running in this workspace.</Text>
      )}
    </ScrollView>
  );
}

/**
 * The proposal this agent has open and the files it changes. The changes are read here rather
 * than carried from the team, because the team is a summary and this is the thing itself.
 */
function GitSection({ standing, server }: { standing: TeamStanding; server: ServerSession | null }) {
  const [changes, setChanges] = useState<GitChangeNode[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let current = true;
    if (!server) return;
    getChanges(server, standing.workspaceId)
      .then(result => {
        if (!current) return;
        setChanges(flattenChangeTree(result).filter(node => !node.isDirectory));
        setError(null);
      })
      .catch(cause => { if (current) setError(cause instanceof Error ? cause.message : 'Could not read the changes.'); });
    return () => { current = false; };
  }, [server, standing.workspaceId]);

  return (
    <ScrollView contentContainerStyle={styles.body}>
      <Heading text="PROPOSAL" />
      {standing.forge.proposalNumber ? (
        <View style={styles.card}>
          <Text style={styles.cardTitle} numberOfLines={2}>
            #{standing.forge.proposalNumber} {standing.forge.proposalTitle}
          </Text>
          <Text style={styles.note}>{checkSummary(standing)}</Text>
          <Text style={styles.note}>{findingSummary(standing)}</Text>
          {standing.forge.mergeState ? (
            <Text style={styles.code}>{standing.forge.mergeState}</Text>
          ) : null}
        </View>
      ) : (
        <Text style={styles.note}>{standing.forge.detail ?? 'No proposal open on this workspace.'}</Text>
      )}

      <Heading text={`CHANGES · ${changes?.length ?? 0}`} />
      {error ? <Text style={[styles.note, styles.danger]}>{error}</Text> : null}
      {changes === null && !error ? <Text style={styles.note}>Reading the working tree…</Text> : null}
      {changes?.length === 0 ? <Text style={styles.note}>The working tree is clean.</Text> : null}
      {(changes ?? []).map(change => (
        <View key={change.key} style={styles.row}>
          <Text style={styles.changeMark}>{(change.status ?? '').slice(0, 1).toUpperCase()}</Text>
          <Text style={styles.changePath} numberOfLines={1} ellipsizeMode="head">{change.path}</Text>
        </View>
      ))}
    </ScrollView>
  );
}

function Heading({ text }: { text: string }) {
  return <Text style={styles.heading}>{text}</Text>;
}

const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: agentUpTheme.colors.canvas },
  fill: { flex: 1 },
  head: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    paddingHorizontal: agentUpTheme.spacing[4],
    paddingVertical: agentUpTheme.spacing[3],
    borderBottomWidth: 1,
    borderBottomColor: agentUpTheme.colors.borderSubtle,
  },
  backButton: { width: 36, height: 44, alignItems: 'flex-start', justifyContent: 'center', marginLeft: -6 },
  backGlyph: { color: agentUpTheme.colors.textSecondary, fontSize: 30, lineHeight: 32 },
  headBody: { flex: 1, minWidth: 0 },
  title: { color: agentUpTheme.colors.textPrimary, fontSize: 20, fontWeight: '700' },
  subtitle: { color: agentUpTheme.colors.textMuted, fontSize: 12 },
  sections: {
    ...auBox('tabs'),
    flexDirection: 'row',
    gap: 6,
    marginHorizontal: agentUpTheme.spacing[4],
    marginTop: agentUpTheme.spacing[3],
  },
  section: auBox('tab'),
  sectionOn: auBox('tab', 'tabSelected'),
  sectionText: { color: agentUpTheme.colors.textMuted, fontSize: 13, fontWeight: '600' },
  sectionTextOn: { color: agentUpTheme.colors.textPrimary },
  body: { padding: agentUpTheme.spacing[4], gap: 10, paddingBottom: agentUpTheme.spacing[8] },
  heading: {
    color: agentUpTheme.colors.textMuted,
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 1,
    marginTop: 6,
  },
  card: { ...auBox('card'), gap: 4 },
  cardDanger: auBox('cardDanger'),
  cardWarning: { borderColor: agentUpTheme.colors.borderStrong },
  cardTitle: { color: agentUpTheme.colors.textPrimary, fontSize: 14, fontWeight: '600' },
  note: { color: agentUpTheme.colors.textMuted, fontSize: 12 },
  danger: { color: agentUpTheme.colors.statusDanger },
  live: { color: agentUpTheme.colors.accentBright },
  code: { color: agentUpTheme.colors.textMuted, fontSize: 12, fontFamily: agentUpTheme.fonts.mono },
  row: { ...auBox('card'), flexDirection: 'row', alignItems: 'center', gap: 12, minHeight: 56 },
  rowBody: { flexGrow: 1, flexShrink: 1, minWidth: 0, gap: 2 },
  factRow: { flexDirection: 'row', alignItems: 'baseline', gap: 10 },
  factKey: { color: agentUpTheme.colors.textFaint, fontSize: 12, width: 72 },
  factValue: { color: agentUpTheme.colors.textSecondary, fontSize: 13, flexShrink: 1 },
  changeMark: {
    color: agentUpTheme.colors.textFaint,
    fontSize: 12,
    fontFamily: agentUpTheme.fonts.mono,
    width: 16,
  },
  changePath: {
    color: agentUpTheme.colors.textSecondary,
    fontSize: 12,
    fontFamily: agentUpTheme.fonts.mono,
    flexShrink: 1,
  },
});
