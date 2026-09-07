import React, { useEffect, useMemo, useState } from 'react';
import { recordAudit } from './audit';
import './App.css';

const apiBaseUrl = `http://localhost:${__API_PORT__}`;

function formatCurrency(value) {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(Number(value));
}

function formatDate(value) {
  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value));
}

function Metric({ label, value, detail }) {
  return (
    <article className="metric-card">
      <p className="metric-label">{label}</p>
      <p className="metric-value">{value}</p>
      <p className="metric-detail">{detail}</p>
    </article>
  );
}

export default function App() {
  const [state, setState] = useState({
    status: 'loading',
    products: [],
    orders: [],
    summary: null,
    error: null,
  });

  useEffect(() => {
    let cancelled = false;

    async function loadDashboard() {
      try {
        const [productsResponse, ordersResponse] = await Promise.all([
          fetch(`${apiBaseUrl}/api/products`),
          fetch(`${apiBaseUrl}/api/orders`),
        ]);

        const productsPayload = await productsResponse.json();
        const ordersPayload = await ordersResponse.json();

        if (!productsResponse.ok) {
          throw new Error(productsPayload.error || `Products API returned ${productsResponse.status}`);
        }
        if (!ordersResponse.ok) {
          throw new Error(ordersPayload.error || `Orders API returned ${ordersResponse.status}`);
        }

        if (!cancelled) {
          setState({
            status: 'ready',
            products: productsPayload.products,
            orders: ordersPayload.orders,
            summary: productsPayload.summary,
            error: null,
          });
          await recordAudit('dashboard_loaded', 'success', {
            productCount: productsPayload.products.length,
            orderCount: ordersPayload.orders.length,
          });
        }
      } catch (error) {
        if (!cancelled) {
          setState({
            status: 'error',
            products: [],
            orders: [],
            summary: null,
            error: error.message,
          });
          await recordAudit('dashboard_loaded', 'failure', { message: error.message });
        }
      }
    }

    loadDashboard();
    const interval = window.setInterval(loadDashboard, 8000);
    return () => {
      cancelled = true;
      window.clearInterval(interval);
    };
  }, []);

  const summary = useMemo(
    () => state.summary || {
      productCount: state.products.length,
      totalInventory: 0,
      inventoryValue: 0,
      lowStockCount: 0,
    },
    [state.summary, state.products.length],
  );

  return (
    <main className="app-shell">
      <section className="topbar">
        <div>
          <p className="eyebrow">Agent-Up example workspace</p>
          <h1>Operations Dashboard</h1>
          <p className="lede">
            React reads live inventory and orders from Express. Postgres is seeded by SQL migrations when the API starts.
          </p>
        </div>
        <div className={`status-pill ${state.status}`}>
          <span />
          {state.status === 'ready' ? 'Live data' : state.status === 'loading' ? 'Loading' : 'API attention'}
        </div>
      </section>

      <section className="metrics-grid" aria-label="Inventory summary">
        <Metric label="Products" value={summary.productCount} detail="catalog rows in Postgres" />
        <Metric label="Inventory" value={summary.totalInventory.toLocaleString()} detail="units in stock" />
        <Metric label="Stock Value" value={formatCurrency(summary.inventoryValue)} detail="current inventory value" />
        <Metric label="Open Orders" value={state.orders.length} detail="rows in orders table" />
      </section>

      {state.status === 'error' && (
        <section className="notice">
          <strong>The dashboard could not reach the API.</strong>
          <span>{state.error}</span>
        </section>
      )}

      <section className="table-panel">
        <div className="table-header">
          <div>
            <h2>Product Portfolio</h2>
            <p>API port {__API_PORT__} · browse the Database tab on the Postgres service to inspect these tables</p>
          </div>
          <button type="button" onClick={() => window.location.reload()}>Refresh</button>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>SKU</th>
                <th>Product</th>
                <th>Category</th>
                <th>Status</th>
                <th>Inventory</th>
                <th>Unit Price</th>
              </tr>
            </thead>
            <tbody>
              {state.products.map((product) => (
                <tr key={product.sku}>
                  <td>{product.sku}</td>
                  <td>{product.name}</td>
                  <td>{product.category}</td>
                  <td>{product.status}</td>
                  <td>{Number(product.inventory).toLocaleString()}</td>
                  <td>{formatCurrency(product.unit_price)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section className="table-panel">
        <div className="table-header">
          <div>
            <h2>Recent Orders</h2>
            <p>Joined query across orders and order_items</p>
          </div>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Order</th>
                <th>Customer</th>
                <th>Region</th>
                <th>Status</th>
                <th>Lines</th>
                <th>Total</th>
                <th>Placed</th>
              </tr>
            </thead>
            <tbody>
              {state.orders.map((order) => (
                <tr key={order.id}>
                  <td>{order.id}</td>
                  <td>{order.customer_name}</td>
                  <td>{order.region}</td>
                  <td>{order.status}</td>
                  <td>{order.line_count}</td>
                  <td>{formatCurrency(order.total_amount)}</td>
                  <td>{formatDate(order.placed_at)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </main>
  );
}
