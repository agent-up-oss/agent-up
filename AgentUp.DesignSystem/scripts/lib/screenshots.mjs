export const desktopSize = { width: 1440, height: 900 };
export const mobileSize = { width: 390, height: 844 };

const desktopSources = [
  'AgentUp.Desktop/Features/Workspaces/Views/MainWindow.axaml',
];
const mobileShellSources = [
  'AgentUp.Mobile/src/features/servers/components/ServerSetupScreen.tsx',
  'AgentUp.Mobile/src/features/shell/components/AppShellLayout.tsx',
  'AgentUp.Mobile/src/features/shell/components/AppNavBar.tsx',
  'AgentUp.Mobile/src/features/shell/components/WorkspaceTabBar.tsx',
];
const gitSources = [
  'AgentUp.Desktop/Features/Workspaces/Views/MainWindow.axaml',
  'AgentUp.Mobile/src/features/git/components/GitChangesPanel.tsx',
  'AgentUp.Mobile/src/features/git/components/GitHistoryPanel.tsx',
  'AgentUp.Mobile/src/features/git/components/GitFileViewer.tsx',
];

const demoCopy = [
  'Harbor Shop',
  'Demo',
  'Storefront',
  'Orders API',
  'ProductGrid.tsx',
  'feat(storefront): featured product grid',
];

