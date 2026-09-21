import { StyleSheet, Text, View } from 'react-native';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import type { EntitlementCard as EntitlementCardModel } from '../models/Entitlements';

export function EntitlementCard({ card }: { card: EntitlementCardModel }) {
  return (
    <View testID="entitlement-card" style={styles.card}>
      <Text style={styles.label}>Edition</Text>
      <Text style={styles.title}>{card.displayName}</Text>
      {!!card.billing && <Text style={styles.muted}>{card.billing}</Text>}
      <Text style={styles.muted}>{card.summary}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  card: { ...auBox('workspace'), gap: agentUpTheme.spacing[1] },
  label: auText('fieldLabel'),
  title: auText('workspaceName'),
  muted: auText('muted'),
});
