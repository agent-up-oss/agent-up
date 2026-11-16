import React from 'react';
import { Image } from 'react-native';
import { agentUpTheme } from '@agent-up/design-system/native';

type SceneIconProps = {
  paths: readonly string[];
  color?: string;
  size?: number;
};

/** A design-system icon from its 24-unit path data, stroked in the surrounding text colour. */
export function SceneIcon({ paths, color = agentUpTheme.colors.textPrimary, size = 20 }: SceneIconProps) {
  const drawn = paths.map(d => `<path d="${d}"/>`).join('');
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="${color}" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round">${drawn}</svg>`;
  return (
    <Image
      accessibilityElementsHidden
      importantForAccessibility="no-hide-descendants"
      source={{ uri: `data:image/svg+xml;base64,${btoa(svg)}` }}
      style={{ width: size, height: size }}
    />
  );
}
