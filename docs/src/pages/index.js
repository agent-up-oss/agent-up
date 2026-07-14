import React, { useState } from 'react';
import Layout from '@theme/Layout';
import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
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
    details: ['agent-1', 'agent-2', 'agent-3'],
  },
  {
    label: 'Tab overload',
    title: 'Browser tab explosion',
    headline: 'The browser becomes workspace state, not loose debris.',
    body: 'A workspace has one isolated browser profile and stable application tabs. Restarting apps reloads the active session instead of scattering more tabs across your browser.',
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
    body: 'The intended workflow is for agents to restart a workspace, inspect the shared browser, interact through MCP, retrieve diagnostics, capture screenshots, and generate Playwright tests from recorded intent.',
    layout: 'validation',
    details: ['Inspect', 'Interact', 'Screenshot', 'Playwright'],
  },
];

const capabilities = [
  'Server-owned runtime state',
  'Per-workspace port ranges',
  'Isolated browser profiles',
  'MCP-first automation (in progress)',
  'Event-backed diagnostics (planned)',
  'Playwright generation (planned)',
];

// ── Projects ──────────────────────────────────────────────────────────────────

const projects = [
  { id: 'A', name: 'Project A', agents: ['agent-1', 'agent-2'] },
  { id: 'B', name: 'Project B', agents: ['agent-3'] },
];

const agentServiceConfig = {
  'agent-1': {
    services: {
      MarketingSite: { secondaryTabs: ['Port 8080', 'Console', 'Metrics'] },
      Dashboard:     { secondaryTabs: ['Port 3000', 'Console', 'Metrics'] },
      Backend:       { secondaryTabs: ['OpenAPI', 'Console', 'Metrics'] },
      Postgres:      { secondaryTabs: ['Console', 'Metrics'] },
    },
    defaultService: 'MarketingSite',
  },
  'agent-2': {
    services: {
      MarketingSite: { secondaryTabs: ['Port 8080', 'Console', 'Metrics'] },
      Dashboard:     { secondaryTabs: ['Port 3000', 'Console', 'Metrics'] },
      Worker:        { secondaryTabs: ['Console', 'Metrics'] },
      Postgres:      { secondaryTabs: ['Console', 'Metrics'] },
    },
    defaultService: 'MarketingSite',
  },
  'agent-3': {
    services: {
      Storefront: { secondaryTabs: ['Port 5000', 'Console', 'Metrics'] },
      AdminPanel: { secondaryTabs: ['Port 5001', 'Console', 'Metrics'] },
      Payments:   { secondaryTabs: ['OpenAPI', 'Console', 'Metrics'] },
      Postgres:   { secondaryTabs: ['Console', 'Metrics'] },
    },
    defaultService: 'Storefront',
  },
};

const getProjectForAgent = (id) => projects.find((p) => p.agents.includes(id));
const getInitials = (id) => {
  const p = getProjectForAgent(id);
  return `${p.id}${p.agents.indexOf(id) + 1}`;
};

const agentMeta = {
  'agent-1': { branch: 'feat/user-auth',     path: '/workspaces/project-a' },
  'agent-2': { branch: 'feat/pricing-page',  path: '/workspaces/project-a-2' },
  'agent-3': { branch: 'feat/inventory',     path: '/workspaces/shopcraft' },
};

// page path lookup for URL bar
const pageUrlPaths = {
  A: {
    MarketingSite: { home: '/', pricing: '/pricing', docs: '/docs' },
    Dashboard: { overview: '/dashboard', users: '/dashboard/users', analytics: '/dashboard/analytics', settings: '/dashboard/settings' },
  },
  B: {
    Storefront: { home: '/', products: '/products', about: '/about' },
    AdminPanel: { orders: '/admin/orders', inventory: '/admin/inventory' },
  },
};

const deriveUrl = (projectId, service, tab, page) => {
  if (!tab.startsWith('Port')) return null;
  const port = tab.split(' ')[1];
  const path = pageUrlPaths[projectId]?.[service]?.[page] ?? '/';
  return `localhost:${port}${path}`;
};

// ── Per-agent/service data ────────────────────────────────────────────────────

