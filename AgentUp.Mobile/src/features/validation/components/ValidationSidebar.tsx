import { useCallback, useEffect, useRef, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import type { ValidationFlow, ValidationRunState, ValidationStageView } from '../models/ValidationFlow';
import { getValidationFlow, listValidationFlows } from '../providers/ValidationFlowApiProvider';
import {
  idleStageViews,
  validationRunGlyph,
} from '../providers/ValidationPanelProvider';
import {
  runValidationReplay,
  type ValidationReplayHost,
  type ValidationReplayReporter,
} from '../providers/ValidationReplayProvider';

type ValidationSidebarProps = {
  workspaceId: string;
  applicationName: string;
  replayHost: ValidationReplayHost | null;
};

type FlowItem = {
  flow: ValidationFlow;
  stages: ValidationStageView[];
  expanded: boolean;
  loading: boolean;
  runState: ValidationRunState;
  resultMessage: string | null;
};

/** Right-hand list of recorded checks for the open HTTP application. */
export function ValidationSidebar({ workspaceId, applicationName, replayHost }: ValidationSidebarProps) {
  const { server } = useWorkspaces();
  const [items, setItems] = useState<FlowItem[]>([]);
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const playAbort = useRef<AbortController | null>(null);

  useEffect(() => {
    let active = true;
    if (!server) {
      setError('No Server connection is available.');
      setItems([]);
      return () => { active = false; };
    }
    setError(null);
    setStatus(null);
    void listValidationFlows(server, workspaceId, applicationName)
      .then(flows => {
        if (!active) return;
        setItems(flows.map(flow => ({
          flow,
          stages: idleStageViews(flow),
          expanded: false,
          loading: false,
          runState: 'pending',
          resultMessage: null,
        })));
        setStatus(flows.length === 0 ? 'No checks recorded for this application.' : null);
      })
      .catch(reason => {
        if (!active) return;
        setError(reason instanceof Error ? reason.message : String(reason));
      });
    return () => { active = false; };
  }, [server, workspaceId, applicationName]);

  useEffect(() => () => playAbort.current?.abort(), []);

  const patch = useCallback((id: string, update: (item: FlowItem) => FlowItem) => {
    setItems(current => current.map(item => item.flow.id === id ? update(item) : item));
  }, []);

  const expand = useCallback((id: string) => {
    patch(id, item => ({ ...item, expanded: true }));
  }, [patch]);

  const play = useCallback(async (id: string) => {
    if (!server) return;
    playAbort.current?.abort();
    const controller = new AbortController();
    playAbort.current = controller;
    patch(id, item => ({
      ...item,
      loading: true,
      expanded: true,
      runState: 'running',
      resultMessage: null,
      stages: idleStageViews(item.flow),
    }));
    try {
      const fresh = await getValidationFlow(server, workspaceId, id);
      if (controller.signal.aborted) return;
      if (fresh == null) {
        setError('Validation flow was not found.');
        patch(id, item => ({ ...item, loading: false, runState: 'failed', resultMessage: 'Validation flow was not found.' }));
        return;
      }
      patch(id, item => ({ ...item, flow: fresh, stages: idleStageViews(fresh) }));
      if (!replayHost) {
        const message = 'Validation replay is not connected to the browser view.';
        patch(id, item => ({ ...item, loading: false, runState: 'failed', resultMessage: message }));
        return;
      }
      const reporter = createReporter(id, patch);
      await runValidationReplay({
        flow: fresh,
        host: replayHost,
        reporter,
        signal: controller.signal,
      });
    } catch (reason) {
      if (controller.signal.aborted) return;
      setError(reason instanceof Error ? reason.message : String(reason));
      patch(id, item => ({ ...item, loading: false, runState: 'failed' }));
    } finally {
      if (!controller.signal.aborted) {
        patch(id, item => ({ ...item, loading: false }));
      }
    }
  }, [patch, replayHost, server, workspaceId]);

  return (
    <View testID="validation-panel" style={styles.sidebar}>
      <View style={styles.head}>
        <View style={styles.copy}>
          <Text style={styles.kicker}>Validation</Text>
          <Text style={styles.muted}>Checks for the displayed app</Text>
        </View>
      </View>
      <ScrollView testID="validation-flows" contentContainerStyle={styles.list}>
        {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}
        {!!status && <Text style={styles.muted}>{status}</Text>}
        {items.map(item => (
          <View key={item.flow.id} testID={`validation-flow-${item.flow.id}`} style={styles.flow}>
            <View style={styles.flowHead}>
              <Text style={glyphStyle(item.runState)}>{validationRunGlyph(item.runState)}</Text>
              <Pressable
                accessibilityRole="button"
                accessibilityLabel={`Show steps for ${item.flow.name}`}
                onPress={() => expand(item.flow.id)}
                style={styles.flowCopy}>
                <Text style={styles.flowName}>{item.flow.name}</Text>
                {!!item.flow.description && <Text style={styles.muted}>{item.flow.description}</Text>}
                <Text style={styles.muted}>Starts at {item.flow.initialPath || '/'}</Text>
                {!!item.resultMessage && <Text style={styles.muted}>{item.resultMessage}</Text>}
              </Pressable>
              <Pressable
                accessibilityRole="button"
                accessibilityLabel={`Play ${item.flow.name}`}
                disabled={item.loading || item.runState === 'running'}
                onPress={() => { void play(item.flow.id); }}
                style={styles.play}>
                <Text style={styles.playLabel}>▶ Play</Text>
              </Pressable>
            </View>
            {item.expanded && (
              <View style={styles.steps}>
                {item.stages.map(stage => (
                  <View key={stage.id} style={styles.stage}>
                    <View style={styles.stageHead}>
                      <Text style={glyphStyle(stage.state)}>{validationRunGlyph(stage.state)}</Text>
                      <Text style={styles.stepTitle}>{stage.title}</Text>
                    </View>
                    <View style={styles.checks}>
                      {stage.checks.map((check, index) => (
                        <View key={`${stage.id}-${index}`} style={styles.checkRow}>
                          <Text style={glyphStyle(check.state)}>{validationRunGlyph(check.state)}</Text>
                          <Text style={styles.check}>{check.label}</Text>
                        </View>
                      ))}
                    </View>
                  </View>
                ))}
              </View>
            )}
          </View>
        ))}
      </ScrollView>
    </View>
  );
}

function createReporter(
  id: string,
  patch: (id: string, update: (item: FlowItem) => FlowItem) => void,
): ValidationReplayReporter {
  const updateStage = (item: FlowItem, stageId: string, next: Partial<ValidationStageView>): FlowItem => ({
    ...item,
    stages: item.stages.map(stage => stage.id === stageId ? { ...stage, ...next } : stage),
  });
  return {
    beginFlow: () => patch(id, item => ({
      ...item,
      expanded: true,
      runState: 'running',
      resultMessage: null,
      stages: item.stages.map(stage => ({
        ...stage,
        state: 'pending',
        checks: stage.checks.map(check => ({ ...check, state: 'pending' })),
      })),
    })),
    beginStage: stageId => patch(id, item => updateStage(item, stageId, { state: 'running' })),
    beginCheck: (stageId, checkIndex) => patch(id, item => ({
      ...item,
      stages: item.stages.map(stage => {
        if (stage.id !== stageId) return stage;
        return {
          ...stage,
          checks: stage.checks.map((check, index) => index === checkIndex ? { ...check, state: 'running' } : check),
        };
      }),
    })),
    completeCheck: (stageId, checkIndex, passed) => patch(id, item => ({
      ...item,
      stages: item.stages.map(stage => {
        if (stage.id !== stageId) return stage;
        return {
          ...stage,
          checks: stage.checks.map((check, index) => (
            index === checkIndex ? { ...check, state: passed ? 'passed' : 'failed' } : check
          )),
        };
      }),
    })),
    completeStage: (stageId, passed) => patch(id, item => updateStage(item, stageId, { state: passed ? 'passed' : 'failed' })),
    completeFlow: (passed, message) => patch(id, item => ({
      ...item,
      runState: passed ? 'passed' : 'failed',
      resultMessage: message ?? null,
      loading: false,
    })),
  };
}

function glyphStyle(state: ValidationRunState) {
  if (state === 'passed') return styles.glyphPassed;
  if (state === 'failed') return styles.glyphFailed;
  if (state === 'running') return styles.glyphRunning;
  return styles.glyph;
}

const styles = StyleSheet.create({
  sidebar: {
    ...auBox('validationSidebar'),
    flex: 1,
    maxWidth: 360,
    width: '100%',
  },
  head: {
    ...auBox('validationSidebarHead'),
    flexDirection: 'row',
    alignItems: 'flex-start',
  },
  copy: auBox('validationSidebarCopy'),
  kicker: auText('accent'),
  muted: auText('muted'),
  list: {
    gap: agentUpTheme.spacing[3],
    padding: agentUpTheme.spacing[3],
  },
  flow: auBox('validationFlow'),
  flowHead: {
    ...auBox('validationFlowHead'),
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: agentUpTheme.spacing[2],
  },
  flowCopy: {
    ...auBox('validationCopy'),
    flex: 1,
    gap: 4,
  },
  flowName: auText('workspaceName'),
  play: auBox('validationPlay'),
  playLabel: auText('validationPlay'),
  steps: auBox('validationSteps'),
  stage: {
    ...auBox('validationStage'),
    gap: 4,
    marginTop: agentUpTheme.spacing[2],
  },
  stageHead: { flexDirection: 'row', alignItems: 'flex-start', gap: agentUpTheme.spacing[2] },
  stepTitle: { ...auText('validationStep'), flex: 1 },
  checks: auBox('validationChecks'),
  checkRow: { flexDirection: 'row', alignItems: 'flex-start', gap: agentUpTheme.spacing[2] },
  check: { ...auText('validationCheck'), flex: 1 },
  glyph: auText('validationGlyph'),
  glyphPassed: auText('validationGlyph', 'validationGlyphPassed'),
  glyphFailed: auText('validationGlyph', 'validationGlyphFailed'),
  glyphRunning: auText('validationGlyph', 'validationGlyphRunning'),
  error: auText('badgeDanger'),
});
