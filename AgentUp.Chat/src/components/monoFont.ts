import { Platform } from 'react-native';

/**
 * The design system's mono stack cannot cross into React Native, which takes one family name,
 * so code and tool output name the platform's own monospace face.
 */
export const monoFont = Platform.select({ ios: 'Menlo', default: 'monospace' });