const consoleLookup = {
  'MarketingSite/agent-1': [
    ['$', 'npm run dev'],
    ['▸', 'vite v5.4.2 ready on http://localhost:8080'],
    ['▸', 'HMR active'],
    ['›', 'GET / 200 4ms'],
    ['›', 'GET /assets/index.js 200 2ms'],
  ],
  'MarketingSite/agent-2': [
    ['$', 'npm run dev'],
    ['▸', 'vite v5.4.2 ready on http://localhost:8080'],
    ['!', 'warn: @types/react-dom deprecated'],
    ['›', 'GET / 200 4ms'],
    ['›', 'GET /pricing 200 3ms'],
  ],
  'Dashboard/agent-1': [
    ['$', 'npm run dev'],
    ['▸', 'Next.js 14 ready on http://localhost:3000'],
    ['▸', 'HMR active'],
    ['›', 'GET /dashboard 200 8ms'],
    ['›', 'GET /api/stats 200 12ms'],
  ],
  'Dashboard/agent-2': [
    ['$', 'npm run dev'],
    ['▸', 'Next.js 14 ready on http://localhost:3000'],
    ['›', 'GET /dashboard/users 200 12ms'],
    ['›', 'GET /dashboard/analytics 200 15ms'],
    ['›', 'GET /api/users?page=1 200 9ms'],
  ],
  'Backend/agent-1': [
    ['$', 'cargo run'],
    ['▸', 'Listening on 0.0.0.0:3001'],
    ['›', 'POST /api/auth/login 200 18ms'],
    ['›', 'GET /api/auth/me 200 3ms'],
    ['›', 'GET /api/users 200 7ms'],
  ],
  'Worker/agent-1': [
    ['$', 'node worker.js'],
    ['▸', 'Worker started, polling queue...'],
    ['›', 'Job #1042 picked up'],
    ['›', 'Job #1042 done 342ms'],
    ['›', 'Job #1043 picked up'],
  ],
  'Worker/agent-2': [
    ['$', 'node worker.js --concurrency 4'],
    ['▸', 'Worker started concurrency=4'],
    ['›', 'Job #1044 (worker 1)'],
    ['›', 'Job #1045 (worker 2)'],
    ['›', 'Job #1044 done 201ms'],
  ],
  'Postgres/agent-1': [
    ['$', 'psql -U postgres -d appdb'],
    ['▸', 'Connected to PostgreSQL 16.2'],
    ['›', 'SELECT COUNT(*) FROM users; → 3'],
    ['›', 'SELECT COUNT(*) FROM orders; → 3'],
    ['›', 'appdb=#'],
  ],
  'Postgres/agent-2': [
    ['$', 'psql -U postgres -d appdb'],
    ['▸', 'Connected to PostgreSQL 16.2'],
    ['›', 'SELECT COUNT(*) FROM products; → 3'],
    ['›', 'SELECT COUNT(*) FROM orders; → 4'],
    ['›', 'appdb=#'],
  ],
  'Postgres/agent-3': [
    ['$', 'psql -U postgres -d shopdb'],
    ['▸', 'Connected to PostgreSQL 16.2'],
    ['›', 'SELECT COUNT(*) FROM customers; → 3'],
    ['›', 'SELECT COUNT(*) FROM transactions; → 4'],
    ['›', 'shopdb=#'],
  ],
  Storefront: [
    ['$', 'npm run build && npm start'],
    ['▸', 'Next.js 15 ready on http://localhost:5000'],
    ['▸', 'Compiled 847 modules'],
    ['›', 'GET / 200 12ms'],
    ['›', 'GET /products 200 8ms'],
  ],
  AdminPanel: [
    ['$', 'python manage.py runserver 5001'],
    ['▸', 'Django 5.0 dev server at port 5001'],
    ['›', 'GET /admin/orders/ 200 5ms'],
    ['›', 'GET /admin/inventory/ 200 3ms'],
    ['›', 'PATCH /api/orders/1042/ 200 8ms'],
  ],
  Payments: [
    ['$', 'go run ./cmd/payments'],
    ['▸', 'Payments service ready on :8080'],
    ['▸', 'Stripe webhook active'],
    ['›', 'POST /charges 201 24ms'],
    ['›', 'POST /subscriptions 201 31ms'],
  ],
};