export function screenshotScenes() {
  return [
    scene({
      id: 'desktop-sign-in',
      surface: 'desktop',
      view: 'sign-in',
      title: 'Desktop sign-in',
      chrome: 'desktop-signin',
      hero: false,
      appSources: desktopSources,
      requiredClasses: ['au-sign-in', 'au-choice', 'au-button', 'au-input', 'au-chrome'],
      requiredDesktopClasses: [],
      requiredMobileComponents: [],
      copy: ['Demo', 'http://127.0.0.1:9'],
      html: desktopSignIn(),
    }),
    scene({
      id: 'desktop-workspaces',
      surface: 'desktop',
      view: 'workspaces',
      title: 'Desktop workspaces',
      chrome: 'desktop-shell',
      hero: false,
      appSources: desktopSources,
      requiredClasses: ['au-workspace', 'au-rail', 'au-chrome', 'au-metrics-card'],
      requiredDesktopClasses: ['wsEntry'],
      requiredMobileComponents: [],
      copy: ['Harbor Shop', 'main'],
      html: desktopWorkspaces(),
    }),
    scene({
      id: 'desktop-applications',
      surface: 'desktop',
      view: 'applications',
      title: 'Desktop applications',
      chrome: 'desktop-shell',
      hero: true,
      appSources: desktopSources,
      requiredClasses: ['au-app-tab', 'au-subtab', 'au-address-bar', 'au-workspace', 'au-chrome'],
      requiredDesktopClasses: ['appTab', 'addressBar'],
      requiredMobileComponents: [],
      copy: ['Harbor Shop', 'Storefront', 'Orders API'],
      html: desktopApplications(),
    }),
    scene({
      id: 'desktop-console',
      surface: 'desktop',
      view: 'console',
      title: 'Desktop console',
      chrome: 'desktop-shell',
      hero: false,
      appSources: desktopSources,
      requiredClasses: ['au-console', 'au-app-tab', 'au-subtab'],
      requiredDesktopClasses: ['subTab'],
      requiredMobileComponents: [],
      copy: ['Storefront', '[install] npm install'],
      html: desktopConsole(),
    }),
    scene({
      id: 'desktop-git',
      surface: 'desktop',
      view: 'git',
      title: 'Desktop Git changes',
      chrome: 'desktop-shell',
      hero: false,
      appSources: gitSources,
      requiredClasses: ['au-git-change-list', 'au-git-row', 'au-git-status', 'au-checkbox'],
      requiredDesktopClasses: ['gitNodeRow'],
      requiredMobileComponents: ['gitChangeList'],
      copy: ['ProductGrid.tsx', 'orders.ts'],
      html: desktopGit(),
    }),
    scene({
      id: 'desktop-history',
      surface: 'desktop',
      view: 'history',
      title: 'Desktop Git history',
      chrome: 'desktop-shell',
      hero: false,
      appSources: gitSources,
      requiredClasses: ['au-git-log', 'au-git-log-row', 'au-git-log-detail'],
      requiredDesktopClasses: ['gitLogRow'],
      requiredMobileComponents: ['gitLog'],
      copy: ['feat(storefront): featured product grid'],
      html: desktopHistory(),
    }),
    scene({
      id: 'desktop-agents',
      surface: 'desktop',
      view: 'agents',
      title: 'Desktop Agent',
      chrome: 'desktop-shell',
      hero: false,
      appSources: [
        ...desktopSources,
        'AgentUp.Chat/src/components/AgentChatScreen.tsx',
      ],
      requiredClasses: ['au-chat-user', 'au-chat-work', 'au-card', 'au-choice'],
      requiredDesktopClasses: [],
      requiredMobileComponents: ['chatUser', 'chatWork'],
      copy: ['Codex', 'Harbor Shop'],
      html: desktopAgents(),
    }),
    scene({
      id: 'desktop-diagnostics',
      surface: 'desktop',
      view: 'diagnostics',
      title: 'Desktop diagnostics',
      chrome: 'desktop-shell',
      hero: false,
      appSources: desktopSources,
      requiredClasses: ['au-audit-row', 'au-audit-header', 'au-page-jump'],
      requiredDesktopClasses: ['auditTableRow'],
      requiredMobileComponents: [],
      copy: ['Storefront'],
      html: desktopDiagnostics(),
    }),
    scene({
      id: 'desktop-metrics',
      surface: 'desktop',
      view: 'metrics',
      title: 'Desktop metrics',
      chrome: 'desktop-shell',
      hero: false,
      appSources: desktopSources,
      requiredClasses: ['au-metrics-card', 'au-metrics-chart'],
      requiredDesktopClasses: ['metricsSummaryCard'],
      requiredMobileComponents: [],
      copy: ['Harbor Shop'],
      html: desktopMetrics(),
    }),
    scene({
      id: 'desktop-validation',
      surface: 'desktop',
      view: 'validation',
      title: 'Desktop validation',
      chrome: 'desktop-shell',
      hero: false,
      appSources: desktopSources,
      requiredClasses: ['au-validation-stage'],
      requiredDesktopClasses: [],
      requiredMobileComponents: [],
      copy: ['Harbor Shop'],
      html: desktopValidation(),
    }),
    scene({
      id: 'desktop-database',
      surface: 'desktop',
      view: 'database',
      title: 'Desktop database',
      chrome: 'desktop-shell',
      hero: false,
      appSources: desktopSources,
      requiredClasses: ['au-code-editor', 'au-db-run', 'au-db-header-cell', 'au-list-item'],
      requiredDesktopClasses: ['sqlEditor', 'dbRunButton'],
      requiredMobileComponents: [],
      copy: ['Harbor Shop'],
      html: desktopDatabase(),
    }),
    scene({
      id: 'desktop-capabilities',
      surface: 'desktop',
      view: 'capabilities',
      title: 'Desktop capabilities',
      chrome: 'desktop-shell',
      hero: false,
      appSources: desktopSources,
      requiredClasses: ['au-overlay-panel', 'au-card', 'au-badge'],
      requiredDesktopClasses: [],
      requiredMobileComponents: [],
      copy: ['.NET', 'Docker', 'Codex'],
      html: desktopCapabilities(),
    }),
    scene({
      id: 'desktop-file-viewer',
      surface: 'desktop',
      view: 'file-viewer',
      title: 'Desktop file viewer',
      chrome: 'desktop-shell',
      hero: false,
      appSources: gitSources,
      requiredClasses: ['au-file-viewer', 'au-file-viewer-line', 'au-overlay-panel'],
      requiredDesktopClasses: [],
      requiredMobileComponents: ['fileViewer'],
      copy: ['ProductGrid.tsx'],
      html: desktopFileViewer(),
    }),
    scene({
      id: 'mobile-sign-in',
      surface: 'mobile',
      view: 'sign-in',
      title: 'Mobile sign-in',
      chrome: 'mobile-signin',
      hero: false,
      livePath: '/connect',
      appSources: mobileShellSources,
      requiredClasses: ['au-sign-in', 'au-choice', 'au-button', 'au-mobile-bar'],
      requiredDesktopClasses: [],
      requiredMobileComponents: ['signIn', 'choice'],
      copy: ['Demo'],
      html: mobileSignIn(),
    }),
    scene({
      id: 'mobile-workspaces',
      surface: 'mobile',
      view: 'workspaces',
      title: 'Mobile workspaces',
      chrome: 'mobile-list',
      hero: false,
      livePath: '/',
      appSources: [
        ...mobileShellSources,
        'AgentUp.Mobile/src/features/workspaces/components/WorkspaceListScreen.tsx',
      ],
      requiredClasses: ['au-workspace', 'au-mobile-bar', 'au-button'],
      requiredDesktopClasses: [],
      requiredMobileComponents: ['workspace', 'mobileBar'],
      copy: ['Harbor Shop'],
      html: mobileWorkspaces(),
    }),
    scene({
      id: 'mobile-apps',
      surface: 'mobile',
      view: 'apps',
      title: 'Mobile apps',
      chrome: 'mobile-shell',
      tab: 'apps',
      hero: false,
      livePath: '/workspace/harbor-shop',
      appSources: [
        ...mobileShellSources,
        'AgentUp.Mobile/src/features/workspaces/components/WorkspaceDashboardScreen.tsx',
      ],
      requiredClasses: ['au-mobile-tab-bar', 'au-workspace', 'au-status-dot'],
      requiredDesktopClasses: [],
      requiredMobileComponents: ['workspace', 'mobileTabBar', 'navTab'],
      copy: ['Storefront', 'Orders API'],
      html: mobileApps(),
    }),
    scene({
      id: 'mobile-git',
      surface: 'mobile',
      view: 'git',
      title: 'Mobile Git',
      chrome: 'mobile-shell',
      tab: 'git',
      hero: false,
      livePath: '/workspace/harbor-shop/git',
      appSources: [
        ...gitSources,
        'AgentUp.Mobile/src/features/shell/components/WorkspaceTabBar.tsx',
      ],
      requiredClasses: ['au-git-change-list', 'au-git-row', 'au-mobile-tab-bar'],
      requiredDesktopClasses: [],
      requiredMobileComponents: ['gitChangeList', 'mobileTabBar'],
      copy: ['ProductGrid.tsx'],
      html: mobileGit(),
    }),
    scene({
      id: 'mobile-review',
      surface: 'mobile',
      view: 'review',
      title: 'Mobile Git review',
      chrome: 'mobile-shell',
      tab: 'git',
      hero: false,
      livePath: '/workspace/harbor-shop/git/review',
      appSources: gitSources,
      requiredClasses: ['au-git-row', 'au-button', 'au-mobile-bar'],
      requiredDesktopClasses: [],
      requiredMobileComponents: ['gitChangeList'],
      copy: ['ProductGrid.tsx', 'orders.ts'],
      html: mobileReview(),
    }),
    scene({
      id: 'mobile-history',
      surface: 'mobile',
      view: 'history',
      title: 'Mobile Git history',
      chrome: 'mobile-shell',
      tab: 'git',
      hero: false,
      livePath: '/workspace/harbor-shop/git/history',
      appSources: gitSources,
      requiredClasses: ['au-git-log', 'au-git-log-row', 'au-git-log-detail'],
      requiredDesktopClasses: [],
      requiredMobileComponents: ['gitLog', 'gitLogRow'],
      copy: ['feat(storefront): featured product grid'],
      html: mobileHistory(),
    }),
    scene({
      id: 'mobile-agents',
      surface: 'mobile',
      view: 'agents',
      title: 'Mobile Agents',
      chrome: 'mobile-shell',
      tab: 'agents',
      hero: false,
      livePath: '/workspace/harbor-shop/agents',
      appSources: [
        ...mobileShellSources,
        'AgentUp.Chat/src/components/AgentChatScreen.tsx',
        'AgentUp.Mobile/src/features/agents/components/AgentsOverviewScreen.tsx',
      ],
      requiredClasses: ['au-choice', 'au-chat-user', 'au-card'],
      requiredDesktopClasses: [],
      requiredMobileComponents: ['choice', 'chatUser'],
      copy: ['Codex'],
      html: mobileAgents(),
    }),
    scene({
      id: 'mobile-settings',
      surface: 'mobile',
      view: 'settings',
      title: 'Mobile settings',
      chrome: 'mobile-shell',
      tab: 'settings',
      hero: false,
      livePath: '/workspace/harbor-shop/settings',
      appSources: [
        ...mobileShellSources,
        'AgentUp.Mobile/src/features/workspaces/components/WorkspaceSettingsScreen.tsx',
        'AgentUp.Mobile/src/features/capabilities/components/CapabilitiesScreen.tsx',
      ],
      requiredClasses: ['au-workspace', 'au-mobile-tab-bar', 'au-button'],
      requiredDesktopClasses: [],
      requiredMobileComponents: ['workspace', 'mobileTabBar'],
      copy: ['.NET', 'Docker', 'Codex'],
      html: mobileSettings(),
    }),
    scene({
      id: 'mobile-file-viewer',
      surface: 'mobile',
      view: 'file-viewer',
      title: 'Mobile file viewer',
      chrome: 'mobile-shell',
      tab: 'git',
      hero: false,
      livePath: '/workspace/harbor-shop/git',
      appSources: gitSources,
      requiredClasses: ['au-file-viewer', 'au-file-viewer-line'],
      requiredDesktopClasses: [],
      requiredMobileComponents: ['fileViewer'],
      copy: ['ProductGrid.tsx'],
      html: mobileFileViewer(),
    }),
  ];
}

