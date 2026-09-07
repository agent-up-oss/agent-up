import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const migrationsDir = path.join(path.dirname(fileURLToPath(import.meta.url)), '..', 'migrations');

export async function migrate(pool) {
  const files = (await fs.readdir(migrationsDir))
    .filter((file) => file.endsWith('.sql'))
    .sort();

  for (const file of files) {
    const id = file.replace(/\.sql$/, '');
    if (await isMigrationApplied(pool, id)) {
      continue;
    }

    const sql = await fs.readFile(path.join(migrationsDir, file), 'utf8');
    const client = await pool.connect();
    try {
      await client.query('begin');
      await client.query(sql);
      await client.query('insert into schema_migrations (id) values ($1)', [id]);
      await client.query('commit');
      console.log(`[migrate] applied ${file}`);
    } catch (error) {
      await client.query('rollback');
      throw error;
    } finally {
      client.release();
    }
  }
}

async function isMigrationApplied(pool, id) {
  try {
    const { rows } = await pool.query('select 1 from schema_migrations where id = $1', [id]);
    return rows.length > 0;
  } catch (error) {
    if (error.code === '42P01') {
      return false;
    }
    throw error;
  }
}

export async function seed(pool) {
  const seedDir = path.join(path.dirname(fileURLToPath(import.meta.url)), 'seed');
  const client = await pool.connect();
  try {
    await client.query('begin');
    const { rows } = await client.query('select count(*)::int as count from products');
    if (rows[0].count > 0) {
      await client.query('rollback');
      return;
    }

    const products = JSON.parse(await fs.readFile(path.join(seedDir, 'products.json'), 'utf8'));
    for (const product of products) {
      await client.query(
        `insert into products (sku, name, category, status, region, inventory, unit_price, margin, updated_at)
         values ($1, $2, $3, $4, $5, $6, $7, $8, $9)`,
        [
          product.sku,
          product.name,
          product.category,
          product.status,
          product.region,
          product.inventory,
          product.unit_price,
          product.margin,
          product.updated_at,
        ],
      );
    }

    const orders = JSON.parse(await fs.readFile(path.join(seedDir, 'orders.json'), 'utf8'));
    for (const order of orders) {
      await client.query(
        `insert into orders (id, customer_name, region, status, total_amount, placed_at)
         values ($1, $2, $3, $4, $5, $6)`,
        [order.id, order.customer_name, order.region, order.status, order.total_amount, order.placed_at],
      );

      for (const item of order.items) {
        await client.query(
          `insert into order_items (order_id, sku, quantity, line_total)
           values ($1, $2, $3, $4)`,
          [order.id, item.sku, item.quantity, item.line_total],
        );
      }
    }

    await client.query('commit');
    console.log(`[seed] inserted ${products.length} product(s) and ${orders.length} order(s)`);
  } catch (error) {
    await client.query('rollback');
    throw error;
  } finally {
    client.release();
  }
}
