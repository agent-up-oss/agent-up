// @ts-check

const lightCodeTheme = require('prism-react-renderer').themes.github;
const darkCodeTheme = require('prism-react-renderer').themes.dracula;

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'Agent-Up',
  tagline: 'Workspace management for AI-assisted development',
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
            label: 'Docs',
          },
          {
            type: 'docSidebar',
            docsPluginId: 'developerGuide',
            sidebarId: 'developerGuideSidebar',
            position: 'left',
            label: 'Developer Guide',
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
                    Workspace management for AI-assisted development.
                    Isolated agents, shared browser context, and server-owned runtime state.
                  </p>
                `,
              },
            ],
          },
          {
            title: 'Docs',
            items: [
              { label: 'Overview', to: '/docs/' },
              { label: 'Workspace', to: '/docs/workspace' },
              { label: 'Configuration', to: '/docs/configuration' },
              { label: 'Browser Profiles', to: '/docs/browser-profiles' },
            ],
          },
          {
            title: 'Developer Guide',
            items: [
              { label: 'Architecture', to: '/developer-guide/architecture' },
              { label: 'Server', to: '/developer-guide/server' },
              { label: 'MCP', to: '/developer-guide/mcp' },
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
