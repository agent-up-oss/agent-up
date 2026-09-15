const { getDefaultConfig } = require('expo/metro-config');
const path = require('node:path');

/**
 * The workspace modules are file: dependencies, so Metro resolves them through their real paths
 * and then looks for their imports in their own node_modules first. For anything that must exist
 * exactly once in a bundle that is fatal rather than wasteful: two copies of react means the
 * module's hooks read a different dispatcher than the one the app rendered with, and every hook in
 * it throws "Cannot read properties of null (reading 'useContext')" the moment it mounts.
 *
 * So these specifiers always resolve from this app, whoever imported them.
 */
const SINGLETONS = new Set([
  'react',
  'react-dom',
  'react-native',
  'react-native-web',
  'react-native-safe-area-context',
  'react-native-webview',
  'expo-clipboard',
  'expo-linking',
]);

const config = getDefaultConfig(__dirname);
const appEntry = path.join(__dirname, 'package.json');
const inherited = config.resolver.resolveRequest;

config.resolver.resolveRequest = (context, moduleName, platform) => {
  const resolve = inherited ?? context.resolveRequest;
  if (!SINGLETONS.has(moduleName) && !moduleName.startsWith('react-native/'))
    return resolve(context, moduleName, platform);
  return resolve({ ...context, originModulePath: appEntry }, moduleName, platform);
};

module.exports = config;