const metricsLookup = {
  'MarketingSite/agent-1': { values: ['142ms','99.2%','1.2k','0'], labels: ['Latency','Uptime','Req/min','Errors'], bars: [4,6,5,8,10,9,7,8,11,9,8,10] },
  'MarketingSite/agent-2': { values: ['98ms','99.8%','1.8k','0'], labels: ['Latency','Uptime','Req/min','Errors'], bars: [6,8,7,10,12,11,9,10,13,11,10,12] },
  'Dashboard/agent-1':     { values: ['54ms','99.5%','0.3k','0'], labels: ['Latency','Uptime','Req/min','Errors'], bars: [2,3,2,4,5,3,2,3,4,3,2,3] },
  'Dashboard/agent-2':     { values: ['49ms','99.9%','0.4k','0'], labels: ['Latency','Uptime','Req/min','Errors'], bars: [3,4,3,5,6,4,3,4,5,4,3,4] },
  'Backend/agent-1':       { values: ['38ms','99.9%','2.1k','0'], labels: ['Latency','Uptime','Req/min','Errors'], bars: [7,8,9,8,10,9,8,9,10,9,8,9] },
  'Backend/agent-2':       { values: ['31ms','99.9%','2.4k','0'], labels: ['Latency','Uptime','Req/min','Errors'], bars: [8,9,10,9,11,10,9,10,11,10,9,10] },
  'Worker/agent-1':        { values: ['342ms','99.1%','0.6k','1'], labels: ['Avg job','Uptime','Jobs/min','Failures'], bars: [4,5,4,6,7,5,4,5,6,5,4,5] },
  'Worker/agent-2':        { values: ['201ms','99.6%','1.1k','0'], labels: ['Avg job','Uptime','Jobs/min','Failures'], bars: [5,7,6,8,9,7,6,7,9,7,6,7] },
  'Postgres/agent-1':      { values: ['8ms','99.9%','0.4k','0'], labels: ['Avg query','Uptime','Queries/min','Errors'], bars: [3,4,3,4,5,3,3,4,4,3,3,4] },
  'Postgres/agent-2':      { values: ['7ms','99.9%','0.5k','0'], labels: ['Avg query','Uptime','Queries/min','Errors'], bars: [3,5,4,5,6,4,3,5,5,4,3,5] },
  'Postgres/agent-3':      { values: ['6ms','99.9%','0.8k','0'], labels: ['Avg query','Uptime','Queries/min','Errors'], bars: [4,5,4,5,6,4,4,5,5,4,4,5] },
  Storefront:  { values: ['220ms','98.7%','3.4k','2'], labels: ['Latency','Uptime','Req/min','Errors'], bars: [8,10,12,9,15,11,8,9,12,10,14,11] },
  AdminPanel:  { values: ['45ms','99.9%','0.1k','0'],  labels: ['Latency','Uptime','Req/min','Errors'], bars: [1,2,1,2,3,2,1,2,2,1,2,3] },
  Payments:    { values: ['124ms','99.7%','0.8k','0'], labels: ['Latency','Uptime','Req/min','Errors'], bars: [4,5,5,6,7,5,4,5,6,5,5,6] },
};

const openApiLookup = {
  'Backend/agent-1': { title: 'Users & Auth', endpoints: [
    { method: 'POST',   path: '/api/auth/login',   desc: 'Log in' },
    { method: 'POST',   path: '/api/auth/logout',  desc: 'Log out' },
    { method: 'GET',    path: '/api/auth/me',      desc: 'Current user' },
    { method: 'GET',    path: '/api/users',        desc: 'List users' },
    { method: 'POST',   path: '/api/users',        desc: 'Create user' },
    { method: 'GET',    path: '/api/users/{id}',   desc: 'Get user' },
    { method: 'PUT',    path: '/api/users/{id}',   desc: 'Update user' },
    { method: 'DELETE', path: '/api/users/{id}',   desc: 'Delete user' },
  ]},
  Payments: { title: 'Payments', endpoints: [
    { method: 'POST', path: '/charges',        desc: 'Create charge' },
    { method: 'GET',  path: '/charges/{id}',   desc: 'Get charge' },
    { method: 'POST', path: '/refunds',        desc: 'Issue refund' },
    { method: 'GET',  path: '/subscriptions',  desc: 'List subscriptions' },
    { method: 'POST', path: '/subscriptions',  desc: 'Create subscription' },
  ]},
};

const dbDataLookup = {
  'A/agent-1': {
    tableOrder: ['users', 'sessions', 'products', 'orders'],
    users:    { columns: ['id','email','role','created_at'], rows: [['1','alice@example.com','admin','2024-01-10'],['2','bob@example.com','user','2024-01-15'],['3','carol@example.com','user','2024-02-01']] },
    sessions: { columns: ['id','user_id','ip','expires_at'], rows: [['1','1','192.168.1.1','2024-03-01'],['2','2','192.168.1.4','2024-03-02']] },
    products: { columns: ['id','name','price','stock'], rows: [['1','Widget Pro','$29.99','143'],['2','Widget Lite','$9.99','512'],['3','Widget Max','$89.99','28']] },
    orders:   { columns: ['id','user_id','total','status'], rows: [['1001','2','$29.99','completed'],['1002','3','$89.99','shipped'],['1003','2','$9.99','processing']] },
  },
  'A/agent-2': {
    tableOrder: ['products', 'price_tiers', 'orders'],
    products:    { columns: ['id','name','price','stock'], rows: [['1','Widget Pro','$34.99','143'],['2','Widget Lite','$9.99','512'],['3','Widget Max','$99.99','28']] },
    price_tiers: { columns: ['id','name','monthly_usd','description'], rows: [['1','Free','0','Up to 5 projects'],['2','Pro','29','Up to 25 projects'],['3','Team','99','Unlimited projects']] },
    orders:      { columns: ['id','email','total','status'], rows: [['1001','bob@example.com','$34.99','completed'],['1002','carol@example.com','$99.99','shipped'],['1003','bob@example.com','$9.99','processing'],['1004','dave@example.com','$34.99','pending']] },
  },
  B: {
    tableOrder: ['inventory', 'transactions', 'customers', 'categories'],
    inventory:    { columns: ['id','sku','name','qty','price'], rows: [['1','MT-001','Merino Tee','45','$34.00'],['2','FJ-002','Field Jacket','12','$129.00'],['3','CB-003','Canvas Bag','87','$58.00'],['4','LB-004','Leather Belt','31','$44.00']] },
    transactions: { columns: ['id','order_ref','amount','status','method'], rows: [['5001','ORD-1042','$129.00','completed','card'],['5002','ORD-1041','$34.00','completed','paypal'],['5003','ORD-1040','$187.00','pending','card'],['5004','ORD-1039','$58.00','refunded','card']] },
    customers:    { columns: ['id','name','email','tier','orders'], rows: [['1','Alice Chen','alice@shopcraft.com','gold','14'],['2','Bob Smith','bob@email.com','silver','3'],['3','Carol Wu','carol@email.com','gold','8']] },
    categories:   { columns: ['id','name','slug','count'], rows: [['1','Clothing','clothing','24'],['2','Accessories','accessories','12'],['3','Footwear','footwear','8'],['4','Bags','bags','15']] },
  },
};

