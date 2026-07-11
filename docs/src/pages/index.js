import React, { useState } from 'react';
import Layout from '@theme/Layout';
import Link from '@docusaurus/Link';
import styles from './index.module.css';

const problems = [
  {
    label: 'Service sprawl',
    title: 'Constantly starting multiple services',
    headline: 'One workspace command brings the whole stack back.',
    body: 'Agent-Up treats every workspace as a managed runtime. Frontend, API, background workers, and infrastructure are launched and restarted as one coherent environment instead of a checklist of forgotten commands.',
    layout: 'services',
    details: ['Frontend', 'API', 'Worker', 'Database'],
  },
  {
    label: 'Infrastructure collisions',
    title: 'Docker infrastructure collisions',
    headline: 'Parallel branches stop fighting over the same infrastructure.',
    body: 'Each workspace owns its infrastructure lifecycle. Containers, databases, networks, and runtime assumptions stay scoped to the worktree that needs them.',
    layout: 'docker',
    details: ['agent-1', 'agent-2', 'review'],
  },
  {
    label: 'Tab overload',
    title: 'Browser tab explosion',
    headline: 'The browser becomes workspace state, not loose debris.',
    body: 'A workspace has one isolated browser profile and stable application tabs. Restarting apps reloads the active session instead of scattering more tabs across your desktop.',
    layout: 'tabs',
    details: ['Frontend', 'Admin', 'Swagger'],
  },
  {
    label: 'Repeated login',
    title: 'Duplicated authentication',
    headline: 'Authenticate once, then let humans and agents share the session.',
    body: 'Humans and AI agents interact with the same workspace browser state. Cookies, local storage, and application state are preserved for validation instead of recreated in another browser.',
    layout: 'auth',
    details: ['Cookies', 'Local Storage', 'IndexedDB'],
  },
  {
    label: 'State drift',
    title: 'Inconsistent runtime state',
    headline: 'One Server knows what is actually running.',
    body: 'The Server is the source of truth for processes, ports, browser profiles, health, diagnostics, and event history. Every client reads the same state.',
    layout: 'state',
    details: ['Processes', 'Ports', 'Health', 'Events'],
  },
  {
    label: 'Manual recovery',
    title: 'Manual process management',
    headline: 'Stop nursing processes back to life by hand.',
    body: 'Workspace operations become server capabilities. CLI, Desktop, and MCP clients request actions from the same orchestration layer instead of each inventing process control.',
    layout: 'process',
    details: ['Restart', 'Stop', 'Logs', 'Status'],
  },
  {
    label: 'Weak validation',
    title: 'Difficult validation of AI-generated implementations',
    headline: 'Review behavior with browser evidence, not just diffs.',
    body: 'Agents can restart a workspace, inspect the shared browser, interact through MCP, retrieve diagnostics, capture screenshots, and generate Playwright tests from the recorded session.',
    layout: 'validation',
    details: ['Inspect', 'Interact', 'Screenshot', 'Playwright'],
  },
];

const capabilities = [
  'Server-owned runtime state',
  'Per-workspace port ranges',
  'Isolated browser profiles',
  'MCP-first automation',
  'Event-backed diagnostics',
  'Playwright generation',
];

const projects = [
  {
    id: 'A',
    name: 'Project A',
    agents: ['agent-1', 'agent-2'],
    serviceConfig: {
      MarketingSite: { secondaryTabs: ['Port 8080', 'Console', 'Metrics'] },
      Dashboard: { secondaryTabs: ['Port 3000', 'Console', 'Metrics'] },
      Backend: { secondaryTabs: ['OpenAPI', 'Console', 'Metrics'] },
      Worker: { secondaryTabs: ['Console', 'Metrics'] },
      Postgres: { secondaryTabs: ['Console', 'Metrics'] },
    },
    defaultService: 'MarketingSite',
  },
  {
    id: 'B',
    name: 'Project B',
    agents: ['agent-3'],
    serviceConfig: {
      Storefront: { secondaryTabs: ['Port 5000', 'Console', 'Metrics'] },
      AdminPanel: { secondaryTabs: ['Port 5001', 'Console', 'Metrics'] },
      Payments: { secondaryTabs: ['OpenAPI', 'Console', 'Metrics'] },
      Postgres: { secondaryTabs: ['Console', 'Metrics'] },
    },
    defaultService: 'Storefront',
  },
];

