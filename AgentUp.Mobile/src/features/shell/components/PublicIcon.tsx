import { Image, StyleSheet } from 'react-native';
import { publicIconUri, type PublicIconName } from '../providers/PublicIconProvider';

type PublicIconProps = {
  name: PublicIconName;
  muted?: boolean;
  size?: number;
};

/** Same-origin SVG so hosted PWAs do not depend on an icon font. */
export function PublicIcon({ name, muted = false, size = 20 }: PublicIconProps) {
  return (
    <Image
      accessibilityElementsHidden
      importantForAccessibility="no-hide-descendants"
      source={{ uri: publicIconUri(name) }}
      style={[styles.icon, { width: size, height: size, opacity: muted ? 0.55 : 1 }]}
    />
  );
}

const styles = StyleSheet.create({
  icon: { flex: 0 },
});
