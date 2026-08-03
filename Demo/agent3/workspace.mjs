export const apps = {
  Storefront: {
    workspace: 'online-shop-agent1 / main',
    name: 'Storefront',
    portVariable: 'STOREFRONT_PORT',
    defaultPort: 5300,
    routes: [
      {
        path: '/',
        label: 'Home',
        heading: 'New arrivals',
        body: 'Spring collection is here for thoughtfully designed goods.',
        actions: ['Shop now'],
        cards: [
          { title: 'Merino Tee', detail: '$34 - New' },
          { title: 'Field Jacket', detail: '$129 - Sale' },
          { title: 'Canvas Bag', detail: '$58' }
        ]
      },
      {
        path: '/products',
        label: 'Products',
        heading: 'Product catalog',
        body: 'A browsable product grid for storefront validation.',
        cards: [
          { title: 'Merino Tee', detail: '$34 - 45 in stock' },
          { title: 'Field Jacket', detail: '$129 - 12 in stock' },
          { title: 'Canvas Bag', detail: '$58 - 87 in stock' },
          { title: 'Leather Belt', detail: '$44 - 31 in stock' },
          { title: 'Wool Cap', detail: '$22 - New' },
          { title: 'Trail Shorts', detail: '$68' }
        ]
      },
      {
        path: '/about',
        label: 'About',
        heading: 'Our story',
        body: 'shopcraft makes thoughtfully designed goods for everyday life with small-batch manufacturers.',
        cards: [
          { title: '6+ years', detail: 'Founded in 2018' },
          { title: '40k customers', detail: 'Across core markets' },
          { title: '200+ products', detail: 'Everyday goods' }
        ]
      },
      {
        path: '/cart',
        label: 'Cart',
        heading: 'Cart is empty',
        body: 'Cart state is intentionally simple for browser automation and screenshot capture.',
        actions: ['Continue shopping', 'Apply promo code']
      }
    ]
  },
  AdminPanel: {
    workspace: 'online-shop-agent1 / main',
    name: 'AdminPanel',
    portVariable: 'ADMIN_PORT',
    defaultPort: 5301,
    routes: [
      {
        path: '/admin/orders',
        label: 'Orders',
        heading: 'Recent orders',
        body: 'Order state for admin validation.',
        columns: ['Order', 'Customer', 'Amount', 'Status'],
        rows: [
          ['#1042', 'Alice Chen', '$129.00', 'shipped'],
          ['#1041', 'Bob Smith', '$34.00', 'delivered'],
          ['#1040', 'Carol Wu', '$187.00', 'pending']
        ]
      },
      {
        path: '/admin/inventory',
        label: 'Inventory',
        heading: 'Inventory',
        body: 'Stock status and low-inventory signals.',
        columns: ['SKU', 'Name', 'Qty', 'Status'],
        rows: [
          ['MT-001', 'Merino Tee', '45', 'in stock'],
          ['FJ-002', 'Field Jacket', '12', 'low'],
          ['CB-003', 'Canvas Bag', '87', 'in stock'],
          ['LB-004', 'Leather Belt', '0', 'out']
        ]
      },
      {
        path: '/admin/customers',
        label: 'Customers',
        heading: 'Customer tiers',
        body: 'Customer profiles used by order and payment workflows.',
        columns: ['Name', 'Email', 'Tier', 'Orders'],
        rows: [
          ['Alice Chen', 'alice@shopcraft.com', 'gold', '14'],
          ['Bob Smith', 'bob@email.com', 'silver', '3'],
          ['Carol Wu', 'carol@email.com', 'gold', '8']
        ]
      }
    ]
  },
  Payments: {
    workspace: 'online-shop-agent1 / main',
    name: 'Payments',
    portVariable: 'PAYMENTS_PORT',
    defaultPort: 5302,
    routes: [
      {
        path: '/openapi',
        label: 'OpenAPI',
        heading: 'Payments API',
        body: 'OpenAPI-style endpoint surface for payment workflows.',
        columns: ['Method', 'Path', 'Description'],
        rows: [
          ['POST', '/charges', 'Create charge'],
          ['GET', '/charges/{id}', 'Get charge'],
          ['POST', '/refunds', 'Issue refund'],
          ['GET', '/subscriptions', 'List subscriptions'],
          ['POST', '/subscriptions', 'Create subscription']
        ]
      },
      {
        path: '/charges',
        label: 'Charges',
        heading: 'Charges',
        body: 'Recent payment charge activity.',
        columns: ['Charge', 'Order', 'Amount', 'Status'],
        rows: [
          ['ch_5001', 'ORD-1042', '$129.00', 'captured'],
          ['ch_5002', 'ORD-1041', '$34.00', 'captured'],
          ['ch_5003', 'ORD-1040', '$187.00', 'authorized']
        ]
      },
      {
        path: '/subscriptions',
        label: 'Subscriptions',
        heading: 'Subscriptions',
        body: 'Subscription rows for payment API inspection.',
        cards: [
          { title: 'Active', detail: '12' },
          { title: 'Trialing', detail: '3' },
          { title: 'Past due', detail: '0' }
        ]
      },
      {
        path: '/health',
        label: 'Health',
        heading: 'Payments healthy',
        body: 'Stripe webhook, charge capture, and subscriptions are ready.',
        cards: [
          { title: 'Latency', detail: '124ms' },
          { title: 'Uptime', detail: '99.7%' },
          { title: 'Errors', detail: '0' }
        ]
      }
    ]
  },
  Postgres: {
    workspace: 'online-shop-agent1 / main',
    name: 'Postgres',
    portVariable: 'DB_PORT',
    defaultPort: 5303,
    routes: [
      {
        path: '/',
        label: 'Tables',
        heading: 'Shop database',
        body: 'Mock database browser for shopcraft data.',
        cards: [
          { title: 'inventory', detail: '4 rows' },
          { title: 'transactions', detail: '4 rows' },
          { title: 'customers', detail: '3 rows' }
        ]
      },
      {
        path: '/tables/inventory',
        label: 'Inventory',
        heading: 'inventory table',
        body: 'Product and stock data.',
        columns: ['id', 'sku', 'name', 'qty', 'price'],
        rows: [
          ['1', 'MT-001', 'Merino Tee', '45', '$34.00'],
          ['2', 'FJ-002', 'Field Jacket', '12', '$129.00'],
          ['3', 'CB-003', 'Canvas Bag', '87', '$58.00'],
          ['4', 'LB-004', 'Leather Belt', '31', '$44.00']
        ]
      },
      {
        path: '/tables/transactions',
        label: 'Transactions',
        heading: 'transactions table',
        body: 'Payment transaction state.',
        columns: ['id', 'order_ref', 'amount', 'status', 'method'],
        rows: [
          ['5001', 'ORD-1042', '$129.00', 'completed', 'card'],
          ['5002', 'ORD-1041', '$34.00', 'completed', 'paypal'],
          ['5003', 'ORD-1040', '$187.00', 'pending', 'card'],
          ['5004', 'ORD-1039', '$58.00', 'refunded', 'card']
        ]
      },
      {
        path: '/tables/customers',
        label: 'Customers',
        heading: 'customers table',
        body: 'Customer tiers and order counts.',
        columns: ['id', 'name', 'email', 'tier', 'orders'],
        rows: [
          ['1', 'Alice Chen', 'alice@shopcraft.com', 'gold', '14'],
          ['2', 'Bob Smith', 'bob@email.com', 'silver', '3'],
          ['3', 'Carol Wu', 'carol@email.com', 'gold', '8']
        ]
      }
    ]
  }
};
