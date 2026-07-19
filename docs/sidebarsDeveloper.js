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
        'desktop',
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
        'ci-configuration',
      ],
    },
  ],
};

module.exports = sidebars;