const getProjectForAgent = (agentId) => projects.find((p) => p.agents.includes(agentId));

const getInitials = (agentId) => {
  const project = getProjectForAgent(agentId);
  return `${project.id}${project.agents.indexOf(agentId) + 1}`;
};

function WebsiteMockup({ agent }) {
  const v2 = agent === 'agent-2';
  return (
    <div className={styles.websiteMockup}>
      <div className={styles.mockupNavBar}>
        <span className={styles.mockupBrand}>myapp</span>
        <div className={styles.mockupNavLinks}>
          <span>Home</span>
          <span>Pricing</span>
          <span>Docs</span>
        </div>
        <span className={styles.mockupNavCta}>{v2 ? 'Start free' : 'Sign up'}</span>
      </div>
      <div className={`${styles.mockupHeroSection} ${v2 ? styles.mockupHeroV2 : ''}`}>
        <div className={styles.mockupHeadline}>
          {v2 ? 'Ship 10× faster with AI' : 'Build something great'}
        </div>
        <div className={styles.mockupSubline}>
          {v2 ? 'Built for the AI era. Deploy in minutes.' : 'A modern platform for modern teams.'}
        </div>
        <div className={styles.mockupCtaRow}>
          <span className={styles.mockupPrimaryBtn}>{v2 ? 'Start free trial' : 'Get started'}</span>
          {!v2 && <span className={styles.mockupSecondaryBtn}>Learn more</span>}
        </div>
      </div>
      <div className={`${styles.mockupCardRow} ${v2 ? styles.mockupCardRowV2 : ''}`}>
        <div className={styles.mockupCard} />
        <div className={styles.mockupCard} />
        {!v2 && <div className={styles.mockupCard} />}
      </div>
    </div>
  );
}