export function screenshotScreens() {
  return [
    screen({
      id: 'sign-in',
      title: 'Sign-in',
      intro: 'Connect to a Server from a compact pane on the canvas. Saved URLs ellipsize; selecting one applies the stored sign-in.',
      desktopId: 'desktop-sign-in',
      mobileId: 'mobile-sign-in',
    }),
    screen({
      id: 'workspaces',
      title: 'Workspaces',
      intro: 'Each checkout stays identifiable on the rail. Desktop shows the selected workspace overview; Mobile lists the same rows on their own screen.',
      desktopId: 'desktop-workspaces',
      mobileId: 'mobile-workspaces',
    }),
    screen({
      id: 'applications',
      title: 'Applications',
      intro: 'Each tab is a running application surface. Desktop opens the allocated HTTP port; Mobile lists Apps in the workspace bottom bar.',
      desktopId: 'desktop-applications',
      mobileId: 'mobile-apps',
    }),
    screen({
      id: 'console',
      title: 'Console',
      intro: 'Application stdout lives next to the running app, including [install] output. This surface is Desktop chrome; Mobile does not host a console.',
      desktopId: 'desktop-console',
    }),
    screen({
      id: 'git',
      title: 'Git',
      intro: 'Working-tree review. Desktop Git changes and Mobile Git show the same change tree. Neither surface mutates the agent commit queue.',
      desktopId: 'desktop-git',
      mobileId: 'mobile-git',
    }),
    screen({
      id: 'review',
      title: 'Review',
      intro: 'Mobile Review reads the Server-owned proposal queue. It does not reconstruct ancestry or infer verification state locally.',
      mobileId: 'mobile-review',
    }),
    screen({
      id: 'history',
      title: 'History',
      intro: 'A bounded commit log with graph lanes, a pinned timestamp column, and a sticky selected-commit detail.',
      desktopId: 'desktop-history',
      mobileId: 'mobile-history',
    }),
    screen({
      id: 'file-viewer',
      title: 'File viewer',
      intro: 'Readonly inspection of a changed file: hunk jumps, line kinds, and syntax tokens. It is not a second editor.',
      desktopId: 'desktop-file-viewer',
      mobileId: 'mobile-file-viewer',
    }),
    screen({
      id: 'agents',
      title: 'Agents',
      intro: 'Desktop Agent is the live ACP session. Mobile Agents is the picker that opens that session.',
      desktopId: 'desktop-agents',
      mobileId: 'mobile-agents',
    }),
    screen({
      id: 'diagnostics',
      title: 'Diagnostics',
      intro: 'Paginated audit rows for the selected application: process, browser, and health evidence. Desktop hosts this next to Console and Metrics.',
      desktopId: 'desktop-diagnostics',
    }),
    screen({
      id: 'metrics',
      title: 'Metrics',
      intro: 'CPU, memory, and request duration for the selected application, on quieter metrics cards rather than a dashboard theme.',
      desktopId: 'desktop-metrics',
    }),
    screen({
      id: 'validation',
      title: 'Validation',
      intro: 'Recorded GUI flows with visible pass, running, and failed stages. The Server owns the flow; Desktop renders it.',
      desktopId: 'desktop-validation',
    }),
    screen({
      id: 'database',
      title: 'Database',
      intro: 'Query the application database from the same application chrome, with a selected table and a run control.',
      desktopId: 'desktop-database',
    }),
    screen({
      id: 'capabilities',
      title: 'Capabilities',
      intro: 'Enable Server-owned modules. Desktop uses an overlay; Mobile Settings is the same list on the workspace tab. Clients never talk to the remote registry.',
      desktopId: 'desktop-capabilities',
      mobileId: 'mobile-settings',
    }),
  ];
}

