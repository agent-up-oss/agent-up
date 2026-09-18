/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
  docsSidebar: [
    'index',
    {
      type: 'category',
      label: 'Start',
      collapsed: true,
      items: [
        'start/downloads',
        'start/setup',
        'start/releases',
        'start/limitations',
        'start/roadmap',
      ],
    },
    {
      type: 'category',
      label: 'Workspaces',
      collapsed: true,
      items: [
        { type: 'doc', id: 'workspaces/index', label: 'General' },
      ],
    },
    {
      type: 'category',
      label: 'Applications',
      collapsed: true,
      items: [
        { type: 'doc', id: 'applications/index', label: 'General' },
      ],
    },
    {
      type: 'category',
      label: 'Git',
      collapsed: true,
      items: [
        { type: 'doc', id: 'git/index', label: 'General' },
        'git/review',
        'git/history',
        'git/remotes',
      ],
    },
    {
      type: 'category',
      label: 'Commits',
      collapsed: true,
      items: [
        { type: 'doc', id: 'commits/index', label: 'General' },
      ],
    },
    {
      type: 'category',
      label: 'Agents',
      collapsed: true,
      items: [
        { type: 'doc', id: 'agents/index', label: 'General' },
      ],
    },
    {
      type: 'category',
      label: 'Browser',
      collapsed: true,
      items: [
        { type: 'doc', id: 'browser/index', label: 'General' },
        'browser/validation',
      ],
    },
    {
      type: 'category',
      label: 'Diagnostics',
      collapsed: true,
      items: [
        { type: 'doc', id: 'diagnostics/index', label: 'General' },
      ],
    },
    {
      type: 'category',
      label: 'Verification',
      collapsed: true,
      items: [
        { type: 'doc', id: 'verification/index', label: 'General' },
      ],
    },
    {
      type: 'category',
      label: 'Configuration',
      collapsed: true,
      items: [
        { type: 'doc', id: 'configuration/index', label: 'General' },
        'configuration/reference',
        'configuration/environment',
        'configuration/examples',
      ],
    },
  ],
};

module.exports = sidebars;
