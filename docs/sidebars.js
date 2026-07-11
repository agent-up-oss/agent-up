/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
  docsSidebar: [
    'index',
    {
      type: 'category',
      label: 'Getting Started',
      items: [
        'workspace',
        'configuration',
        'agent-up-json',
      ],
    },
    {
      type: 'category',
      label: 'Using Workspaces',
      items: [
        'cli',
        'browser',
        'browser-profiles',
      ],
    },
    'roadmap',
  ],
};

module.exports = sidebars;
