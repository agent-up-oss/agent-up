'use strict';

const { agentUpTheme } = require('../../AgentUp.DesignSystem/dist/web/tokens.cjs');

const iosBundleId = 'net.themassiveone.agent-up.ios';
const androidPackageName = 'net.themassiveone.agentup.android';
const canvasColor = agentUpTheme.colors.canvas;
const versionPattern = /^\d+\.\d+\.\d+$/;
const versionCodePattern = /^[1-9][0-9]*$/;

function pluginName(plugin) {
  return Array.isArray(plugin) ? plugin[0] : plugin;
}

function splashScreenPlugin(backgroundColor) {
  return [
    'expo-splash-screen',
    {
      image: './assets/icon.png',
      resizeMode: 'contain',
      backgroundColor,
    },
  ];
}

function withSplashScreenPlugin(plugins, backgroundColor) {
  const existing = Array.isArray(plugins) ? plugins : [];
  return [
    ...existing.filter((plugin) => pluginName(plugin) !== 'expo-splash-screen'),
    splashScreenPlugin(backgroundColor),
  ];
}

function createMobileExpoConfig(appJson, env = {}) {
  const expo = appJson.expo || {};
  const version = String(env.AGENTUP_MOBILE_VERSION || expo.version || '').trim();
  if (!versionPattern.test(version)) {
    throw new Error(`Mobile Expo version is not X.Y.Z: ${version}`);
  }

  const versionCodeRaw = String(env.AGENTUP_MOBILE_VERSION_CODE || '1').trim();
  if (!versionCodePattern.test(versionCodeRaw)) {
    throw new Error(`Mobile version code is not a positive integer: ${versionCodeRaw}`);
  }

  return {
    ...appJson,
    expo: {
      ...expo,
      version,
      icon: './assets/icon.png',
      plugins: withSplashScreenPlugin(expo.plugins, canvasColor),
      ios: {
        ...expo.ios,
        bundleIdentifier: iosBundleId,
        buildNumber: versionCodeRaw,
        icon: './assets/icon.png',
        infoPlist: {
          ...(expo.ios && expo.ios.infoPlist),
          ITSAppUsesNonExemptEncryption: false,
        },
      },
      android: {
        ...expo.android,
        package: androidPackageName,
        versionCode: Number(versionCodeRaw),
        adaptiveIcon: {
          foregroundImage: './assets/adaptive-icon.png',
          backgroundColor: canvasColor,
        },
      },
    },
  };
}

module.exports = {
  canvasColor,
  createMobileExpoConfig,
  iosBundleId,
  androidPackageName,
  pluginName,
};
