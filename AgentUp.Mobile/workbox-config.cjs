module.exports = {
  globDirectory: 'dist/',
  globPatterns: ['**/*.{css,html,ico,js,json,png,svg,ttf,woff,woff2}'],
  swDest: 'dist/sw.js',
  cleanupOutdatedCaches: true,
  clientsClaim: true,
  skipWaiting: false,
  navigateFallback: '/index.html',
  maximumFileSizeToCacheInBytes: 5 * 1024 * 1024,
};
