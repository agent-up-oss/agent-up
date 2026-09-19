/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
  developerGuideSidebar: [
    'index',
    {
      type: 'category',
      label: 'Repo',
      collapsed: true,
      items: [
        'repo/architecture',
        'repo/testing',
        'repo/design-principles',
        'repo/design-system',
        'repo/packaging',
        'repo/au-debug',
        'repo/telemetry',
        'repo/ci-configuration',
        'repo/mobile-store-release',
      ],
    },
    {
      type: 'category',
      label: 'Workspaces',
      collapsed: true,
      items: [
        { type: 'doc', id: 'workspaces/index', label: 'General' },
        'workspaces/workflows',
      ],
    },
    {
      type: 'category',
      label: 'Applications',
      collapsed: true,
      items: [
        { type: 'doc', id: 'applications/index', label: 'General' },
        'applications/hosting-assessment',
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
        'commits/merge-queue-assessment',
      ],
    },
    {
      type: 'category',
      label: 'Agents',
      collapsed: true,
      items: [
        { type: 'doc', id: 'agents/index', label: 'General' },
        'agents/sign-in',
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
        'diagnostics/events',
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
      ],
    },
  ],
};

module.exports = sidebars;
