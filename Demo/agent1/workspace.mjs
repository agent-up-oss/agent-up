export const apps = {
  MarketingSite: {
    workspace: 'SaaS-agent1 / feat-login',
    name: 'MarketingSite',
    portVariable: 'WEB_PORT',
    defaultPort: 8080,
    routes: [
      {
        path: '/',
        label: 'Home',
        heading: 'Launch your team portal faster.',
        body: 'A polished workspace for customers, analytics, billing, and team operations.',
        actions: ['Start free', 'View demo'],
        cards: [
          { title: 'Analytics', detail: 'Live growth signals' },
          { title: 'Billing', detail: 'Plans and invoices' },
          { title: 'Teams', detail: 'Roles and access' }
        ]
      },
      {
        path: '/docs',
        label: 'Docs',
        heading: 'Documentation',
        body: 'Implementation notes for getting started, installation, configuration, API usage, and deployment.',
        cards: [
          { title: 'Getting started', detail: 'Project setup checklist' },
          { title: 'Configuration', detail: 'Environment and ports' },
          { title: 'API reference', detail: 'Auth and user endpoints' }
        ]
      },
      {
        path: '/login',
        label: 'Login',
        heading: 'Sign in to continue',
        body: 'Shared browser state lets a human and an agent validate authentication in the same workspace profile.',
        actions: ['Use demo account', 'Continue with SSO'],
        cards: [
          { title: 'alice@example.com', detail: 'Admin user' },
          { title: 'Session', detail: 'Cookie-backed workspace state' }
        ]
      },
      {
        path: '/features',
        label: 'Features',
        heading: 'Everything needed for team operations.',
        body: 'The login branch adds clearer onboarding, session status, and team-access messaging.',
        cards: [
          { title: 'Role-based access', detail: 'Admin and member permissions' },
          { title: 'Audit trail', detail: 'Recent sign-in and invite events' },
          { title: 'Workspace health', detail: 'All systems ready' }
        ]
      }
    ]
  },
  Dashboard: {
    workspace: 'SaaS-agent1 / feat-login',
    name: 'Dashboard',
    portVariable: 'DASHBOARD_PORT',
    defaultPort: 3000,
    routes: [
      {
        path: '/dashboard',
        label: 'Overview',
        heading: 'Workspace overview',
        body: 'Core SaaS metrics for the login branch.',
        cards: [
          { title: '2,847 users', detail: '94% retention' },
          { title: '$12.4k revenue', detail: 'Month to date' },
          { title: '3 trial accounts', detail: 'Need onboarding review' }
        ]
      },
      {
        path: '/dashboard/users',
        label: 'Users',
        heading: 'User directory',
        body: 'A focused user table for validating auth, roles, and session state.',
        columns: ['Name', 'Plan', 'Role', 'Status'],
        rows: [
          ['Alice Chen', 'Pro', 'Admin', 'active'],
          ['Bob Smith', 'Free', 'Member', 'active'],
          ['Carol Wu', 'Team', 'Member', 'trial']
        ]
      },
      {
        path: '/dashboard/analytics',
        label: 'Analytics',
        heading: 'Signups by month',
        body: 'The login branch should not regress acquisition or retention reporting.',
        cards: [
          { title: 'January', detail: '124 signups' },
          { title: 'February', detail: '139 signups' },
          { title: 'March', detail: '181 signups' }
        ]
      },
      {
        path: '/dashboard/settings',
        label: 'Settings',
        heading: 'Authentication settings',
        body: 'Validate account, session, and domain preferences.',
        actions: ['Save changes', 'Rotate session'],
        cards: [
          { title: 'App name', detail: 'myapp' },
          { title: 'Domain', detail: 'myapp.com' },
          { title: 'Plan', detail: 'Free' }
        ]
      }
    ]
  },
  Backend: {
    workspace: 'SaaS-agent1 / feat-login',
    name: 'Backend',
    portVariable: 'API_PORT',
    defaultPort: 3001,
    routes: [
      {
        path: '/openapi',
        label: 'OpenAPI',
        heading: 'Users and Auth API',
        body: 'OpenAPI-style endpoint surface for login branch validation.',
        columns: ['Method', 'Path', 'Description'],
        rows: [
          ['POST', '/api/auth/login', 'Log in'],
          ['POST', '/api/auth/logout', 'Log out'],
          ['GET', '/api/auth/me', 'Current user'],
          ['GET', '/api/users', 'List users']
        ]
      },
      {
        path: '/api/auth/me',
        label: 'Current user',
        heading: 'Current user JSON',
        body: 'Human-readable wrapper for current-user state.',
        json: { id: 1, email: 'alice@example.com', role: 'admin', authenticated: true }
      },
      {
        path: '/api/users',
        label: 'Users JSON',
        heading: 'Users JSON',
        body: 'Human-readable wrapper for users API state.',
        json: { users: ['Alice Chen', 'Bob Smith', 'Carol Wu'], total: 3 }
      },
      {
        path: '/health',
        label: 'Health',
        heading: 'Backend healthy',
        body: 'Auth API, session store, and database connection are available.',
        cards: [
          { title: 'API', detail: 'ready' },
          { title: 'Sessions', detail: 'ready' },
          { title: 'Database', detail: 'ready' }
        ]
      }
    ]
  },
  Postgres: {
    workspace: 'SaaS-agent1 / feat-login',
    name: 'Postgres',
    portVariable: 'DB_PORT',
    defaultPort: 5432,
    routes: [
      {
        path: '/',
        label: 'Tables',
        heading: 'Postgres inspector',
        body: 'Mock database browser for SaaS auth and team data.',
        cards: [
          { title: 'users', detail: '3 rows' },
          { title: 'sessions', detail: '2 rows' },
          { title: 'orders', detail: '3 rows' }
        ]
      },
      {
        path: '/tables/users',
        label: 'Users',
        heading: 'users table',
        body: 'User and role data used by the dashboard.',
        columns: ['id', 'email', 'role', 'created_at'],
        rows: [
          ['1', 'alice@example.com', 'admin', '2024-01-10'],
          ['2', 'bob@example.com', 'user', '2024-01-15'],
          ['3', 'carol@example.com', 'user', '2024-02-01']
        ]
      },
      {
        path: '/tables/sessions',
        label: 'Sessions',
        heading: 'sessions table',
        body: 'Session rows for the login validation branch.',
        columns: ['id', 'user_id', 'ip', 'expires_at'],
        rows: [
          ['1', '1', '192.168.1.1', '2026-08-01'],
          ['2', '2', '192.168.1.4', '2026-08-02']
        ]
      },
      {
        path: '/tables/orders',
        label: 'Orders',
        heading: 'orders table',
        body: 'Small order set used by dashboard cards.',
        columns: ['id', 'user_id', 'total', 'status'],
        rows: [
          ['1001', '2', '$29.99', 'completed'],
          ['1002', '3', '$89.99', 'shipped'],
          ['1003', '2', '$9.99', 'processing']
        ]
      }
    ]
  }
};
