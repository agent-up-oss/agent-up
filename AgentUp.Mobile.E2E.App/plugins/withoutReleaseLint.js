const { withAppBuildGradle } = require('expo/config-plugins');

/**
 * Turns off lint's release checks for this harness app.
 *
 * `assembleRelease` runs lintVital, which tries to read every proguard file the variant declares,
 * and on a hosted runner that walks into a path baked into the machine image at build time:
 *
 *   Execution failed for task ':app:generateReleaseLintVitalReportModel'.
 *   > Cannot access input property 'variantInputs.proguardFiles'
 *      > java.nio.file.AccessDeniedException: /home/packer
 *
 * /home/packer is the image builder's home directory, not ours and not readable by the runner, so
 * the task can never succeed there. This app exists only to be driven by the sign-in suites and
 * is never shipped, so lint has nothing to protect here - and the real client, which is shipped,
 * keeps its own lint untouched.
 */
module.exports = function withoutReleaseLint(config) {
  return withAppBuildGradle(config, mod => {
    if (mod.modResults.language !== 'groovy') {
      throw new Error(`Expected app/build.gradle to be groovy, found ${mod.modResults.language}.`);
    }

    if (mod.modResults.contents.includes('checkReleaseBuilds false')) return mod;

    mod.modResults.contents = mod.modResults.contents.replace(
      /^android\s*\{/m,
      match => `${match}\n    lint {\n        checkReleaseBuilds false\n    }\n`,
    );
    return mod;
  });
};
