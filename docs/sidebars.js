/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
  docsSidebar: [
    'index',
    {
      type: 'category',
      label: 'Foundations',
      items: [
        'design-principles',
        'architecture',
        'workspace',
        'configuration',
        'agent-up-json',
      ],
    },
    {
      type: 'category',
      label: 'Runtime',
      items: [
        'server',
        'desktop',
        'cli',
        'browser',
        'browser-profiles',
      ],
    },
    {
      type: 'category',
      label: 'Automation',
      items: [
        'mcp',
        'event-recording',
        'playwright',
        'diagnostics',
        'workflows',
      ],
    },
    'roadmap',
  ],
};

module.exports = sidebars;
