const { withAppBuildGradle } = require('expo/config-plugins');

const DYNAMIC = "androidTestImplementation('com.wix:detox:+')";

/**
 * Asks for the Detox this project installed, by version, instead of "whatever is newest".
 *
 * `@config-plugins/detox` declares the native dependency as `com.wix:detox:+`, and the repository
 * that is meant to answer it is the maven directory inside the npm package - resolved at build
 * time by running `require.resolve('detox/package.json')` from the android project. That works
 * only while Detox is installed *here*, in the app under test, and when it is not there is no
 * error: `com.wix:detox` also exists on Maven Central, as an abandoned stub whose newest version
 * is 0.1.1 and which contains no Detox at all. Gradle resolves the stub, says nothing, and the
 * build dies much later and much less helpfully:
 *
 *   error: cannot find symbol
 *   import com.wix.detox.Detox;
 *
 * Naming the version closes that door. 0.1.1 can no longer stand in for 20.x, so a missing or
 * misresolved local repository fails at resolution, saying which version could not be found,
 * instead of compiling against an empty package.
 *
 * This is listed *before* @config-plugins/detox in app.json, which is what makes it run *after*
 * it: Expo applies each mod by wrapping the one registered before it, so app/build.gradle is
 * rewritten in the reverse of the order the plugins are listed in. Listed the other way round
 * this plugin sees a build.gradle Detox has not touched yet, and the check below says so.
 */
module.exports = function withExactDetoxVersion(config) {
  return withAppBuildGradle(config, mod => {
    if (mod.modResults.language !== 'groovy') {
      throw new Error(`Expected app/build.gradle to be groovy, found ${mod.modResults.language}.`);
    }

    const { version } = require('detox/package.json');
    const exact = `androidTestImplementation('com.wix:detox:${version}')`;
    if (mod.modResults.contents.includes(exact)) return mod;

    if (!mod.modResults.contents.includes(DYNAMIC)) {
      throw new Error(
        `Expected @config-plugins/detox to have added ${DYNAMIC} to app/build.gradle, and it did ` +
          'not. Pinning is what stops the abandoned com.wix:detox on Maven Central being resolved ' +
          'in place of the one Detox ships, so a silent fallback to the dynamic version is worse ' +
          'than this failure. Check what the plugin generates for this version of Detox.',
      );
    }

    mod.modResults.contents = mod.modResults.contents.replace(DYNAMIC, exact);
    return mod;
  });
};
