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
        'desktop-app-hosting-assessment',
        'au-debug',
        'mobile',
        'packaging',
      ],
    },
    {
      type: 'category',
      label: 'Automation Runtime',
      items: [
        'mcp',
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
        'testing',
        'ci-configuration',
        'mobile-store-release',
      ],
    },
  ],
};

module.exports = sidebars;
