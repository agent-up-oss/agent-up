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
config.watchFolders = [
  ...(config.watchFolders ?? []),
  path.resolve(__dirname, '../AgentUp.FakeServer'),
];

config.resolver.resolveRequest = (context, moduleName, platform) => {
  const resolve = inherited ?? context.resolveRequest;
  const fromApp =
    SINGLETONS.has(moduleName) ||
    moduleName.startsWith('react-native/') ||
    // The workspace modules too: this app declares every one of them, and resolving them from here
    // keeps one copy of each in the bundle however deep the import chain goes.
    moduleName.startsWith('@agent-up/');
  return fromApp
    ? resolve({ ...context, originModulePath: appEntry }, moduleName, platform)
    : resolve(context, moduleName, platform);
};

module.exports = config;