// ── Content components ────────────────────────────────────────────────────────

function WebsiteMockup({ agent, page, setPage }) {
  const v2 = agent === 'agent-2';

  const nav = (
    <div className={styles.mockupNavBar}>
      <span className={styles.mockupBrand}>myapp</span>
      <div className={styles.mockupNavLinks}>
        {(v2 ? ['Home', 'Pricing', 'Docs'] : ['Home', 'Docs']).map((label) => {
          const key = label.toLowerCase();
          const activePage = page === key || (label === 'Home' && (!page || page === 'home'));
          return (
            <button key={label} type="button"
              className={activePage ? styles.mockupNavLinkActive : styles.mockupNavLink}
              onClick={() => setPage(key === 'home' ? 'home' : key)}
            >{label}</button>
          );
        })}
      </div>
      <span className={styles.mockupNavCta}>{v2 ? 'Start free' : 'Sign up'}</span>
    </div>
  );

  if (page === 'pricing') {
    const tiers = [
      { name: 'Free', price: '$0', sub: 'per month', features: ['5 projects', '1 GB storage', 'Basic support'], cta: 'Get started', featured: false },
      { name: 'Pro', price: '$29', sub: 'per month', features: ['25 projects', '10 GB storage', 'Priority support'], cta: 'Start Pro', featured: true },
      { name: 'Team', price: '$99', sub: 'per month', features: ['Unlimited', '50 GB storage', 'Dedicated support'], cta: 'Contact us', featured: false },
    ];
    return (
      <div className={styles.websiteMockup}>
        {nav}
        <div className={styles.pricingPage}>
          <div className={styles.pricingHeadline}>Simple pricing</div>
          <div className={styles.pricingGrid}>
            {tiers.map((t) => (
              <div key={t.name} className={`${styles.pricingTier} ${t.featured && v2 ? styles.pricingFeatured : ''}`}>
                <div className={styles.pricingTierName}>{t.name}</div>
                <div className={styles.pricingPrice}>{t.price}<span>{t.sub}</span></div>
                <div className={styles.pricingFeatures}>
                  {t.features.map((f) => <div key={f} className={styles.pricingFeature}>{f}</div>)}
                </div>
                <span className={t.featured ? styles.mockupPrimaryBtn : styles.mockupSecondaryBtn}>{t.cta}</span>
              </div>
            ))}
          </div>
        </div>
      </div>
    );
  }

  if (page === 'docs') {
    const sections = ['Getting started', 'Installation', 'Configuration', 'API reference', 'Deployment guide'];
    return (
      <div className={styles.websiteMockup}>
        {nav}
        <div className={styles.docsPage}>
          <div className={styles.docsPageTitle}>Documentation</div>
          {sections.map((s) => (
            <div key={s} className={styles.docsItem}><span>{s}</span><span className={styles.docsArrow}>›</span></div>
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className={styles.websiteMockup}>
      {nav}
      <div className={`${styles.mockupHeroSection} ${v2 ? styles.mockupHeroV2 : ''}`}>
        <div className={styles.mockupHeadline}>{v2 ? 'Ship 10× faster with AI' : 'Build something great'}</div>
        <div className={styles.mockupSubline}>{v2 ? 'Built for the AI era. Deploy in minutes.' : 'A modern platform for modern teams.'}</div>
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

function DashboardMockup({ agent, page, setPage }) {
  const v2 = agent === 'agent-2';
  const view = page || 'overview';
  const navItems = [
    { key: 'overview',  label: 'Overview' },
    ...(!v2 ? [{ key: 'users', label: 'Users' }] : []),
    { key: 'analytics', label: 'Analytics' },
    { key: 'settings',  label: 'Settings' },
  ];
  const stats = v2
    ? [{ value: '2,851', label: 'Users' }, { value: '$12.7k', label: 'Revenue' }, { value: '96%', label: 'Retention' }]
    : [{ value: '2,847', label: 'Users' }, { value: '$12.4k', label: 'Revenue' }, { value: '94%', label: 'Retention' }];
  const users = [
    { name: 'Alice Chen', plan: 'Pro', status: 'active' },
    { name: 'Bob Smith', plan: 'Free', status: 'active' },
    { name: 'Carol Wu', plan: 'Team', status: 'trial' },
    ...(v2 ? [{ name: 'Dave Lee', plan: 'Pro', status: 'active' }] : []),
  ];
  const analyticsBars = v2 ? [3,5,4,7,8,6,5,7,9,8,7,9] : [2,4,3,6,7,5,4,6,8,7,6,8];

  const sidebar = (
    <div className={styles.dashboardSidebar}>
      {navItems.map((item) => (
        <button key={item.key} type="button"
          className={`${styles.dashboardNavItem} ${view === item.key ? styles.dashboardNavActive : ''}`}
          onClick={() => setPage(item.key)}
        >{item.label}</button>
      ))}
    </div>
  );

  if (view === 'users') return (
    <div className={styles.dashboardMockup}>
      {sidebar}
      <div className={styles.dashboardMain}>
        <div className={styles.dashboardTableFull}>
          <div className={styles.dashboardTableHeaderFull}><span>Name</span><span>Plan</span><span>Joined</span><span>Status</span></div>
          {users.map((u, i) => (
            <div key={u.name} className={styles.dashboardTableRowFull}>
              <span>{u.name}</span><span>{u.plan}</span>
              <span className={styles.dashMuted}>{`2024-0${i + 1}-10`}</span>
              <span className={u.status === 'active' ? styles.statusActive : styles.statusTrial}>{u.status}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );

  if (view === 'analytics') return (
    <div className={styles.dashboardMockup}>
      {sidebar}
      <div className={styles.dashboardMain}>
        <div className={styles.analyticsChart}>
          <div className={styles.analyticsTitle}>Signups / week</div>
          <div className={styles.analyticsBars}>
            {analyticsBars.map((h, i) => (
              <div key={i} className={styles.analyticsBar} style={{ height: `${h * 8}px` }} />
            ))}
          </div>
          <div className={styles.analyticsLabels}>
            {['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'].map((m) => (
              <span key={m}>{m}</span>
            ))}
          </div>
        </div>
      </div>
    </div>
  );

  if (view === 'settings') return (
    <div className={styles.dashboardMockup}>
      {sidebar}
      <div className={styles.dashboardMain}>
        <div className={styles.settingsForm}>
          <div className={styles.settingsRow}><label>App name</label><div className={styles.settingsInput}>myapp</div></div>
          <div className={styles.settingsRow}><label>Domain</label><div className={styles.settingsInput}>myapp.com</div></div>
          <div className={styles.settingsRow}><label>Plan</label><div className={styles.settingsInput}>{v2 ? 'Pro' : 'Free'}</div></div>
          <span className={styles.mockupPrimaryBtn} style={{ width: 'fit-content' }}>Save changes</span>
        </div>
      </div>
    </div>
  );

  return (
    <div className={styles.dashboardMockup}>
      {sidebar}
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
          <div className={styles.dashboardTableHeader}><span>Name</span><span>Plan</span><span>Status</span></div>
          {users.map((u) => (
            <div key={u.name} className={styles.dashboardTableRow}>
              <span>{u.name}</span><span>{u.plan}</span>
              <span className={u.status === 'active' ? styles.statusActive : styles.statusTrial}>{u.status}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function OpenApiMockup({ service, agent }) {
  const key = agent ? `${service}/${agent}` : service;
  const data = openApiLookup[key] ?? openApiLookup[service] ?? openApiLookup['Backend/agent-1'];
  return (
    <div className={styles.openApiMockup}>
      <div className={styles.apiTitle}>{data.title} <span>{data.endpoints.length} endpoints</span></div>
      {data.endpoints.map((ep) => (
        <div key={`${ep.method}-${ep.path}`} className={styles.apiEndpoint}>
          <span className={`${styles.methodBadge} ${styles[`method${ep.method}`]}`}>{ep.method}</span>
          <span className={styles.apiPath}>{ep.path}</span>
          <span className={styles.apiDesc}>{ep.desc}</span>
        </div>
      ))}
    </div>
  );
}

function DbConsoleMockup({ projectId, agent }) {
  const key = projectId === 'B' ? 'B' : `A/${agent}`;
  const data = dbDataLookup[key] ?? dbDataLookup['A/agent-1'];
  const [activeTable, setActiveTable] = useState(data.tableOrder[0]);
  const tableData = data[activeTable] ?? data[data.tableOrder[0]];

  return (
    <div className={styles.dbMockup}>
      <div className={styles.dbTableList}>
        <div className={styles.dbListHeader}>Tables</div>
        {data.tableOrder.map((t) => (
          <button key={t} type="button"
            className={`${styles.dbTableBtn} ${activeTable === t ? styles.dbTableActive : styles.dbTableItem}`}
            onClick={() => setActiveTable(t)}
          >{t}</button>
        ))}
      </div>
      <div className={styles.dbResultPanel}>
        <div className={styles.dbQueryBar}>SELECT * FROM {activeTable} LIMIT 10</div>
        <div className={styles.dbTableScroll}>
          <table className={styles.dbTable}>
            <thead>
              <tr>{tableData.columns.map((c) => <th key={c}>{c}</th>)}</tr>
            </thead>
            <tbody>
              {tableData.rows.map((row, ri) => (
                <tr key={ri}>{row.map((cell, ci) => <td key={ci}>{cell}</td>)}</tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

function StorefrontMockup({ page, setPage }) {
  const products = [
    { name: 'Merino Tee',   price: '$34',  tag: 'New' },
    { name: 'Field Jacket', price: '$129', tag: 'Sale' },
    { name: 'Canvas Bag',   price: '$58',  tag: null },
    { name: 'Leather Belt', price: '$44',  tag: null },
    { name: 'Wool Cap',     price: '$22',  tag: 'New' },
    { name: 'Trail Shorts', price: '$68',  tag: null },
  ];
  const displayed = page === 'products' ? products : products.slice(0, 3);

  const nav = (
    <div className={styles.storeNav}>
      <span className={styles.storeBrand}>shopcraft</span>
      <div className={styles.storeNavLinks}>
        {['Home', 'Products', 'About'].map((label) => {
          const key = label.toLowerCase();
          return (
            <button key={label} type="button"
              className={(page === key || (!page && key === 'home')) ? styles.storeNavActive : styles.storeNavLink}
              onClick={() => setPage(key)}
            >{label}</button>
          );
        })}
      </div>
      <span className={styles.storeCart}>Cart (0)</span>
    </div>
  );

  if (page === 'about') return (
    <div className={styles.storefrontMockup}>
      {nav}
      <div className={styles.storeAbout}>
        <div className={styles.storeAboutTitle}>Our story</div>
        <div className={styles.storeAboutText}>shopcraft makes thoughtfully designed goods for everyday life. Founded in 2018, we work with small-batch manufacturers to bring quality products at fair prices.</div>
        <div className={styles.storeAboutStats}>
          <div><strong>6+</strong><span>Years</span></div>
          <div><strong>40k</strong><span>Customers</span></div>
          <div><strong>200+</strong><span>Products</span></div>
        </div>
      </div>
    </div>
  );

  return (
    <div className={styles.storefrontMockup}>
      {nav}
      {page !== 'products' && (
        <div className={styles.storeBanner}>
          <div className={styles.storeBannerTitle}>New arrivals</div>
          <div className={styles.storeBannerSub}>Spring collection is here</div>
          <span className={styles.storeBannerCta} onClick={() => setPage('products')}>Shop now</span>
        </div>
      )}
      <div className={`${styles.storeGrid} ${page === 'products' ? styles.storeGridFull : ''}`}>
        {displayed.map((p) => (
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

function AdminPanelMockup({ page, setPage }) {
  const view = page || 'orders';
  const orders = [
    { id: '#1042', customer: 'Alice Chen', amount: '$129.00', status: 'shipped' },
    { id: '#1041', customer: 'Bob Smith',  amount: '$34.00',  status: 'delivered' },
    { id: '#1040', customer: 'Carol Wu',   amount: '$187.00', status: 'pending' },
  ];
  const inventory = [
    { sku: 'MT-001', name: 'Merino Tee',   qty: 45,  status: 'in stock' },
    { sku: 'FJ-002', name: 'Field Jacket', qty: 12,  status: 'low' },
    { sku: 'CB-003', name: 'Canvas Bag',   qty: 87,  status: 'in stock' },
    { sku: 'LB-004', name: 'Leather Belt', qty: 0,   status: 'out' },
  ];
  const statusClass = { shipped: styles.orderShipped, delivered: styles.orderDelivered, pending: styles.orderPending };
  const invClass = { 'in stock': styles.statusActive, low: styles.statusTrial, out: styles.orderPending };

  return (
    <div className={styles.adminMockup}>
      <div className={styles.adminHeader}>
        <button type="button" className={view === 'orders' ? styles.adminTabActive : styles.adminTab} onClick={() => setPage('orders')}>Orders</button>
        <button type="button" className={view === 'inventory' ? styles.adminTabActive : styles.adminTab} onClick={() => setPage('inventory')}>Inventory</button>
        <span className={styles.adminBadge}>{view === 'orders' ? `${orders.length} recent` : `${inventory.length} items`}</span>
      </div>
      {view === 'orders' ? (
        <div className={styles.adminTableWrap}>
          <div className={styles.adminTableHeader}><span>Order</span><span>Customer</span><span>Amount</span><span>Status</span></div>
          {orders.map((o) => (
            <div key={o.id} className={styles.adminTableRow}>
              <span className={styles.adminOrderId}>{o.id}</span>
              <span>{o.customer}</span><span>{o.amount}</span>
              <span className={statusClass[o.status]}>{o.status}</span>
            </div>
          ))}
        </div>
      ) : (
        <div className={styles.adminTableWrap}>
          <div className={styles.adminTableHeaderInv}><span>SKU</span><span>Name</span><span>Qty</span><span>Status</span></div>
          {inventory.map((i) => (
            <div key={i.sku} className={styles.adminTableRow}>
              <span className={styles.adminOrderId}>{i.sku}</span>
              <span>{i.name}</span><span>{i.qty}</span>
              <span className={invClass[i.status]}>{i.status}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function ConsoleMockup({ service, agent }) {
  const key = `${service}/${agent}`;
  const lines = consoleLookup[key] ?? consoleLookup[service] ?? [];
  return (
    <div className={styles.consoleMockup}>
      {lines.map(([prefix, text], i) => (
        <div key={i} className={prefix === '$' ? styles.consoleCmd : prefix === '!' ? styles.consoleWarn : styles.consoleLine}>
          <span className={styles.consolePrefix}>{prefix}</span>
          <span>{text}</span>
        </div>
      ))}
    </div>
  );
}

function MetricsMockup({ service, agent }) {
  const key = `${service}/${agent}`;
  const data = metricsLookup[key] ?? metricsLookup[service] ?? metricsLookup['MarketingSite/agent-1'];
  return (
    <div className={styles.metricsMockup}>
      <div className={styles.metricGrid}>
        {data.values.map((v, i) => (
          <div key={i} className={styles.metricCard}>
            <span className={styles.metricValue}>{v}</span>
            <span className={styles.metricLabel}>{data.labels[i]}</span>
          </div>
        ))}
      </div>
      <div className={styles.sparkline}>
        {data.bars.map((h, i) => <span key={i} style={{ height: `${h * 8}px` }} />)}
      </div>
    </div>
  );
}

function ContentPane({ projectId, service, tab, agent, page, setPage }) {
  if (projectId === 'A') {
    if (service === 'MarketingSite' && tab === 'Port 8080') return <WebsiteMockup agent={agent} page={page} setPage={setPage} />;
    if (service === 'Dashboard'     && tab === 'Port 3000') return <DashboardMockup agent={agent} page={page} setPage={setPage} />;
    if (service === 'Backend'       && tab === 'OpenAPI')   return <OpenApiMockup service="Backend" agent={agent} />;
    if (service === 'Postgres'      && tab === 'Console')   return <DbConsoleMockup key={agent} projectId="A" agent={agent} />;
  }
  if (projectId === 'B') {
    if (service === 'Storefront' && tab === 'Port 5000') return <StorefrontMockup page={page} setPage={setPage} />;
    if (service === 'AdminPanel' && tab === 'Port 5001') return <AdminPanelMockup page={page} setPage={setPage} />;
    if (service === 'Payments'   && tab === 'OpenAPI')   return <OpenApiMockup service="Payments" />;
    if (service === 'Postgres'   && tab === 'Console')   return <DbConsoleMockup key="B" projectId="B" agent={agent} />;
  }
  if (tab === 'Console') return <ConsoleMockup service={service} agent={agent} />;
  if (tab === 'Metrics') return <MetricsMockup service={service} agent={agent} />;
  return null;
}

// ── HeroMockup ────────────────────────────────────────────────────────────────

function HeroMockup() {
  const [collapsed, setCollapsed] = useState(false);
  const [activeAgent, setActiveAgent] = useState('agent-1');
  const [activeService, setActiveService] = useState('MarketingSite');
  const [activeTab, setActiveTab] = useState('Port 8080');
  const [activePage, setActivePage] = useState('home');

  const activeProject = getProjectForAgent(activeAgent);
  const activeAgentCfg = agentServiceConfig[activeAgent];
  const showUrlBar = activeTab.startsWith('Port');
  const currentUrl = deriveUrl(activeProject.id, activeService, activeTab, activePage);

  const handleAgentChange = (agentId) => {
    const newCfg = agentServiceConfig[agentId];
    const svc = newCfg.services[activeService] ? activeService : newCfg.defaultService;
    const tabs = newCfg.services[svc].secondaryTabs;
    setActiveAgent(agentId);
    setActiveService(svc);
    setActiveTab(tabs.includes(activeTab) ? activeTab : tabs[0]);
    setActivePage('home');
  };

  const handleServiceChange = (service) => {
    setActiveService(service);
    setActiveTab(activeAgentCfg.services[service].secondaryTabs[0]);
    setActivePage('home');
  };

  const handleTabChange = (tab) => {
    setActiveTab(tab);
    setActivePage('home');
  };

  const secondaryTabs = activeAgentCfg.services[activeService]?.secondaryTabs ?? [];

  return (
    <div
      className={`${styles.heroVisual} ${collapsed ? styles.heroVisualCollapsed : ''}`}
      aria-label="Agent-Up workspace topology"
    >
      <div className={styles.windowChrome}>
        <span /><span /><span />
      </div>
      <div className={styles.interactiveBadge}>
        <span className={styles.badgePulse} aria-hidden="true" />
        Live interactive demo
      </div>
      <div className={`${styles.workspaceRail} ${collapsed ? styles.workspaceRailCollapsed : ''}`}>
        {!collapsed && <strong>Workspaces</strong>}
        <div className={styles.agentList}>
          {projects.map((project, pi) =>
            collapsed ? (
              <React.Fragment key={project.id}>
                {pi > 0 && <div className={styles.projectDivider} />}
                {project.agents.map((agentId) => (
                  <button key={agentId} type="button"
                    className={`${styles.agentAvatar} ${activeAgent === agentId ? styles.agentAvatarActive : ''}`}
                    onClick={() => handleAgentChange(agentId)}
                    title={`${project.name} — ${agentId}`}
                  >{getInitials(agentId)}</button>
                ))}
              </React.Fragment>
            ) : (
              <div key={project.id} className={`${styles.projectSection} ${pi > 0 ? styles.projectSectionBorder : ''}`}>
                <div className={styles.projectLabel}>{project.name}</div>
                {project.agents.map((agentId) => (
                  <button key={agentId} type="button"
                    className={`${styles.agentEntry} ${activeAgent === agentId ? styles.agentActive : ''}`}
                    onClick={() => handleAgentChange(agentId)}
                  >
                    <span>{agentId}</span>
                    <span className={styles.agentBranch}>{agentMeta[agentId]?.branch}</span>
                    <span className={styles.agentPath}>{agentMeta[agentId]?.path}</span>
                  </button>
                ))}
              </div>
            )
          )}
        </div>
        <button type="button" className={styles.collapseToggle}
          onClick={() => setCollapsed(!collapsed)}
          title={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
        >{collapsed ? '›' : '‹'}</button>
      </div>
      <div className={styles.runtimePanel}>
        <div className={styles.serviceTabs}>
          {Object.keys(activeAgentCfg.services).map((service) => (
            <button key={service} type="button"
              className={`${styles.serviceTab} ${activeService === service ? styles.serviceTabActive : ''}`}
              onClick={() => handleServiceChange(service)}
            >{service}</button>
          ))}
        </div>
        <div className={styles.secondaryBar}>
          {secondaryTabs.map((tab) => (
            <button key={tab} type="button"
              className={`${styles.secondaryTab} ${activeTab === tab ? styles.secondaryTabActive : ''}`}
              onClick={() => handleTabChange(tab)}
            >{tab}</button>
          ))}
        </div>
        {showUrlBar && (
          <div className={styles.urlBar}>
            <span className={styles.urlLock}>🔒</span>
            <span className={styles.urlText}>{currentUrl}</span>
          </div>
        )}
        <div className={styles.contentPane}>
          <ContentPane
            projectId={activeProject.id}
            service={activeService}
            tab={activeTab}
            agent={activeAgent}
            page={activePage}
            setPage={setActivePage}
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
        <p className={styles.eyebrow}>{problem.label}</p>
        <h2>{problem.title}</h2>
        <h3>{problem.headline}</h3>
        <p>{problem.body}</p>
      </div>
      <ProblemVisual problem={problem} />
    </section>
  );
}

export default function Home() {
  const {siteConfig} = useDocusaurusContext();
  const artifactDownloadUrl = siteConfig.customFields?.artifactDownloadUrl;

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
              Every AI hosts its own workspace.<br/>
              No manual setup. Just review.
            </p>
            <div className={styles.actions}>
              <Link className={styles.primaryAction} to="/docs/">
                Run from source
              </Link>
              {artifactDownloadUrl && (
                <a className={styles.secondaryAction} href={artifactDownloadUrl}>
                  Download artifact
                </a>
              )}
            </div>
          </div>
          <div className={styles.heroVisualWrap}>
            <HeroMockup />
            <div className={styles.demoHint}>
              <span>Conceptual preview</span>
              <span>Agents, services, runtime tabs, and page controls show the intended workspace experience.</span>
            </div>
          </div>
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
