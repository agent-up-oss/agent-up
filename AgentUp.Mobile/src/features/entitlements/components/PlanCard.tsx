import { StyleSheet, Text, View } from 'react-native';
import { agentUpTheme, auBox, auText } from '@agent-up/design-system/native';
import type { PlanCard as PlanCardModel } from '../models/Entitlements';

export function PlanCard({ card }: { card: PlanCardModel }) {
  return (
    <View testID="plan-card" style={styles.card}>
      <Text style={styles.label}>Plan</Text>
      <Text style={styles.title}>{card.displayName}</Text>
      {!!card.billing && <Text style={styles.muted}>{card.billing}</Text>}
      <Text style={styles.muted}>{card.summary}</Text>
      {card.features.map(feature => (
        <Text key={feature.id} style={styles.muted}>
          {feature.id}: {feature.available ? 'available' : 'unavailable'}
        </Text>
      ))}
      {card.limits.map(limit => (
        <Text key={limit.id} style={styles.muted}>
          {limit.id}: {formatLimit(limit.max, limit.used)}
        </Text>
      ))}
    </View>
  );
}

function formatLimit(max?: number | null, used?: number | null): string {
  if (max == null && used == null) return 'none';
  if (max == null) return `${used} used`;
  if (used == null) return `max ${max}`;
  return `${used} of ${max}`;
}

const styles = StyleSheet.create({
  card: { ...auBox('workspace'), gap: agentUpTheme.spacing[1] },
  label: auText('fieldLabel'),
  title: auText('workspaceName'),
  muted: auText('muted'),
});
