// @ts-check

const { agentUpTheme } = require('@agent-up/design-system/tokens');
const voice = require('@agent-up/design-system/brand/voice.json');

const agentUpCodeTheme = {
  plain: {
    color: agentUpTheme.colors.textSecondary,
    backgroundColor: agentUpTheme.colors.surface,
  },
  styles: [
    {
      types: ['comment', 'prolog', 'doctype', 'cdata'],
      style: {
        color: agentUpTheme.colors.textMuted,
        fontStyle: 'italic',
      },
    },
    {
      types: ['punctuation'],
      style: {
        color: agentUpTheme.colors.textMuted,
      },
    },
    {
      types: ['property', 'tag', 'constant', 'symbol', 'deleted'],
      style: {
        color: agentUpTheme.colors.accentSoft,
      },
    },
    {
      types: ['boolean', 'number'],
      style: {
        color: agentUpTheme.colors.accentSoft,
      },
    },
    {
      types: ['selector', 'attr-name', 'string', 'char', 'builtin', 'inserted'],
      style: {
        color: agentUpTheme.colors.accent,
      },
    },
    {
      types: ['operator', 'entity', 'url', 'variable'],
      style: {
        color: agentUpTheme.colors.textSecondary,
      },
    },
    {
      types: ['atrule', 'attr-value', 'function', 'class-name'],
      style: {
        color: agentUpTheme.colors.textPrimary,
      },
    },
    {
      types: ['keyword'],
      style: {
        color: agentUpTheme.colors.accentSoft,
        fontWeight: '700',
      },
    },
    {
      types: ['regex', 'important'],
      style: {
        color: agentUpTheme.colors.textSecondary,
      },
    },
  ],
};

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'Agent-Up',
  tagline: voice.category,
  favicon: 'img/favicon.ico',

  url: 'https://agent-up.local',
  baseUrl: '/',
  organizationName: 'agent-up',
  projectName: 'agent-up',

  onBrokenLinks: 'throw',
  markdown: {
    hooks: {
      onBrokenMarkdownLinks: 'warn',
    },
  },

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      /** @type {import('@docusaurus/preset-classic').Options} */
      ({
        docs: {
          routeBasePath: '/docs',
          path: 'user-docs',
          sidebarPath: require.resolve('./sidebars.js'),
          editUrl: undefined,
        },
        blog: false,
        theme: {
          customCss: require.resolve('./src/css/custom.css'),
        },
      }),
    ],
  ],

  plugins: [
    [
      '@docusaurus/plugin-content-docs',
      {
        id: 'developerGuide',
        path: 'developer-guide',
        routeBasePath: '/developer-guide',
        sidebarPath: require.resolve('./sidebarsDeveloper.js'),
        editUrl: undefined,
      },
    ],
    [
      '@docusaurus/plugin-client-redirects',
      {
        redirects: [
          { from: '/docs/downloads', to: '/docs/start/downloads' },
          { from: '/docs/setup', to: '/docs/start/setup' },
          { from: '/docs/releases', to: '/docs/start/releases' },
          { from: '/docs/limitations', to: '/docs/start/limitations' },
          { from: '/docs/roadmap', to: '/docs/start/roadmap' },
          { from: '/docs/workspace', to: '/docs/workspaces' },
          { from: '/docs/cli', to: '/docs/workspaces' },
          { from: '/docs/mobile', to: '/docs/workspaces' },
          { from: '/docs/git-changes', to: '/docs/git' },
          { from: '/docs/browser-profiles', to: '/docs/browser' },
          { from: '/docs/agent-up-json', to: '/docs/configuration' },
          { from: '/docs/agent-up-json-reference', to: '/docs/configuration/reference' },
          { from: '/docs/agent-up-json-environment', to: '/docs/configuration/environment' },
          { from: '/docs/agent-up-json-examples', to: '/docs/configuration/examples' },
          { from: '/developer-guide/architecture', to: '/developer-guide/repo/architecture' },
          { from: '/developer-guide/design-principles', to: '/developer-guide/repo/design-principles' },
          { from: '/developer-guide/design-system', to: '/developer-guide/repo/design-system' },
          { from: '/developer-guide/packaging', to: '/developer-guide/repo/packaging' },
          { from: '/developer-guide/au-debug', to: '/developer-guide/repo/au-debug' },
          { from: '/developer-guide/telemetry', to: '/developer-guide/repo/telemetry' },
          { from: '/developer-guide/ci-configuration', to: '/developer-guide/repo/ci-configuration' },
          { from: '/developer-guide/server', to: '/developer-guide/workspaces' },
          { from: '/developer-guide/desktop', to: '/developer-guide/workspaces' },
          { from: '/developer-guide/mobile', to: '/developer-guide/workspaces' },
          { from: '/developer-guide/mcp', to: '/developer-guide/' },
          { from: '/developer-guide/agent-sign-in', to: '/developer-guide/agents/sign-in' },
          { from: '/developer-guide/event-recording', to: '/developer-guide/diagnostics/events' },
          { from: '/developer-guide/playwright', to: '/developer-guide/browser/validation' },
          { from: '/developer-guide/workflows', to: '/developer-guide/workspaces/workflows' },
          { from: '/developer-guide/desktop-app-hosting-assessment', to: '/developer-guide/applications/hosting-assessment' },
          { from: '/developer-guide/commit-merge-queue-assessment', to: '/developer-guide/commits/merge-queue-assessment' },
        ],
      },
    ],
  ],

  clientModules: [require.resolve('./src/forceDarkMode.js')],

  themeConfig:
    /** @type {import('@docusaurus/preset-classic').ThemeConfig} */
    ({
      colorMode: {
        defaultMode: 'dark',
        disableSwitch: true,
        respectPrefersColorScheme: false,
      },
      image: 'img/social-card.png',
      navbar: {
        title: 'Agent-Up',
        logo: {
          alt: 'Agent-Up logo',
          src: 'img/logo.svg',
        },
        items: [
          {
            type: 'docSidebar',
            sidebarId: 'docsSidebar',
            position: 'left',
            label: 'Docs',
          },
          {
            type: 'docSidebar',
            docsPluginId: 'developerGuide',
            sidebarId: 'developerGuideSidebar',
            position: 'left',
            label: 'Developer Guide',
          },
          {
            to: '/design-system',
            label: 'Design System',
            position: 'left',
          },
          {
            href: 'https://github.com/themassiveone/agent-up',
            label: 'GitHub',
            position: 'right',
          },
        ],
      },
      footer: {
        style: 'dark',
        links: [
          {
            items: [
              {
                html: `
                  <p class="footer-brand__name">Agent-Up</p>
                  <p class="footer-brand__tagline">
                    Local runtime control for AI-assisted development.
                    Isolated workspaces, independent browser sessions, and Server-owned state.
                  </p>
                `,
              },
            ],
          },
          {
            title: 'Docs',
            items: [
              { label: 'Overview', to: '/docs/' },
              { label: 'Start', to: '/docs/start/setup' },
              { label: 'Workspaces', to: '/docs/workspaces' },
              { label: 'Applications', to: '/docs/applications' },
              { label: 'Git', to: '/docs/git' },
              { label: 'Commits', to: '/docs/commits' },
              { label: 'Agents', to: '/docs/agents' },
              { label: 'Browser', to: '/docs/browser' },
              { label: 'Diagnostics', to: '/docs/diagnostics' },
              { label: 'Verification', to: '/docs/verification' },
              { label: 'Configuration', to: '/docs/configuration' },
            ],
          },
          {
            title: 'Developer Guide',
            items: [
              { label: 'Overview', to: '/developer-guide/' },
              { label: 'Workspaces', to: '/developer-guide/workspaces' },
              { label: 'Applications', to: '/developer-guide/applications' },
              { label: 'Git', to: '/developer-guide/git' },
              { label: 'Commits', to: '/developer-guide/commits' },
              { label: 'Agents', to: '/developer-guide/agents' },
              { label: 'Browser', to: '/developer-guide/browser' },
              { label: 'Diagnostics', to: '/developer-guide/diagnostics' },
              { label: 'Verification', to: '/developer-guide/verification' },
              { label: 'Configuration', to: '/developer-guide/configuration' },
            ],
          },
          {
            title: 'Project',
            items: [
              { label: 'Design System', to: '/design-system' },
              { label: 'GitHub', href: 'https://github.com/themassiveone/agent-up' },
              { label: 'Contributing', href: 'https://github.com/themassiveone/agent-up/blob/main/CONTRIBUTING.md' },
              { label: 'Security', href: 'https://github.com/themassiveone/agent-up/blob/main/SECURITY.md' },
              { label: 'License', href: 'https://github.com/themassiveone/agent-up/blob/main/LICENSE' },
            ],
          },
        ],
        copyright: `Copyright ${new Date().getFullYear()} Agent-Up. Licensed under Apache-2.0.`,
      },
      prism: {
        theme: agentUpCodeTheme,
        darkTheme: agentUpCodeTheme,
      },
    }),
};

module.exports = config;
