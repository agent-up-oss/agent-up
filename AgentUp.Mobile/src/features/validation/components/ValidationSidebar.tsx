import { useCallback, useEffect, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import type { ValidationFlow, ValidationStage } from '../models/ValidationFlow';
import { getValidationFlow, listValidationFlows } from '../providers/ValidationFlowApiProvider';
import { validationStages } from '../providers/ValidationPanelProvider';

type ValidationSidebarProps = {
  workspaceId: string;
  applicationName: string;
};

type FlowItem = {
  flow: ValidationFlow;
  stages: ValidationStage[];
  expanded: boolean;
  loading: boolean;
};

/** Right-hand list of recorded checks for the open HTTP application. */
export function ValidationSidebar({ workspaceId, applicationName }: ValidationSidebarProps) {
  const { server } = useWorkspaces();
  const [items, setItems] = useState<FlowItem[]>([]);
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

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
        setItems(flows.map(flow => ({ flow, stages: validationStages(flow), expanded: false, loading: false })));
        setStatus(flows.length === 0 ? 'No checks recorded for this application.' : null);
      })
      .catch(reason => {
        if (!active) return;
        setError(reason instanceof Error ? reason.message : String(reason));
      });
    return () => { active = false; };
  }, [server, workspaceId, applicationName]);

  const expand = useCallback((id: string, next: ValidationFlow | null) => {
    setItems(current => current.map(item => {
      if (item.flow.id !== id) return item;
      const flow = next ?? item.flow;
      return { flow, stages: validationStages(flow), expanded: true, loading: false };
    }));
  }, []);

  const play = useCallback(async (id: string) => {
    if (!server) return;
    setItems(current => current.map(item => item.flow.id === id ? { ...item, loading: true, expanded: true } : item));
    try {
      const fresh = await getValidationFlow(server, workspaceId, id);
      expand(id, fresh);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
      setItems(current => current.map(item => item.flow.id === id ? { ...item, loading: false } : item));
    }
  }, [expand, server, workspaceId]);

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
              <Pressable
                accessibilityRole="button"
                accessibilityLabel={`Show steps for ${item.flow.name}`}
                onPress={() => expand(item.flow.id, item.flow)}
                style={styles.flowCopy}>
                <Text style={styles.flowName}>{item.flow.name}</Text>
                {!!item.flow.description && <Text style={styles.muted}>{item.flow.description}</Text>}
                <Text style={styles.muted}>Starts at {item.flow.initialPath || '/'}</Text>
              </Pressable>
              <Pressable
                accessibilityRole="button"
                accessibilityLabel={`Play ${item.flow.name}`}
                disabled={item.loading}
                onPress={() => { void play(item.flow.id); }}
                style={styles.play}>
                <Text style={styles.playLabel}>▶ Play</Text>
              </Pressable>
            </View>
            {item.expanded && (
              <View style={styles.steps}>
                {item.stages.map(stage => (
                  <View key={stage.id} style={styles.stage}>
                    <Text style={styles.stepTitle}>{stage.title}</Text>
                    <View style={styles.checks}>
                      {stage.checks.map(check => (
                        <Text key={check} style={styles.check}>{check}</Text>
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
  stepTitle: auText('validationStep'),
  checks: auBox('validationChecks'),
  check: auText('validationCheck'),
  error: auText('badgeDanger'),
});