function DashboardMockup({ agent }) {
  const v2 = agent === 'agent-2';
  const stats = v2
    ? [{ value: '2,851', label: 'Total users' }, { value: '$12.7k', label: 'Revenue' }, { value: '96%', label: 'Retention' }]
    : [{ value: '2,847', label: 'Total users' }, { value: '$12.4k', label: 'Revenue' }, { value: '94%', label: 'Retention' }];
  const users = [
    { name: 'Alice Chen', plan: 'Pro', status: 'active' },
    { name: 'Bob Smith', plan: 'Free', status: 'active' },
    { name: 'Carol Wu', plan: 'Team', status: 'trial' },
    ...(v2 ? [{ name: 'Dave Lee', plan: 'Pro', status: 'active' }] : []),
  ];
  return (
    <div className={styles.dashboardMockup}>
      <div className={styles.dashboardSidebar}>
        <div className={`${styles.dashboardNavItem} ${styles.dashboardNavActive}`}>Overview</div>
        <div className={styles.dashboardNavItem}>Users</div>
        <div className={styles.dashboardNavItem}>Analytics</div>
        <div className={styles.dashboardNavItem}>Settings</div>
      </div>
      <div className={styles.dashboardMain}>
        <div className={styles.dashboardStatRow}>
          {stats.map((s) => (
            <div key={s.label} className={styles.dashboardStat}>
              <span className={styles.dashboardStatValue}>{s.value}</span>
              <span className={styles.dashboardStatLabel}>{s.label}</span>
            </div>
          ))}
        </div>
        <div className={styles.dashboardTable}>
          <div className={styles.dashboardTableHeader}>
            <span>Name</span><span>Plan</span><span>Status</span>
          </div>
          {users.map((u) => (
            <div key={u.name} className={styles.dashboardTableRow}>
              <span>{u.name}</span>
              <span>{u.plan}</span>
              <span className={u.status === 'active' ? styles.statusActive : styles.statusTrial}>{u.status}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

const openApiEndpoints = {
  Backend: [
    { method: 'GET', path: '/api/users', desc: 'List all users' },
    { method: 'POST', path: '/api/users', desc: 'Create a user' },
    { method: 'GET', path: '/api/users/{id}', desc: 'Get user by ID' },
    { method: 'PUT', path: '/api/users/{id}', desc: 'Update user' },
    { method: 'DELETE', path: '/api/users/{id}', desc: 'Delete user' },
  ],
  Payments: [
    { method: 'POST', path: '/charges', desc: 'Create charge' },
    { method: 'GET', path: '/charges/{id}', desc: 'Get charge' },
    { method: 'POST', path: '/refunds', desc: 'Issue refund' },
    { method: 'GET', path: '/subscriptions', desc: 'List subscriptions' },
    { method: 'POST', path: '/subscriptions', desc: 'Create subscription' },
  ],
};

function OpenApiMockup({ service }) {
  const endpoints = openApiEndpoints[service] ?? openApiEndpoints.Backend;
  const title = service === 'Payments' ? 'Payments' : 'Users';
  return (
    <div className={styles.openApiMockup}>
      <div className={styles.apiTitle}>{title} <span>{endpoints.length} endpoints</span></div>
      {endpoints.map((ep) => (
        <div key={`${ep.method}-${ep.path}`} className={styles.apiEndpoint}>
          <span className={`${styles.methodBadge} ${styles[`method${ep.method}`]}`}>{ep.method}</span>
          <span className={styles.apiPath}>{ep.path}</span>
          <span className={styles.apiDesc}>{ep.desc}</span>
        </div>
      ))}
    </div>
  );
}

function StorefrontMockup() {
  const products = [
    { name: 'Merino Tee', price: '$34', tag: 'New' },
    { name: 'Field Jacket', price: '$129', tag: 'Sale' },
    { name: 'Canvas Bag', price: '$58', tag: null },
  ];
  return (
    <div className={styles.storefrontMockup}>
      <div className={styles.storeNav}>
        <span className={styles.storeBrand}>shopcraft</span>
        <div className={styles.storeNavLinks}><span>Products</span><span>About</span></div>
        <span className={styles.storeCart}>Cart (0)</span>
      </div>
      <div className={styles.storeGrid}>
        {products.map((p) => (
          <div key={p.name} className={styles.storeProduct}>
            <div className={styles.storeProductImg}>
              {p.tag && <span className={styles.storeProductTag}>{p.tag}</span>}
            </div>
            <div className={styles.storeProductName}>{p.name}</div>
            <div className={styles.storeProductPrice}>{p.price}</div>
          </div>
        ))}
      </div>
    </div>
  );
}

function AdminPanelMockup() {
  const orders = [
    { id: '#1042', customer: 'Alice Chen', amount: '$129.00', status: 'shipped' },
    { id: '#1041', customer: 'Bob Smith', amount: '$34.00', status: 'delivered' },
    { id: '#1040', customer: 'Carol Wu', amount: '$187.00', status: 'pending' },
  ];
  const statusClass = { shipped: styles.orderShipped, delivered: styles.orderDelivered, pending: styles.orderPending };
  return (
    <div className={styles.adminMockup}>
      <div className={styles.adminHeader}>
        <span className={styles.adminTitle}>Orders</span>
        <span className={styles.adminBadge}>{orders.length} recent</span>
      </div>
      <div className={styles.adminTableWrap}>
        <div className={styles.adminTableHeader}>
          <span>Order</span><span>Customer</span><span>Amount</span><span>Status</span>
        </div>
        {orders.map((o) => (
          <div key={o.id} className={styles.adminTableRow}>
            <span className={styles.adminOrderId}>{o.id}</span>
            <span>{o.customer}</span>
            <span>{o.amount}</span>
            <span className={statusClass[o.status]}>{o.status}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

function DbConsoleMockup() {
  const tables = ['users', 'sessions', 'products', 'orders'];
  const rows = [
    { id: 1, email: 'alice@example.com', role: 'admin' },
    { id: 2, email: 'bob@example.com', role: 'user' },
    { id: 3, email: 'carol@example.com', role: 'user' },
  ];
  return (
    <div className={styles.dbMockup}>
      <div className={styles.dbTableList}>
        <div className={styles.dbListHeader}>Tables</div>
        {tables.map((t) => (
          <div key={t} className={t === 'users' ? styles.dbTableActive : styles.dbTableItem}>{t}</div>
        ))}
      </div>
      <div className={styles.dbResultPanel}>
        <div className={styles.dbQueryBar}>SELECT * FROM users LIMIT 3</div>
        <table className={styles.dbTable}>
          <thead>
            <tr><th>id</th><th>email</th><th>role</th></tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.id}>
                <td>{r.id}</td>
                <td>{r.email}</td>
                <td>{r.role}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function ConsoleMockup({ service }) {
  const lines = {
    MarketingSite: [
      ['$', 'npm run dev'],
      ['▸', 'vite v5.4.2 ready on http://localhost:8080'],
      ['▸', 'HMR active'],
      ['›', 'GET / 200 in 4ms'],
      ['›', 'GET /assets/index.js 200 in 2ms'],
    ],
    Dashboard: [
      ['$', 'npm run dev'],
      ['▸', 'Next.js 14 ready on http://localhost:3000'],
      ['▸', 'HMR active'],
      ['›', 'GET / 200 in 6ms'],
      ['›', 'GET /dashboard 200 in 8ms'],
    ],
    Backend: [
      ['$', 'cargo run'],
      ['▸', 'Listening on 0.0.0.0:3000'],
      ['›', 'GET /api/health 200 3ms'],
      ['›', 'POST /api/users 201 11ms'],
      ['›', 'GET /api/users 200 7ms'],
    ],
    Worker: [
      ['$', 'node worker.js'],
      ['▸', 'Worker started, polling queue...'],
      ['›', 'Job #1042 picked up'],
      ['›', 'Job #1042 completed in 342ms'],
      ['›', 'Job #1043 picked up'],
    ],
    Postgres: [
      ['$', 'psql -U postgres'],
      ['▸', 'Connected to PostgreSQL 16.2'],
      ['›', 'CREATE TABLE users (...)  OK'],
      ['›', 'INSERT 0 3'],
      ['›', 'SELECT 3'],
    ],
    Storefront: [
      ['$', 'npm run build && npm start'],
      ['▸', 'Next.js 15 ready on http://localhost:5000'],
      ['▸', 'Compiled 847 modules'],
      ['›', 'GET / 200 in 12ms'],
      ['›', 'GET /products 200 in 8ms'],
    ],
    AdminPanel: [
      ['$', 'python manage.py runserver 5001'],
      ['▸', 'Django 5.0 dev server at port 5001'],
      ['▸', 'Watching for file changes...'],
      ['›', 'GET /admin/ 200 5ms'],
      ['›', 'GET /orders/ 200 3ms'],
    ],
    Payments: [
      ['$', 'go run ./cmd/payments'],
      ['▸', 'Payments service ready on :8080'],
      ['▸', 'Stripe webhook listener active'],
      ['›', 'POST /charges 201 24ms'],
      ['›', 'POST /subscriptions 201 31ms'],
    ],
  };
  const serviceLines = lines[service] || lines.MarketingSite;
  return (
    <div className={styles.consoleMockup}>
      {serviceLines.map(([prefix, text], i) => (
        <div key={i} className={prefix === '$' ? styles.consoleCmd : styles.consoleLine}>
          <span className={styles.consolePrefix}>{prefix}</span>
          <span>{text}</span>
        </div>
      ))}
    </div>
  );
}

function MetricsMockup() {
  const metrics = [
    { value: '142ms', label: 'Avg latency' },
    { value: '99.2%', label: 'Uptime' },
    { value: '1.2k', label: 'Req / min' },
    { value: '0', label: 'Errors' },
  ];
  const bars = [4, 6, 5, 8, 10, 9, 7, 8, 11, 9, 8, 10];
  return (
    <div className={styles.metricsMockup}>
      <div className={styles.metricGrid}>
        {metrics.map((m) => (
          <div key={m.label} className={styles.metricCard}>
            <span className={styles.metricValue}>{m.value}</span>
            <span className={styles.metricLabel}>{m.label}</span>
          </div>
        ))}
      </div>
      <div className={styles.sparkline}>
        {bars.map((h, i) => (
          <span key={i} style={{ height: `${h * 8}px` }} />
        ))}
      </div>
    </div>
  );
}

function ContentPane({ projectId, service, tab, agent }) {
  if (projectId === 'A') {
    if (service === 'MarketingSite' && tab === 'Port 8080') return <WebsiteMockup agent={agent} />;
    if (service === 'Dashboard' && tab === 'Port 3000') return <DashboardMockup agent={agent} />;
    if (service === 'Backend' && tab === 'OpenAPI') return <OpenApiMockup service="Backend" />;
    if (service === 'Postgres' && tab === 'Console') return <DbConsoleMockup />;
  }
  if (projectId === 'B') {
    if (service === 'Storefront' && tab === 'Port 5000') return <StorefrontMockup />;
    if (service === 'AdminPanel' && tab === 'Port 5001') return <AdminPanelMockup />;
    if (service === 'Payments' && tab === 'OpenAPI') return <OpenApiMockup service="Payments" />;
    if (service === 'Postgres' && tab === 'Console') return <DbConsoleMockup />;
  }
  if (tab === 'Console') return <ConsoleMockup service={service} />;
  if (tab === 'Metrics') return <MetricsMockup />;
  return null;
}

function HeroMockup() {
  const [collapsed, setCollapsed] = useState(false);
  const [activeAgent, setActiveAgent] = useState('agent-1');
  const [activeService, setActiveService] = useState('MarketingSite');
  const [activeTab, setActiveTab] = useState('Port 8080');

  const activeProject = getProjectForAgent(activeAgent);

  const handleAgentChange = (agentId) => {
    const newProject = getProjectForAgent(agentId);
    if (newProject.id !== activeProject.id) {
      const svc = newProject.defaultService;
      setActiveService(svc);
      setActiveTab(newProject.serviceConfig[svc].secondaryTabs[0]);
    }
    setActiveAgent(agentId);
  };

  const handleServiceChange = (service) => {
    setActiveService(service);
    setActiveTab(activeProject.serviceConfig[service].secondaryTabs[0]);
  };

  const secondaryTabs = activeProject.serviceConfig[activeService]?.secondaryTabs ?? [];

  return (
    <div
      className={`${styles.heroVisual} ${collapsed ? styles.heroVisualCollapsed : ''}`}
      aria-label="Agent-Up workspace topology"
    >
      <div className={styles.windowChrome}>
        <span /><span /><span />
      </div>
      <div className={`${styles.workspaceRail} ${collapsed ? styles.workspaceRailCollapsed : ''}`}>
        {!collapsed && <strong>Workspaces</strong>}
        <div className={styles.agentList}>
          {projects.map((project, pi) => (
            collapsed ? (
              <React.Fragment key={project.id}>
                {pi > 0 && <div className={styles.projectDivider} />}
                {project.agents.map((agentId) => (
                  <button
                    key={agentId}
                    type="button"
                    className={`${styles.agentAvatar} ${activeAgent === agentId ? styles.agentAvatarActive : ''}`}
                    onClick={() => handleAgentChange(agentId)}
                    title={`${project.name} — ${agentId}`}
                  >
                    {getInitials(agentId)}
                  </button>
                ))}
              </React.Fragment>
            ) : (
              <div key={project.id} className={`${styles.projectSection} ${pi > 0 ? styles.projectSectionBorder : ''}`}>
                <div className={styles.projectLabel}>{project.name}</div>
                {project.agents.map((agentId) => (
                  <button
                    key={agentId}
                    type="button"
                    className={`${styles.agentEntry} ${activeAgent === agentId ? styles.agentActive : ''}`}
                    onClick={() => handleAgentChange(agentId)}
                  >
                    {agentId}
                  </button>
                ))}
              </div>
            )
          ))}
        </div>
        <button
          type="button"
          className={styles.collapseToggle}
          onClick={() => setCollapsed(!collapsed)}
          title={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
        >
          {collapsed ? '›' : '‹'}
        </button>
      </div>
      <div className={styles.runtimePanel}>
        <div className={styles.serviceTabs}>
          {Object.keys(activeProject.serviceConfig).map((service) => (
            <button
              key={service}
              type="button"
              className={`${styles.serviceTab} ${activeService === service ? styles.serviceTabActive : ''}`}
              onClick={() => handleServiceChange(service)}
            >
              {service}
            </button>
          ))}
        </div>
        <div className={styles.secondaryBar}>
          {secondaryTabs.map((tab) => (
            <button
              key={tab}
              type="button"
              className={`${styles.secondaryTab} ${activeTab === tab ? styles.secondaryTabActive : ''}`}
              onClick={() => setActiveTab(tab)}
            >
              {tab}
            </button>
          ))}
        </div>
        <div className={styles.contentPane}>
          <ContentPane
            projectId={activeProject.id}
            service={activeService}
            tab={activeTab}
            agent={activeAgent}
          />
        </div>
      </div>
    </div>
  );
}

function ProblemVisual({problem}) {
  if (problem.layout === 'services') {
    return (
      <div className={styles.serviceVisual}>
        {problem.details.map((item) => (
          <div key={item}>{item}</div>
        ))}
      </div>
    );
  }

  if (problem.layout === 'docker') {
    return (
      <div className={styles.dockerVisual}>
        {problem.details.map((item, index) => (
          <div key={item}>
            <span>{item}</span>
            <strong>{`${5200 + index * 100}-${5299 + index * 100}`}</strong>
          </div>
        ))}
      </div>
    );
  }

  if (problem.layout === 'tabs') {
    return (
      <div className={styles.tabsVisual}>
        <div className={styles.tabStack}>
          <span />
          <span />
          <span />
          <span />
        </div>
        <div className={styles.singleBrowser}>
          {problem.details.map((item) => (
            <span key={item}>{item}</span>
          ))}
        </div>
      </div>
    );
  }

  if (problem.layout === 'auth') {
    return (
      <div className={styles.authVisual}>
        <div>Human</div>
        <strong>Shared browser profile</strong>
        <div>Agent</div>
      </div>
    );
  }

  if (problem.layout === 'state') {
    return (
      <div className={styles.stateVisual}>
        <strong>AgentUp.Server</strong>
        {problem.details.map((item) => (
          <span key={item}>{item}</span>
        ))}
      </div>
    );
  }

  if (problem.layout === 'process') {
    return (
      <div className={styles.processVisual}>
        {problem.details.map((item) => (
          <button type="button" key={item}>{item}</button>
        ))}
      </div>
    );
  }

  return (
    <div className={styles.validationVisual}>
      {problem.details.map((item) => (
        <span key={item}>{item}</span>
      ))}
    </div>
  );
}

function ProblemSection({problem, index}) {
  return (
    <section className={`${styles.problemSection} ${styles[problem.layout]}`}>
      <div>
        <p className={styles.problemIndex}>{String(index + 1).padStart(2, '0')} / {problem.label}</p>
        <h2>{problem.title}</h2>
        <h3>{problem.headline}</h3>
        <p>{problem.body}</p>
      </div>
      <ProblemVisual problem={problem} />
    </section>
  );
}

export default function Home() {
  return (
    <Layout
      title="Agent-Up"
      description="Workspace management for AI-assisted software development">
      <main className={styles.page}>
        <section className={styles.hero}>
          <div className={styles.heroText}>
            <p className={styles.eyebrow}>AI-assisted development workspaces</p>
            <h1>See what your AI built. Instantly.</h1>
            <p className={styles.lede}>
              Every AI gets its own workspace.
              No tabs. No ports. No setup. Just review.
            </p>
            <div className={styles.actions}>
              <Link className={styles.primaryAction} to="/docs/">
                Read the docs
              </Link>
              <Link className={styles.secondaryAction} to="/developer-guide/architecture">
                See architecture
              </Link>
            </div>
          </div>
          <HeroMockup />
        </section>


        <section id="problems" className={styles.problemIntro}>
          <p className={styles.eyebrow}>What Agent-Up solves</p>
          <h2>Parallel AI work turns local development into distributed operations.</h2>
          <p>
            Every agent brings another branch, runtime, browser session, port range, and validation loop.
            Agent-Up manages that surrounding workspace layer so your applications stay unchanged and your review
            process stays focused on what changed.
          </p>
        </section>

        <section className={styles.problemSections}>
          {problems.map((problem, index) => (
            <ProblemSection key={problem.title} problem={problem} index={index} />
          ))}
        </section>

        <section className={styles.operatingModel}>
          <div>
            <p className={styles.eyebrow}>Operating model</p>
            <h2>The Server owns orchestration. Every other surface stays thin.</h2>
            <p>
              Desktop, CLI, and MCP clients all connect to the same Server. That keeps humans and AI agents aligned
              around one authoritative view of every running workspace.
            </p>
          </div>
          <ul>
            {capabilities.map((capability) => (
              <li key={capability}>{capability}</li>
            ))}
          </ul>
        </section>

        <section id="workflow" className={styles.workflow}>
          <p className={styles.eyebrow}>Validation loop</p>
          <h2>From code change to browser evidence without a separate automation harness.</h2>
          <div className={styles.steps}>
            <span>Modify code</span>
            <span>Restart workspace</span>
            <span>Inspect page</span>
            <span>Interact</span>
            <span>Capture diagnostics</span>
            <span>Generate Playwright</span>
          </div>
        </section>
      </main>
    </Layout>
  );
}
