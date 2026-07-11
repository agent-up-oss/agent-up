// @ts-check

const lightCodeTheme = require('prism-react-renderer').themes.github;
const darkCodeTheme = require('prism-react-renderer').themes.dracula;

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'Agent-Up',
  tagline: 'Workspace management for AI-assisted development',
  favicon: 'img/favicon.svg',

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

  themeConfig:
    /** @type {import('@docusaurus/preset-classic').ThemeConfig} */
    ({
      image: 'img/social-card.svg',
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
            label: 'Documentation',
          },
          {
            to: '/',
            label: 'Product',
            position: 'left',
          },
        ],
      },
      footer: {
        style: 'dark',
        links: [
          {
            title: 'Core',
            items: [
              { label: 'Architecture', to: '/docs/architecture' },
              { label: 'Workspace', to: '/docs/workspace' },
              { label: 'Configuration', to: '/docs/configuration' },
            ],
          },
          {
            title: 'Automation',
            items: [
              { label: 'MCP', to: '/docs/mcp' },
              { label: 'Event Recording', to: '/docs/event-recording' },
              { label: 'Playwright', to: '/docs/playwright' },
            ],
          },
        ],
        copyright: `Copyright ${new Date().getFullYear()} Agent-Up.`,
      },
      prism: {
        theme: lightCodeTheme,
        darkTheme: darkCodeTheme,
      },
    }),
};

module.exports = config;
