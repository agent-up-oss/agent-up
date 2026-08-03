export const apps = {
  MarketingSite: {
    workspace: 'SaaS-agent2 / feat-pricing',
    name: 'MarketingSite',
    portVariable: 'WEB_PORT',
    defaultPort: 5200,
    routes: [
      {
        path: '/',
        label: 'Home',
        heading: 'Ship faster with clear SaaS pricing',
        body: 'Built for the AI era. Deploy in minutes with pricing that is easy to evaluate.',
        actions: ['Start free trial'],
        cards: [
          { title: 'Analytics', detail: 'Live growth signals' },
          { title: 'Billing', detail: 'Plans and invoices' }
        ]
      },
      {
        path: '/pricing',
        label: 'Pricing',
        heading: 'Simple pricing',
        body: 'The pricing branch highlights Pro and clarifies plan limits.',
        cards: [
          { title: 'Free', detail: '$0 / month - 5 projects' },
          { title: 'Pro', detail: '$29 / month - 25 projects' },
          { title: 'Team', detail: '$99 / month - unlimited projects' }
        ],
        actions: ['Start Pro', 'Contact sales']
      },
      {
        path: '/docs',
        label: 'Docs',
        heading: 'Documentation',
        body: 'Pricing and billing docs for evaluating rollout behavior.',
        cards: [
          { title: 'Plan limits', detail: 'Projects, seats, and storage' },
          { title: 'Billing events', detail: 'Invoices and subscriptions' },
          { title: 'Migration guide', detail: 'Move from legacy plans' }
        ]
      },
      {
        path: '/compare',
        label: 'Compare',
        heading: 'Compare plans',
        body: 'A compact comparison table for pricing-page validation.',
        columns: ['Feature', 'Free', 'Pro', 'Team'],
        rows: [
          ['Projects', '5', '25', 'Unlimited'],
          ['Storage', '1 GB', '10 GB', '50 GB'],
          ['Support', 'Basic', 'Priority', 'Dedicated']
        ]
      }
    ]
  },
  Dashboard: {
    workspace: 'SaaS-agent2 / feat-pricing',
    name: 'Dashboard',
    portVariable: 'DASHBOARD_PORT',
    defaultPort: 5201,
    routes: [
      {
        path: '/dashboard',
        label: 'Overview',
        heading: 'Pricing branch overview',
        body: 'Updated SaaS metrics after pricing copy changes.',
        cards: [
          { title: '2,851 users', detail: '96% retention' },
          { title: '$12.7k revenue', detail: 'Month to date' },
          { title: '4 Pro upgrades', detail: 'Last 24 hours' }
        ]
      },
      {
        path: '/dashboard/analytics',
        label: 'Analytics',
        heading: 'Conversion analytics',
        body: 'Pricing-page experiments should improve Pro conversion.',
        cards: [
          { title: 'Pricing visits', detail: '1,842' },
          { title: 'Trial starts', detail: '184' },
          { title: 'Upgrade rate', detail: '12.8%' }
        ]
      },
      {
        path: '/dashboard/settings',
        label: 'Settings',
        heading: 'Plan settings',
        body: 'Settings reflect a Pro workspace after the pricing branch update.',
        actions: ['Save changes'],
        cards: [
          { title: 'App name', detail: 'myapp' },
          { title: 'Domain', detail: 'myapp.com' },
          { title: 'Plan', detail: 'Pro' }
        ]
      },
      {
        path: '/dashboard/billing',
        label: 'Billing',
        heading: 'Billing activity',
        body: 'Invoices, subscriptions, and plan changes for validation.',
        columns: ['Invoice', 'Customer', 'Amount', 'Status'],
        rows: [
          ['INV-2041', 'Alice Chen', '$29.00', 'paid'],
          ['INV-2042', 'Dave Lee', '$99.00', 'open'],
          ['INV-2043', 'Carol Wu', '$29.00', 'paid']
        ]
      }
    ]
  },
  Worker: {
    workspace: 'SaaS-agent2 / feat-pricing',
    name: 'Worker',
    portVariable: 'WORKER_PORT',
    defaultPort: 5202,
    routes: [
      {
        path: '/',
        label: 'Status',
        heading: 'Worker started with concurrency 4',
        body: 'Background jobs process subscription updates and pricing notifications.',
        cards: [
          { title: 'Concurrency', detail: '4 workers' },
          { title: 'Avg job', detail: '201ms' },
          { title: 'Failures', detail: '0' }
        ]
      },
      {
        path: '/jobs',
        label: 'Jobs',
        heading: 'Recent jobs',
        body: 'Pricing and invoice jobs moving through the queue.',
        columns: ['Job', 'Type', 'Duration', 'Status'],
        rows: [
          ['1044', 'invoice.created', '201ms', 'done'],
          ['1045', 'plan.changed', '234ms', 'done'],
          ['1046', 'trial.expiring', 'queued', 'pending']
        ]
      },
      {
        path: '/metrics',
        label: 'Metrics',
        heading: 'Worker metrics',
        body: 'Operational metrics exposed as an HTTP page for the demo.',
        cards: [
          { title: 'Jobs/min', detail: '1.1k' },
          { title: 'Uptime', detail: '99.6%' },
          { title: 'Dead letters', detail: '0' }
        ]
      },
      {
        path: '/queue',
        label: 'Queue',
        heading: 'Queue depth',
        body: 'Queue state for background pricing workflows.',
        cards: [
          { title: 'billing', detail: '2 pending' },
          { title: 'emails', detail: '7 pending' },
          { title: 'exports', detail: '0 pending' }
        ]
      }
    ]
  },
  Postgres: {
    workspace: 'SaaS-agent2 / feat-pricing',
    name: 'Postgres',
    portVariable: 'DB_PORT',
    defaultPort: 5203,
    routes: [
      {
        path: '/',
        label: 'Tables',
        heading: 'Postgres inspector',
        body: 'Mock database browser for pricing branch data.',
        cards: [
          { title: 'products', detail: '3 rows' },
          { title: 'price_tiers', detail: '3 rows' },
          { title: 'orders', detail: '4 rows' }
        ]
      },
      {
        path: '/tables/products',
        label: 'Products',
        heading: 'products table',
        body: 'Product rows with updated prices.',
        columns: ['id', 'name', 'price', 'stock'],
        rows: [
          ['1', 'Widget Pro', '$34.99', '143'],
          ['2', 'Widget Lite', '$9.99', '512'],
          ['3', 'Widget Max', '$99.99', '28']
        ]
      },
      {
        path: '/tables/price_tiers',
        label: 'Price tiers',
        heading: 'price_tiers table',
        body: 'Plan data rendered by the pricing page.',
        columns: ['id', 'name', 'monthly_usd', 'description'],
        rows: [
          ['1', 'Free', '0', 'Up to 5 projects'],
          ['2', 'Pro', '29', 'Up to 25 projects'],
          ['3', 'Team', '99', 'Unlimited projects']
        ]
      },
      {
        path: '/tables/orders',
        label: 'Orders',
        heading: 'orders table',
        body: 'Recent order state after pricing changes.',
        columns: ['id', 'email', 'total', 'status'],
        rows: [
          ['1001', 'bob@example.com', '$34.99', 'completed'],
          ['1002', 'carol@example.com', '$99.99', 'shipped'],
          ['1003', 'bob@example.com', '$9.99', 'processing'],
          ['1004', 'dave@example.com', '$34.99', 'pending']
        ]
      }
    ]
  }
};
