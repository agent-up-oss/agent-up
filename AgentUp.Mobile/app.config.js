'use strict';

const appJson = require('./app.json');
const { createMobileExpoConfig } = require('./scripts/mobile-expo-config.js');

module.exports = createMobileExpoConfig(appJson, process.env);
