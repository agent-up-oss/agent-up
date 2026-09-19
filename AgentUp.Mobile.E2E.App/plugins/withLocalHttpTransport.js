const { withAndroidManifest, withInfoPlist } = require('expo/config-plugins');

/**
 * The native E2E harness talks to Server and identity-provider processes on the CI host over
 * ephemeral HTTP ports. Production clients retain their normal transport policy; this exception
 * belongs only to the disposable harness app.
 */
function withLocalHttpTransport(config) {
  config = withAndroidManifest(config, androidConfig => {
    allowAndroidCleartext(androidConfig.modResults);
    return androidConfig;
  });

  return withInfoPlist(config, iosConfig => {
    allowIosLocalHttp(iosConfig.modResults);
    return iosConfig;
  });
}

function allowAndroidCleartext(manifest) {
  manifest.manifest.application[0].$['android:usesCleartextTraffic'] = 'true';
}

function allowIosLocalHttp(infoPlist) {
  infoPlist.NSAppTransportSecurity = {
    ...(infoPlist.NSAppTransportSecurity ?? {}),
    NSAllowsArbitraryLoads: true,
    NSAllowsLocalNetworking: true,
  };
}

module.exports = withLocalHttpTransport;
module.exports.allowAndroidCleartext = allowAndroidCleartext;
module.exports.allowIosLocalHttp = allowIosLocalHttp;