export function framedSceneHtml(scene) {
  return frame(scene);
}

export function wrapSceneDocument(scene, css) {
  const size = scene.surface === 'mobile' ? mobileSize : desktopSize;
  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=${size.width}, height=${size.height}, initial-scale=1" />
  <title>${escapeHtml(scene.title)}</title>
  <style>${css}</style>
</head>
<body class="au-theme">
${frame(scene)}
</body>
</html>
`;
}

function scene(value) {
  return {
    ...value,
    mediaFile: `${value.id}.png`,
    htmlFile: `${value.id}.html`,
    width: value.surface === 'mobile' ? mobileSize.width : desktopSize.width,
    height: value.surface === 'mobile' ? mobileSize.height : desktopSize.height,
    livePath: value.livePath ?? '',
    tab: value.tab ?? '',
  };
}

function screen(value) {
  return {
    id: value.id,
    title: value.title,
    intro: value.intro,
    desktopId: value.desktopId ?? '',
    mobileId: value.mobileId ?? '',
  };
}

function frame(scene) {
  if (scene.chrome === 'desktop-signin') return desktopFrame(desktopSignInScreen(scene.html), false);
  if (scene.chrome === 'desktop-shell') return desktopFrame(scene.html, true);
  if (scene.chrome === 'mobile-signin') return mobileFrame('Connect', scene.html, null);
  if (scene.chrome === 'mobile-list') return mobileFrame('Workspaces', scene.html, null);
  return mobileFrame(scene.title.replace(/^Mobile /, ''), scene.html, scene.tab || 'apps');
}

function desktopFrame(inner, withRail) {
  const size = `width:${desktopSize.width}px;height:${desktopSize.height}px`;
  const body = withRail
    ? `<div class="au-screenshot-body">${rail()}${inner}</div>`
    : inner;
  return `<div class="au-theme au-screenshot au-screenshot--desktop" style="${size}">
  ${desktopChrome()}
  ${body}
</div>`;
}

function mobileFrame(title, inner, tab) {
  const size = `width:${mobileSize.width}px;height:${mobileSize.height}px`;
  const bar = tab ? mobileTabBar(tab) : '';
  return `<div class="au-theme au-screenshot au-screenshot--mobile" style="${size}">
  <div class="au-mobile-bar au-cluster"><strong>${escapeHtml(title)}</strong></div>
  <div class="au-screenshot-mobile-body">${inner}</div>
  ${bar}
</div>`;
}

function desktopChrome() {
  return `<div class="au-chrome au-cluster" style="justify-content:space-between;padding:0 0.75rem;">
    <span class="au-cluster">
      <span class="au-title-tool au-chrome-icon">☰</span>
      <span class="au-title-tool au-chrome-icon">↻</span>
      <span class="au-badge au-badge--healthy"><span class="au-status-dot au-status-dot--healthy"></span>Server online</span>
    </span>
    <strong class="au-chrome-title">Agent-Up</strong>
    <span class="au-cluster">
      <span class="au-chrome-button au-chrome-icon">−</span>
      <span class="au-chrome-button au-chrome-icon">□</span>
      <span class="au-chrome-button au-chrome-close au-chrome-icon">×</span>
    </span>
  </div>`;
}

function rail() {
  return `<aside class="au-rail au-screenshot-rail">
    <div class="au-cluster" style="justify-content:space-between;">
      <span class="au-field-label">Workspaces</span>
      <button class="au-workspace-add" type="button">+</button>
    </div>
    ${workspaceRow(true)}
  </aside>`;
}

function workspaceRow(selected) {
  const klass = selected ? 'au-workspace au-workspace--selected' : 'au-workspace';
  return `<div class="${klass}">
      <button class="au-lifecycle-button" type="button">■</button>
      <span class="au-status-dot au-status-dot--healthy"></span>
      <span>
        <strong class="au-workspace-name">Harbor Shop</strong>
        <small class="au-workspace-branch">main</small>
      </span>
    </div>`;
}

function appTabs(selected) {
  const tab = (name, on, health) =>
    `<button class="au-app-tab${on ? ' au-app-tab--selected' : ''}" type="button"><span class="au-status-dot ${health}"></span> ${name}</button>`;
  return `<div class="au-screenshot-tabs" role="tablist">
    ${tab('Storefront', selected === 'Storefront', 'au-status-dot--healthy')}
    ${tab('Orders API', selected === 'Orders API', 'au-status-dot--healthy')}
  </div>`;
}

function subTabs(selected) {
  const tab = (name, on) =>
    `<button class="au-subtab${on ? ' au-subtab--selected' : ''}" type="button">${name}</button>`;
  return `<div class="au-screenshot-subtabs">
    ${tab('9100', selected === 'port')}
    ${tab('Console', selected === 'Console')}
    ${tab('Metrics', selected === 'Metrics')}
    ${tab('Diagnostics', selected === 'Diagnostics')}
    ${tab('Database', selected === 'Database')}
    ${tab('Git', selected === 'Git')}
  </div>`;
}

function main(tabs, subs, body) {
  return `<main class="au-screenshot-main">${tabs}${subs}${body}</main>`;
}

function desktopSignInScreen(form) {
  return `<div class="au-screenshot-signin">${form}</div>`;
}

function desktopSignIn() {
  return `<form class="au-sign-in au-stack">
    <p class="au-eyebrow">Agent-Up Server</p>
    <h2 class="au-page-title">Connect to server</h2>
    <p class="au-muted">Choose a saved server or enter a URL.</p>
    <span class="au-field-label">Server URL</span>
    <input class="au-input" value="http://127.0.0.1:9" readonly />
    <span class="au-field-label">Saved servers</span>
    <div class="au-sign-in-list">
      <button class="au-choice au-choice--compact au-choice--selected" type="button" title="http://127.0.0.1:9"><span class="au-choice-label">Demo</span></button>
    </div>
    <button class="au-button" type="button">Connect</button>
  </form>`;
}

function desktopWorkspaces() {
  return main(
    `<div class="au-screenshot-tabs" role="tablist"><button class="au-app-tab au-app-tab--selected" type="button">Overview</button></div>`,
    '',
    `<div class="au-screenshot-pane au-pane" style="padding:1rem;">
      <p class="au-field-label">Harbor Shop</p>
      <p class="au-muted">/demo/harbor-shop · main · a1b2c3d</p>
      <div class="au-cluster" style="margin-top:1rem;">
        <div class="au-metrics-card"><span class="au-eyebrow">State</span><strong>Running</strong></div>
        <div class="au-metrics-card"><span class="au-eyebrow">CPU</span><strong>4.2%</strong></div>
        <div class="au-metrics-card"><span class="au-eyebrow">Memory</span><strong>256 MB</strong></div>
        <div class="au-metrics-card"><span class="au-eyebrow">Apps</span><strong>2</strong></div>
      </div>
    </div>`,
  );
}

function desktopApplications() {
  return main(
    appTabs('Storefront'),
    subTabs('port'),
    `<div class="au-screenshot-browser">
      <div class="au-cluster">
        <button class="au-browser-button" type="button">‹</button>
        <button class="au-browser-button" type="button">›</button>
        <button class="au-browser-button" type="button">↻</button>
        <input class="au-address-bar" value="http://127.0.0.1:9100/" readonly />
      </div>
      <div class="au-screenshot-browser-page">
        <p class="au-eyebrow">STOREFRONT</p>
        <h2 class="au-page-title">Harbor Shop</h2>
        <p class="au-muted">Featured goods from the harbor catalog.</p>
        <div class="au-cluster" style="margin-top:1rem;">
          <div class="au-card"><strong>Harbor Mug</strong><p class="au-muted">Stoneware, fog glaze</p></div>
          <div class="au-card"><strong>Canvas Tote</strong><p class="au-muted">Waxed canvas, brass snap</p></div>
          <div class="au-card"><strong>Chart Lamp</strong><p class="au-muted">Brass shade, linen cord</p></div>
        </div>
      </div>
    </div>`,
  );
}

function desktopConsole() {
  return main(
    appTabs('Storefront'),
    subTabs('Console'),
    `<div class="au-screenshot-pane"><pre class="au-console">[install] npm install
Storefront listening on WEB_PORT</pre></div>`,
  );
}

function gitTree() {
  return `<div class="au-git-change-list" style="padding:0.5rem;">
    <div class="au-cluster" style="margin-bottom:0.5rem;">
      <button class="au-button au-button--secondary au-button--compact" type="button">Review <span class="au-git-insertions">+2</span> <span class="au-git-deletions">−0</span></button>
      <button class="au-button au-button--compact" type="button">Commit <span class="au-git-insertions au-git-insertions--on-primary">+2</span></button>
    </div>
    <div class="au-git-row">
      <span class="au-checkbox au-checkbox--checked"></span>
      <button class="au-git-tree-toggle au-git-tree-toggle--expanded" type="button">▾</button>
      <span class="au-git-change-name au-git-change-name--directory">apps</span>
    </div>
    <div class="au-git-row au-git-row--selected">
      <span class="au-checkbox au-checkbox--checked"></span>
      <span class="au-git-tree-guide"></span>
      <span class="au-git-change-name">ProductGrid.tsx</span>
      <span class="au-git-status au-git-status--modified">M</span>
    </div>
    <div class="au-git-row">
      <span class="au-checkbox au-checkbox--checked"></span>
      <span class="au-git-tree-guide"></span>
      <span class="au-git-change-name">orders.ts</span>
      <span class="au-git-status au-git-status--modified">M</span>
    </div>
  </div>`;
}

function desktopGit() {
  return main(appTabs('Storefront'), subTabs('Git'), `<div class="au-screenshot-pane au-pane">${gitTree()}</div>`);
}

function gitHistory() {
  return `<div class="au-git-log">
    <div class="au-git-log-detail">
      <span class="au-git-log-subject">feat(storefront): featured product grid</span>
      <span class="au-git-log-author">Demo</span>
      <div class="au-cluster">
        <span class="au-git-log-ref au-git-log-ref--head">HEAD</span>
        <span class="au-git-log-ref">main</span>
      </div>
    </div>
    <div class="au-git-log-track">
      <div>
        <div class="au-git-log-row au-git-log-row--selected"><span class="au-git-log-time">22.09.26 07:30</span></div>
      </div>
      <div class="au-git-log-graph-scroll">
        <div class="au-git-log-row">
          <svg class="au-git-log-graph" width="28" height="28" viewBox="0 0 28 28" aria-hidden="true">
            <g class="au-git-log-lane-0">
              <circle class="au-git-log-graph-node au-git-log-graph-node--filled" cx="14" cy="14" r="3.5"></circle>
            </g>
          </svg>
        </div>
      </div>
    </div>
  </div>`;
}

function desktopHistory() {
  return main(appTabs('Storefront'), subTabs('Git'), `<div class="au-screenshot-pane au-pane">${gitHistory()}</div>`);
}

function desktopAgents() {
  return main(
    `<div class="au-screenshot-tabs" role="tablist"><button class="au-app-tab au-app-tab--selected" type="button">Agent</button></div>`,
    '',
    `<div class="au-screenshot-pane au-pane" style="padding:1rem;">
      <div class="au-stack">
        <button class="au-choice au-choice--selected" type="button">Codex <span class="au-accent">Available</span></button>
        <div class="au-chat-transcript">
          <div class="au-chat-user">What is running in Harbor Shop?</div>
          <div class="au-chat-work"><span class="au-field-label">Tool</span> workspace status</div>
          <div class="au-card"><span class="au-field-label">Codex</span><p>Harbor Shop is running locally. The storefront is showing three featured products, and the Orders API reports twelve open orders.</p></div>
        </div>
      </div>
    </div>`,
  );
}

function desktopDiagnostics() {
  return main(
    appTabs('Storefront'),
    subTabs('Diagnostics'),
    `<div class="au-screenshot-pane au-pane">
      <div class="au-audit-header">Time · Kind · Message</div>
      <div class="au-audit-row">08:00 · process · Storefront listening on WEB_PORT</div>
      <div class="au-audit-row">08:00 · process · Orders API listening on API_PORT</div>
      <div class="au-cluster" style="margin-top:0.75rem;">
        <button class="au-page-jump au-page-jump--current" type="button">1</button>
      </div>
    </div>`,
  );
}

function desktopMetrics() {
  return main(
    appTabs('Storefront'),
    subTabs('Metrics'),
    `<div class="au-screenshot-pane au-pane" style="padding:1rem;">
      <div class="au-cluster">
        <div class="au-metrics-card"><span class="au-eyebrow">CPU</span><strong>4.2%</strong></div>
        <div class="au-metrics-card"><span class="au-eyebrow">Memory</span><strong>256 MB</strong></div>
      </div>
      <div class="au-metrics-chart" style="margin-top:1rem;">Harbor Shop · CPU · memory · request duration</div>
    </div>`,
  );
}

function desktopValidation() {
  return main(
    appTabs('Storefront'),
    subTabs('port'),
    `<div class="au-screenshot-pane au-pane" style="padding:1rem;">
      <p class="au-page-title">Validation</p>
      <div class="au-stack">
        <div class="au-validation-stage au-validation-stage--passed">Open storefront · passed</div>
        <div class="au-validation-stage au-validation-stage--running">Place order · running</div>
        <div class="au-validation-stage au-validation-stage--failed">Mark shipped · failed</div>
      </div>
    </div>`,
  );
}

function desktopDatabase() {
  return main(
    appTabs('Storefront'),
    subTabs('Database'),
    `<div class="au-screenshot-pane au-pane" style="padding:1rem;">
      <div class="au-stack">
        <div class="au-list-item au-list-item--selected">orders</div>
        <div class="au-list-item">customers</div>
        <textarea class="au-code-editor">select * from orders limit 20;</textarea>
        <button class="au-db-run" type="button">Run</button>
        <div class="au-cluster">
          <div class="au-db-header-cell">id</div>
          <div class="au-db-data-cell">1008</div>
        </div>
      </div>
    </div>`,
  );
}

function desktopCapabilities() {
  return main(
    `<div class="au-screenshot-tabs" role="tablist"><button class="au-app-tab au-app-tab--selected" type="button">Overview</button></div>`,
    '',
    `<div class="au-screenshot-pane" style="display:grid;place-items:center;">
      <div class="au-overlay-panel" style="padding:1.25rem;max-width:36rem;">
        <p class="au-page-title">Capability modules</p>
        <p class="au-muted">Enable packages on this Server. Clients never talk to the remote registry.</p>
        <div class="au-stack" style="margin-top:1rem;">
          <div class="au-card"><strong>.NET</strong><p class="au-muted">agent-up · 1.0.0</p><span class="au-badge au-badge--healthy"><span class="au-status-dot au-status-dot--healthy"></span>Ready</span></div>
          <div class="au-card"><strong>Docker</strong><p class="au-muted">agent-up · 1.0.0</p><span class="au-badge au-badge--healthy"><span class="au-status-dot au-status-dot--healthy"></span>Ready</span></div>
          <div class="au-card"><strong>Codex</strong><p class="au-muted">agent-up · 1.0.0</p><span class="au-badge au-badge--healthy"><span class="au-status-dot au-status-dot--healthy"></span>Ready</span></div>
        </div>
      </div>
    </div>`,
  );
}

function fileViewer() {
  return `<div class="au-overlay-panel au-file-viewer">
    <div class="au-file-viewer-header">
      <span class="au-file-viewer-path">apps/storefront/ProductGrid.tsx</span>
      <span class="au-file-viewer-status">Modified</span>
      <button class="au-button au-button--secondary au-button--compact" type="button">Close</button>
    </div>
    <div class="au-file-viewer-nav">
      <button class="au-file-viewer-jump" type="button">@@ −1,3 +1,4 @@</button>
    </div>
    <div class="au-file-viewer-body">
      <div class="au-file-viewer-line au-file-viewer-line--added au-file-viewer-line--current">
        <span class="au-file-viewer-gutter">1</span>
        <span class="au-file-viewer-prefix au-file-viewer-prefix--added">+</span>
        <span class="au-file-viewer-code"><span class="au-syntax-keyword">export const</span> featured = [<span class="au-syntax-string">'Harbor Mug'</span>];</span>
      </div>
      <div class="au-file-viewer-line">
        <span class="au-file-viewer-gutter">2</span>
        <span class="au-file-viewer-prefix"> </span>
        <span class="au-file-viewer-code"><span class="au-syntax-keyword">export function</span> <span class="au-syntax-function">ProductGrid</span>() {</span>
      </div>
    </div>
  </div>`;
}

function desktopFileViewer() {
  return main(appTabs('Storefront'), subTabs('Git'), `<div class="au-screenshot-pane" style="display:grid;place-items:center;padding:1rem;">${fileViewer()}</div>`);
}

function mobileSignIn() {
  return desktopSignIn();
}

function mobileWorkspaces() {
  return `<div class="au-stack">
    ${workspaceRow(true)}
    <button class="au-button" type="button">Clone</button>
  </div>`;
}

function mobileApps() {
  return `<div class="au-stack">
    <div class="au-workspace au-workspace--selected">
      <span class="au-status-dot au-status-dot--healthy"></span>
      <span><strong class="au-workspace-name">Storefront</strong><small class="au-workspace-branch">Running</small></span>
    </div>
    <div class="au-workspace">
      <span class="au-status-dot au-status-dot--healthy"></span>
      <span><strong class="au-workspace-name">Orders API</strong><small class="au-workspace-branch">Running</small></span>
    </div>
  </div>`;
}

function mobileGit() {
  return gitTree();
}

function mobileReview() {
  return `<div class="au-stack">
    <p class="au-page-title">Review</p>
    <p class="au-muted">Harbor Shop proposal queue is empty. Unassigned files stay on the working tree.</p>
    <div class="au-git-row"><span class="au-git-change-name">ProductGrid.tsx</span><span class="au-git-status au-git-status--modified">M</span></div>
    <div class="au-git-row"><span class="au-git-change-name">orders.ts</span><span class="au-git-status au-git-status--modified">M</span></div>
    <button class="au-button" type="button">Commit</button>
  </div>`;
}

function mobileHistory() {
  return gitHistory();
}

function mobileAgents() {
  return `<div class="au-stack">
    <button class="au-choice au-choice--selected" type="button">Codex <span class="au-accent">Available</span></button>
    <div class="au-chat-user">Open Harbor Shop</div>
    <div class="au-card"><span class="au-field-label">Codex</span><p>Harbor Shop is running locally. The storefront is showing three featured products, and the Orders API reports twelve open orders.</p></div>
  </div>`;
}

function mobileSettings() {
  return `<div class="au-stack">
    <p class="au-field-label">Capabilities</p>
    <p class="au-muted">Enable packages on this Server. Clients never talk to the remote registry.</p>
    <div class="au-workspace"><span><strong class="au-workspace-name">.NET</strong><small class="au-workspace-branch">Ready</small></span><button class="au-button au-button--secondary" type="button">Disable</button></div>
    <div class="au-workspace"><span><strong class="au-workspace-name">Docker</strong><small class="au-workspace-branch">Ready</small></span><button class="au-button au-button--secondary" type="button">Disable</button></div>
    <div class="au-workspace"><span><strong class="au-workspace-name">Codex</strong><small class="au-workspace-branch">Ready</small></span><button class="au-button au-button--secondary" type="button">Disable</button></div>
  </div>`;
}

function mobileFileViewer() {
  return fileViewer();
}

function mobileTabBar(active) {
  const item = (id, label, svg) => `<button class="au-subtab au-nav-tab${id === active ? ' au-subtab--selected' : ''}" type="button" aria-selected="${id === active ? 'true' : 'false'}">
      <svg class="au-nav-icon" viewBox="0 0 24 24" aria-hidden="true">${svg}</svg>
      ${label}
    </button>`;
  return `<div class="au-mobile-tab-bar au-cluster" role="tablist">
    ${item('apps', 'Apps', '<rect x="3" y="3" width="7" height="7" rx="1.5"></rect><rect x="14" y="3" width="7" height="7" rx="1.5"></rect><rect x="3" y="14" width="7" height="7" rx="1.5"></rect><rect x="14" y="14" width="7" height="7" rx="1.5"></rect>')}
    ${item('git', 'Git', '<circle cx="6" cy="18" r="2.5"></circle><circle cx="18" cy="6" r="2.5"></circle><circle cx="18" cy="18" r="2.5"></circle><path d="M8.2 16.2 16 8"></path>')}
    ${item('agents', 'Agents', '<path d="M21 15a2 2 0 0 1-2 2H8l-4 3V7a2 2 0 0 1 2-2h13a2 2 0 0 1 2 2z"></path>')}
    ${item('settings', 'Settings', '<circle cx="12" cy="12" r="3"></circle><path d="M12 3.5v2.2M12 18.3v2.2M3.5 12h2.2M18.3 12h2.2"></path>')}
  </div>`;
}

function escapeHtml(value) {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;');
}

export { demoCopy };
