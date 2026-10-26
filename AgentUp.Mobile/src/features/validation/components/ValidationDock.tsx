import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { PanResponder, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import { PublicIcon } from '@/features/shell/components/PublicIcon';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import type { ValidationFlow, ValidationRunState, ValidationStageView } from '../models/ValidationFlow';
import { getValidationFlow, listValidationFlows } from '../providers/ValidationFlowApiProvider';
import {
  adjacentFlowIndex,
  compactValidationCopy,
  idleStageViews,
  validationMenuLabel,
} from '../providers/ValidationPanelProvider';
import {
  runValidationReplay,
  type ValidationReplayHost,
  type ValidationReplayReporter,
} from '../providers/ValidationReplayProvider';

type ValidationDockProps = {
  workspaceId: string;
  applicationName: string;
  replayHost: ValidationReplayHost | null;
};

type FlowItem = {
  flow: ValidationFlow;
  stages: ValidationStageView[];
  loading: boolean;
  runState: ValidationRunState;
  resultMessage: string | null;
};

/** Compact 50px dock under the application webview. */
export function ValidationDock({ workspaceId, applicationName, replayHost }: ValidationDockProps) {
  const { server } = useWorkspaces();
  const [items, setItems] = useState<FlowItem[]>([]);
  const [selectedIndex, setSelectedIndex] = useState(0);
  const [menuOpen, setMenuOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const playAbort = useRef<AbortController | null>(null);
  const selectedIndexRef = useRef(0);
  selectedIndexRef.current = selectedIndex;

  useEffect(() => {
    let active = true;
    if (!server) {
      setError('No Server connection is available.');
      setItems([]);
      return () => { active = false; };
    }
    setError(null);
    void listValidationFlows(server, workspaceId, applicationName)
      .then(flows => {
        if (!active) return;
        setItems(flows.map(flow => ({
          flow,
          stages: idleStageViews(flow),
          loading: false,
          runState: 'pending',
          resultMessage: null,
        })));
        setSelectedIndex(0);
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

  const select = useCallback((index: number) => {
    playAbort.current?.abort();
    setSelectedIndex(index);
    setMenuOpen(false);
  }, []);

  const play = useCallback(async (id: string) => {
    if (!server) return;
    playAbort.current?.abort();
    const controller = new AbortController();
    playAbort.current = controller;
    patch(id, item => ({
      ...item,
      loading: true,
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
      await runValidationReplay({
        flow: fresh,
        host: replayHost,
        reporter: createReporter(id, patch),
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

  const selected = items[selectedIndex] ?? null;
  const copy = selected
    ? compactValidationCopy({
      name: selected.flow.name,
      stages: selected.stages,
      resultMessage: selected.resultMessage ?? error,
    })
    : { title: error ?? 'No checks recorded', substep: '' };
  const playing = selected?.runState === 'running' || selected?.loading === true;

  const panResponder = useMemo(() => PanResponder.create({
    onMoveShouldSetPanResponder: (_event, gesture) => (
      items.length > 1
      && Math.abs(gesture.dy) > 12
      && Math.abs(gesture.dy) > Math.abs(gesture.dx)
    ),
    onPanResponderRelease: (_event, gesture) => {
      if (Math.abs(gesture.dy) < 24) return;
      const delta = gesture.dy < 0 ? 1 : -1;
      select(adjacentFlowIndex(selectedIndexRef.current, items.length, delta));
    },
  }), [items.length, select]);

  return (
    <View style={styles.wrap}>
      {menuOpen && (
        <View testID="validation-flows" style={styles.menu}>
          <ScrollView>
            {items.map((item, index) => (
              <Pressable
                key={item.flow.id}
                testID={`validation-flow-${item.flow.id}`}
                accessibilityRole="button"
                accessibilityLabel={item.flow.name}
                accessibilityState={{ selected: index === selectedIndex }}
                onPress={() => select(index)}
                style={[styles.menuRow, index === selectedIndex && styles.menuRowSelected]}>
                <Text numberOfLines={1} style={styles.menuName}>{item.flow.name}</Text>
              </Pressable>
            ))}
            {items.length === 0 && (
              <Text style={styles.substep}>{error ?? 'No checks recorded for this application.'}</Text>
            )}
          </ScrollView>
        </View>
      )}
      <View testID="validation-panel" style={styles.dock}>
        <Pressable
          testID="validation-play"
          accessibilityRole="button"
          accessibilityLabel={selected ? `Play ${selected.flow.name}` : 'Play validation'}
          disabled={!selected || playing}
          onPress={() => { if (selected) void play(selected.flow.id); }}
          style={[styles.tool, (!selected || playing) && styles.toolDisabled]}>
          <Text style={styles.playGlyph}>▶</Text>
        </Pressable>
        <View {...panResponder.panHandlers} style={styles.copy}>
          <Text numberOfLines={1} style={styles.title}>{copy.title}</Text>
          {!!copy.substep && <Text numberOfLines={1} style={styles.substep}>{copy.substep}</Text>}
        </View>
        <Pressable
          testID="validation.toggle"
          accessibilityRole="button"
          accessibilityLabel={validationMenuLabel(menuOpen)}
          onPress={() => setMenuOpen(open => !open)}
          style={styles.tool}>
          <PublicIcon name="menu" size={18} />
        </Pressable>
      </View>
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

const styles = StyleSheet.create({
  wrap: {
    position: 'relative',
    zIndex: 3,
    overflow: 'visible',
  },
  dock: {
    ...auBox('validationDock'),
    flexDirection: 'row',
    alignItems: 'center',
  },
  tool: {
    ...auBox('titleTool'),
    alignItems: 'center',
    justifyContent: 'center',
  },
  toolDisabled: auBox('lifecycleButtonDisabled'),
  playGlyph: { ...auText('chromeIcon'), fontSize: 16, lineHeight: 18 },
  copy: {
    ...auBox('validationDockCopy'),
    flex: 1,
    minWidth: 0,
    justifyContent: 'center',
  },
  title: { ...auText('workspaceName'), fontSize: agentUpTheme.typography.sizeUiSm },
  substep: { ...auText('muted'), fontSize: agentUpTheme.typography.sizeUiXs },
  menu: {
    ...auBox('validationDockList'),
    position: 'absolute',
    left: agentUpTheme.spacing[3],
    right: agentUpTheme.spacing[3],
    bottom: 50,
    maxHeight: 192,
    zIndex: 4,
  },
  menuRow: {
    ...auBox('choice'),
    paddingHorizontal: agentUpTheme.spacing[3],
    paddingVertical: agentUpTheme.spacing[2],
  },
  menuRowSelected: auBox('choiceSelected'),
  menuName: auText('workspaceName'),
});
