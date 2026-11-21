/**
 * Detox's own Jest environment, with adb reverse removal made idempotent before Detox loads a
 * driver. The patch has to happen here: the environment and the Android driver share this
 * process's module cache, while a test file only sees Jest's sandboxed copy of Detox.
 */
const ADB = require('detox/src/devices/common/drivers/android/exec/ADB');
const { tolerateMissingReverse } = require('./missingReverse');

tolerateMissingReverse(ADB.prototype);

module.exports = require('detox/runners/jest/testEnvironment');
