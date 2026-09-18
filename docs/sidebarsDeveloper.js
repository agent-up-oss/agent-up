/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
  developerGuideSidebar: [
    'index',
    {
      type: 'category',
      label: 'System Design',
      items: [
        'design-principles',
        'architecture',
        'server',
        'agent-sign-in',
        'desktop',
        'mobile',
        'design-system',
        'desktop-app-hosting-assessment',
        'commit-merge-queue-assessment',
        'au-debug',
        'packaging',
        'telemetry',
      ],
    },
    {
      type: 'category',
      label: 'Automation Runtime',
      items: [
        'mcp',
        'verification',
        'event-recording',
        'playwright',
        'diagnostics',
        'workflows',
      ],
    },
    {
      type: 'category',
      label: 'Operations',
      items: [
        'ci-configuration',
      ],
    },
  ],
};

module.exports = sidebars;
