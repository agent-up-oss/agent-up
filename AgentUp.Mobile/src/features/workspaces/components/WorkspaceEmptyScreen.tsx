import { useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useShellConfig } from '@/features/shell/hooks/useShellConfig';
import { useWorkspaces } from '../controllers/WorkspacesContext';
import { CloneWorkspaceDialog } from './WorkspaceDialogs';
import { PlanCard } from '@/features/entitlements/components/PlanCard';
import { isFeatureAvailable, presentPlanCard, workspaceCreateFeature, workspaceCreateUnavailableMessage, type PlanCard as PlanCardModel } from '@/features/entitlements/models/Entitlements';
import { getEntitlements } from '@/features/entitlements/providers/EntitlementsApiProvider';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';

export function WorkspaceEmptyScreen() {
  const { server, loading, error, refresh, clone } = useWorkspaces();
  const [adding, setAdding] = useState(false);
  const [repository, setRepository] = useState('');
  const [branch, setBranch] = useState('main');
  const [derived, setDerived] = useState(false);
  const [cloning, setCloning] = useState(false);
  const [cloneError, setCloneError] = useState<string | null>(null);
  const [plan, setPlan] = useState<PlanCardModel | null>(null);
  const [canCreate, setCanCreate] = useState<boolean | null>(null);

  useEffect(() => {
    if (!server) {
      setPlan(null);
      setCanCreate(true);
      return;
    }
    let cancelled = false;
    void getEntitlements(server).then(document => {
      if (cancelled) return;
      setPlan(presentPlanCard(document));
      setCanCreate(isFeatureAvailable(document, workspaceCreateFeature));
    });
    return () => { cancelled = true; };
  }, [server]);

  const shellConfig = useMemo(() => ({
    title: 'Workspaces',
    rightAction: canCreate === true ? {
      label: 'Add',
      accessibilityLabel: 'Add workspace',
      onPress: () => { setRepository(''); setBranch('main'); setDerived(false); setCloneError(null); setAdding(true); },
    } : null,
    sidebarContent: null,
  }), [canCreate]);

  useShellConfig(shellConfig);

  const confirmClone = async () => {
    if (cloning) return;
    setCloning(true); setCloneError(null);
    try {
      const revision = branch.trim();
      await clone({
        repository: repository.trim(),
        branch: revision,
        ...(derived ? { derived: true, revision } : {}),
      });
      setAdding(false);
    } catch (cause) {
      setCloneError(cause instanceof Error ? cause.message : 'Could not clone the repository.');
    } finally {
      setCloning(false);
    }
  };

  return (
    <>
      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        <View style={styles.card}>
          <Text accessibilityRole="header" style={styles.title}>No workspaces</Text>
          {plan && <PlanCard card={plan} />}
          <Text style={styles.empty}>{canCreate === false ? workspaceCreateUnavailableMessage : 'Clone a repository with Add to get started.'}</Text>
          <Text style={styles.subtitle}>
            {server ? `Connected to ${server.url}` : 'Connect to a server to manage workspaces.'}
          </Text>
          {loading && <ActivityIndicator color={agentUpTheme.colors.accent} />}
          {!!error && <Text accessibilityRole="alert" style={styles.error}>{error}</Text>}
          <Pressable accessibilityRole="button" accessibilityLabel="Reload workspaces" onPress={() => void refresh()} style={styles.secondaryButton}>
            <Text style={styles.secondaryButtonText}>Reload</Text>
          </Pressable>
        </View>
      </ScrollView>

      <CloneWorkspaceDialog
        visible={adding}
        repository={repository}
        branch={branch}
        derived={derived}
        cloning={cloning}
        error={cloneError}
        onRepository={setRepository}
        onBranch={setBranch}
        onDerived={setDerived}
        onCancel={() => setAdding(false)}
        onConfirm={() => { void confirmClone(); }}
      />
    </>
  );
}

const styles = StyleSheet.create({
  content: {
    flexGrow: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: agentUpTheme.spacing[6],
    paddingBottom: agentUpTheme.spacing[8],
    gap: 14,
  },
  card: { ...auBox('signIn'), width: '100%', maxWidth: 416, gap: agentUpTheme.spacing[3] },
  title: auText('pageTitle'),
  subtitle: auText('muted'),
  empty: auText('muted'),
  error: auText('badgeDanger'),
  label: auText('fieldLabel'),
  input: { ...auBox('input'), ...auText('input') },
  button: { ...auBox('button'), alignItems: 'center', justifyContent: 'center' },
  buttonText: auText('button'),
  secondaryButton: { ...auBox('button', 'buttonSecondary'), alignItems: 'center', justifyContent: 'center', alignSelf: 'flex-start' },
  secondaryButtonText: auText('buttonSecondary'),
  disabled: auBox('buttonDisabled'),
  modalScrim: { flex: 1, padding: agentUpTheme.spacing[5], alignItems: 'center', justifyContent: 'center', ...auBox('scrim') },
  dialog: { ...auBox('card'), width: '100%', maxWidth: 480, gap: 10 },
  dialogTitle: auText('pageTitle'),
  dialogDetail: auText('muted'),
  dialogActions: { flexDirection: 'row', justifyContent: 'flex-end', gap: 10, marginTop: 6 },
});
