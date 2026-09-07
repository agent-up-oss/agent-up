import express from 'express';
import rateLimit from 'express-rate-limit';
import pg from 'pg';
import { migrate, seed } from './migrate.mjs';

const { Pool } = pg;

const app = express();
const limiter = rateLimit({
  windowMs: 60_000,
  max: 120,
  standardHeaders: true,
  legacyHeaders: false,
});
app.use(limiter);

const port = Number(process.env.API_PORT || 5601);
const postgresHost = process.env.POSTGRES_HOST || '127.0.0.1';
const postgresPort = Number(process.env.POSTGRES_PORT || 5432);

const runtimeMetrics = (() => {
  const startedAt = Date.now();
  let totalRequests = 0;
  let errorsTotal = 0;
  const recent = [];

  const prune = () => {
    const cutoff = Date.now() - 60_000;
    while (recent.length > 0 && recent[0].at < cutoff) {
      recent.shift();
    }
  };

  return {
    record(durationMs, statusCode) {
      totalRequests += 1;
      if (statusCode >= 400) {
        errorsTotal += 1;
      }
      recent.push({ at: Date.now(), durationMs });
      prune();
    },
    snapshot() {
      prune();
      const requestsPerMinute = recent.length;
      const latencyMs = requestsPerMinute === 0
        ? 0
        : Math.round(recent.reduce((sum, entry) => sum + entry.durationMs, 0) / requestsPerMinute);
      const successRate = totalRequests === 0
        ? 100
        : Math.round(((totalRequests - errorsTotal) / totalRequests) * 1000) / 10;

      return {
        latency_ms: latencyMs,
        requests_per_minute: requestsPerMinute,
        errors_total: errorsTotal,
        success_rate: successRate,
        uptime_percent: 100,
        requests_total: totalRequests,
        uptime_seconds: Math.round((Date.now() - startedAt) / 1000),
        heap_mb: Math.round(process.memoryUsage().heapUsed / 1024 / 1024),
      };
    },
  };
})();

const pool = new Pool({
  host: postgresHost,
  port: postgresPort,
  user: process.env.POSTGRES_USER || 'agentup',
  password: process.env.POSTGRES_PASSWORD || 'agentup-example-local',
  database: process.env.POSTGRES_DB || 'agentup',
});

let databaseReady = false;
let databaseWarmupPromise = null;

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function connectToPostgres() {
  const client = await pool.connect();
  client.release();
}

async function warmDatabase() {
  let attempt = 0;
  while (!databaseReady) {
    attempt += 1;
    try {
      await connectToPostgres();
      await migrate(pool);
      await seed(pool);
      databaseReady = true;
      console.log(`[startup] database ready after ${attempt} attempt(s)`);
      return;
    } catch (error) {
      const delayMs = Math.min(1000 * attempt, 5000);
      console.log(`[startup] waiting for Postgres (attempt ${attempt}): ${error.message}`);
      await sleep(delayMs);
    }
  }
}

function startDatabaseWarmup() {
  databaseWarmupPromise ??= warmDatabase();
  return databaseWarmupPromise;
}

async function ensureDatabase() {
  if (databaseReady) {
    return;
  }

  startDatabaseWarmup();
  throw new Error('Database is not ready yet.');
}

app.use(express.json());
app.use((req, res, next) => {
  const started = Date.now();
  res.on('finish', () => runtimeMetrics.record(Date.now() - started, res.statusCode));
  next();
});
app.use((_req, res, next) => {
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
  next();
});

app.get('/health', async (_req, res) => {
  try {
    await ensureDatabase();
    const { rows } = await pool.query('select count(*)::int as product_count from products');
    res.json({ ok: true, database: 'connected', product_count: rows[0]?.product_count ?? 0 });
  } catch (error) {
    console.error(`[health] ${error.message}`);
    res.status(503).json({ ok: false, database: error.message });
  }
});

app.get('/metrics', async (_req, res) => {
  try {
    await ensureDatabase();
    const { rows } = await pool.query(`
      select
        (select count(*)::int from products) as product_count,
        (select count(*)::int from orders) as order_count
    `);
    res.json({
      metrics: {
        ...runtimeMetrics.snapshot(),
        product_count: rows[0]?.product_count ?? 0,
        order_count: rows[0]?.order_count ?? 0,
      },
    });
  } catch (error) {
    console.error(`[metrics] ${error.message}`);
    res.status(503).json({ error: error.message });
  }
});

app.get('/api/products', async (_req, res) => {
  try {
    await ensureDatabase();
    const { rows } = await pool.query(`
      select sku, name, category, status, region, inventory, unit_price, margin, updated_at
      from products
      order by name
    `);

    const summary = rows.reduce(
      (current, product) => {
        const inventory = Number(product.inventory);
        const unitPrice = Number(product.unit_price);
        return {
          productCount: current.productCount + 1,
          totalInventory: current.totalInventory + inventory,
          inventoryValue: current.inventoryValue + inventory * unitPrice,
          lowStockCount: current.lowStockCount + (inventory < 100 ? 1 : 0),
        };
      },
      { productCount: 0, totalInventory: 0, inventoryValue: 0, lowStockCount: 0 },
    );

    res.json({ products: rows, summary });
  } catch (error) {
    console.error(`[products] ${error.message}`);
    res.status(503).json({ error: error.message });
  }
});

app.get('/api/orders', async (_req, res) => {
  try {
    await ensureDatabase();
    const { rows } = await pool.query(`
      select
        o.id,
        o.customer_name,
        o.region,
        o.status,
        o.total_amount,
        o.placed_at,
        count(oi.sku)::int as line_count
      from orders o
      left join order_items oi on oi.order_id = o.id
      group by o.id, o.customer_name, o.region, o.status, o.total_amount, o.placed_at
      order by o.placed_at desc
    `);
    res.json({ orders: rows });
  } catch (error) {
    console.error(`[orders] ${error.message}`);
    res.status(503).json({ error: error.message });
  }
});

app.listen(port, '0.0.0.0', () => {
  console.log(`API listening on ${port}`);
  console.log(`API querying Postgres at ${postgresHost}:${postgresPort}/${process.env.POSTGRES_DB || 'agentup'}`);
  startDatabaseWarmup().catch((error) => {
    console.error(`[startup] database warmup failed: ${error.message}`);
  });
}).on('error', (error) => {
  console.error(`[startup] ${error.message}`);
  process.exitCode = 1;
});
