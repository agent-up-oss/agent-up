/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
  docsSidebar: [
    'index',
    {
      type: 'category',
      label: 'Getting Started',
      items: [
        'downloads',
        'setup',
        'workspace',
        'configuration',
        {
          type: 'category',
          label: 'agent-up.json',
          items: [
            'agent-up-json',
            'agent-up-json-reference',
            'agent-up-json-environment',
            'agent-up-json-examples',
          ],
        },
        'releases',
        'limitations',
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
