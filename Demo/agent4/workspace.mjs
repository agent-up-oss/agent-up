export const apps = {
  Storefront: {
    workspace: 'online-shop-agent2 / returns-ops',
    name: 'Storefront',
    portVariable: 'STOREFRONT_PORT',
    defaultPort: 5000,
    routes: [
      {
        path: '/',
        label: 'Home',
        heading: 'Every order has a second journey.',
        body: 'An altered shopcraft workspace focused on returns, support, and fulfillment operations.',
        actions: ['Start a return'],
        cards: [
          { title: 'Merino Tee', detail: '$34 - easy returns' },
          { title: 'Field Jacket', detail: '$129 - exchange eligible' },
          { title: 'Canvas Bag', detail: '$58 - support favorite' }
        ]
      },
      {
        path: '/products',
        label: 'Products',
        heading: 'Return-aware catalog',
        body: 'Product cards include fulfillment and return handling hints.',
        cards: [
          { title: 'Merino Tee', detail: '45 in stock - 30 day returns' },
          { title: 'Field Jacket', detail: '12 in stock - exchange recommended' },
          { title: 'Canvas Bag', detail: '87 in stock - low return rate' },
          { title: 'Leather Belt', detail: '31 in stock - final inspection' }
        ]
      },
      {
        path: '/returns',
        label: 'Returns',
        heading: 'Start a return',
        body: 'Customers can review order status, select items, and choose refund or exchange.',
        actions: ['Find order', 'Choose exchange', 'Print label'],
        cards: [
          { title: 'Eligible', detail: '18 orders' },
          { title: 'In transit', detail: '7 returns' },
          { title: 'Refund ready', detail: '3 returns' }
        ]
      },
      {
        path: '/support',
        label: 'Support',
        heading: 'Support center',
        body: 'Support content for sizing, return labels, and fulfillment exceptions.',
        cards: [
          { title: 'Return labels', detail: 'Download and reprint' },
          { title: 'Exchanges', detail: 'Choose a replacement size' },
          { title: 'Damaged item', detail: 'Escalate to support' }
        ]
      }
    ]
  },
  AdminPanel: {
    workspace: 'online-shop-agent2 / returns-ops',
    name: 'AdminPanel',
    portVariable: 'ADMIN_PORT',
    defaultPort: 5001,
    routes: [
      {
        path: '/admin/returns',
        label: 'Returns',
        heading: 'Return queue',
        body: 'Admin queue for return authorization and refund review.',
        columns: ['Return', 'Customer', 'Amount', 'Status'],
        rows: [
          ['RMA-701', 'Alice Chen', '$129.00', 'label sent'],
          ['RMA-702', 'Bob Smith', '$34.00', 'received'],
          ['RMA-703', 'Carol Wu', '$58.00', 'refund ready']
        ]
      },
      {
        path: '/admin/fulfillment',
        label: 'Fulfillment',
        heading: 'Fulfillment exceptions',
        body: 'Operational view for shipments, holds, and exchange picks.',
        columns: ['Order', 'Item', 'Location', 'Status'],
        rows: [
          ['#2042', 'Field Jacket', 'Aisle 3', 'hold'],
          ['#2043', 'Canvas Bag', 'Aisle 7', 'ready'],
          ['#2044', 'Merino Tee', 'Aisle 1', 'picked']
        ]
      },
      {
        path: '/admin/inventory',
        label: 'Inventory',
        heading: 'Return-adjusted inventory',
        body: 'Inventory counts after returns and exchanges.',
        columns: ['SKU', 'Name', 'Available', 'Returns'],
        rows: [
          ['MT-001', 'Merino Tee', '48', '3'],
          ['FJ-002', 'Field Jacket', '10', '2'],
          ['CB-003', 'Canvas Bag', '91', '4'],
          ['LB-004', 'Leather Belt', '29', '1']
        ]
      }
    ]
  },
  Fulfillment: {
    workspace: 'online-shop-agent2 / returns-ops',
    name: 'Fulfillment',
    portVariable: 'FULFILLMENT_PORT',
    defaultPort: 8080,
    routes: [
      {
        path: '/',
        label: 'Status',
        heading: 'Fulfillment service ready',
        body: 'Pick lists, return intake, and shipment status are available.',
        cards: [
          { title: 'Open shipments', detail: '18' },
          { title: 'Return intake', detail: '7 parcels' },
          { title: 'Avg pick time', detail: '11m' }
        ]
      },
      {
        path: '/shipments',
        label: 'Shipments',
        heading: 'Shipment tracker',
        body: 'Shipment state for outbound and exchange orders.',
        columns: ['Shipment', 'Carrier', 'Destination', 'Status'],
        rows: [
          ['SHP-9001', 'DHL', 'Berlin', 'in transit'],
          ['SHP-9002', 'UPS', 'Munich', 'label printed'],
          ['SHP-9003', 'DHL', 'Hamburg', 'delivered']
        ]
      },
      {
        path: '/pick-lists',
        label: 'Pick lists',
        heading: 'Pick lists',
        body: 'Warehouse pick lists for exchanges and delayed shipments.',
        columns: ['List', 'Items', 'Zone', 'Status'],
        rows: [
          ['PK-21', '8', 'A', 'ready'],
          ['PK-22', '3', 'B', 'picking'],
          ['PK-23', '5', 'Returns', 'inspection']
        ]
      },
      {
        path: '/health',
        label: 'Health',
        heading: 'Fulfillment healthy',
        body: 'Warehouse events and return intake are processing normally.',
        cards: [
          { title: 'Latency', detail: '72ms' },
          { title: 'Uptime', detail: '99.4%' },
          { title: 'Failed scans', detail: '1' }
        ]
      }
    ]
  },
  Postgres: {
    workspace: 'online-shop-agent2 / returns-ops',
    name: 'Postgres',
    portVariable: 'DB_PORT',
    defaultPort: 5432,
    routes: [
      {
        path: '/',
        label: 'Tables',
        heading: 'Returns database',
        body: 'Mock database browser for return and fulfillment data.',
        cards: [
          { title: 'returns', detail: '3 rows' },
          { title: 'shipments', detail: '3 rows' },
          { title: 'inventory', detail: '4 rows' }
        ]
      },
      {
        path: '/tables/returns',
        label: 'Returns',
        heading: 'returns table',
        body: 'Return authorization and refund state.',
        columns: ['id', 'order_ref', 'amount', 'status'],
        rows: [
          ['701', 'ORD-2042', '$129.00', 'label sent'],
          ['702', 'ORD-2041', '$34.00', 'received'],
          ['703', 'ORD-2039', '$58.00', 'refund ready']
        ]
      },
      {
        path: '/tables/shipments',
        label: 'Shipments',
        heading: 'shipments table',
        body: 'Outbound and exchange shipment records.',
        columns: ['id', 'carrier', 'destination', 'status'],
        rows: [
          ['9001', 'DHL', 'Berlin', 'in transit'],
          ['9002', 'UPS', 'Munich', 'label printed'],
          ['9003', 'DHL', 'Hamburg', 'delivered']
        ]
      },
      {
        path: '/tables/inventory',
        label: 'Inventory',
        heading: 'inventory table',
        body: 'Inventory after return adjustments.',
        columns: ['sku', 'name', 'available', 'returns'],
        rows: [
          ['MT-001', 'Merino Tee', '48', '3'],
          ['FJ-002', 'Field Jacket', '10', '2'],
          ['CB-003', 'Canvas Bag', '91', '4'],
          ['LB-004', 'Leather Belt', '29', '1']
        ]
      }
    ]
  }
};
