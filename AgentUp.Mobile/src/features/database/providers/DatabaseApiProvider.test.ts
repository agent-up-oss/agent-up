import assert from 'node:assert/strict';
import { test } from 'node:test';
import { executeQuery, listDatabases, listTables } from './DatabaseApiProvider';

type Recorded = { url: string; init: RequestInit };

function fakeFetch(status: number, body: string, recorded: Recorded[]): typeof fetch {
  return (async (url: string | URL | Request, init: RequestInit = {}) => {
    recorded.push({ url: String(url), init });
    return new Response(body, { status, headers: { 'Content-Type': 'application/json' } });
  }) as unknown as typeof fetch;
}

test('listDatabases requests the application databases route', async () => {
  const recorded: Recorded[] = [];
  const catalog = await listDatabases(
    { url: 'http://localhost:5000' },
    'ws 1',
    'Inventory',
    fakeFetch(200, JSON.stringify({ databases: ['harbor'] }), recorded),
  );

  assert.deepEqual(catalog.databases, ['harbor']);
  assert.equal(
    recorded[0].url,
    'http://localhost:5000/api/workspaces/ws%201/applications/Inventory/database/databases',
  );
});

test('listTables requests tables for the selected database', async () => {
  const recorded: Recorded[] = [];
  const tables = await listTables(
    { url: 'http://localhost:5000' },
    'harbor-shop',
    'Inventory',
    'harbor',
    fakeFetch(200, JSON.stringify({ tables: ['products'] }), recorded),
  );

  assert.deepEqual(tables.tables, ['products']);
  assert.equal(
    recorded[0].url,
    'http://localhost:5000/api/workspaces/harbor-shop/applications/Inventory/database/tables?database=harbor',
  );
});

test('executeQuery posts database and sql and returns columns with rows', async () => {
  const recorded: Recorded[] = [];
  const result = await executeQuery(
    { url: 'http://localhost:5000' },
    'harbor-shop',
    'Inventory',
    'harbor',
    'SELECT * FROM "products" LIMIT 50',
    fakeFetch(200, JSON.stringify({ columns: ['id'], rows: [['mug']] }), recorded),
  );

  assert.deepEqual(result.columns, ['id']);
  assert.deepEqual(result.rows, [['mug']]);
  assert.equal(
    recorded[0].url,
    'http://localhost:5000/api/workspaces/harbor-shop/applications/Inventory/database/query',
  );
  assert.equal(JSON.parse(String(recorded[0].init.body)).database, 'harbor');
  assert.match(JSON.parse(String(recorded[0].init.body)).sql, /products/);
});

test('empty Server bodies become empty catalogs and results', async () => {
  const emptyCatalog = await listDatabases({ url: 'http://localhost:5000' }, 'ws', 'Inventory', fakeFetch(200, '', []));
  const emptyTables = await listTables({ url: 'http://localhost:5000' }, 'ws', 'Inventory', 'harbor', fakeFetch(200, '', []));
  const emptyQuery = await executeQuery({ url: 'http://localhost:5000' }, 'ws', 'Inventory', 'harbor', 'select 1', fakeFetch(200, '', []));

  assert.deepEqual(emptyCatalog.databases, []);
  assert.deepEqual(emptyTables.tables, []);
  assert.deepEqual(emptyQuery.columns, []);
  assert.deepEqual(emptyQuery.rows, []);
});
