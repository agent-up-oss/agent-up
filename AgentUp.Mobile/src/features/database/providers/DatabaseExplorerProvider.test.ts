import assert from 'node:assert/strict';
import { test } from 'node:test';
import { buildDefaultQuery, isDatabaseApplication, quoteIdentifier } from './DatabaseExplorerProvider';

test('isDatabaseApplication follows the Server database flag', () => {
  assert.equal(isDatabaseApplication({ name: 'Inventory', state: 'Running', database: true }), true);
  assert.equal(isDatabaseApplication({ name: 'Storefront', state: 'Running', database: false }), false);
  assert.equal(isDatabaseApplication({ name: 'Storefront', state: 'Running' }), false);
  assert.equal(isDatabaseApplication(undefined), false);
});

test('buildDefaultQuery quotes the selected table and limits to 50 rows', () => {
  assert.equal(buildDefaultQuery('products'), 'SELECT * FROM "products" LIMIT 50');
  assert.equal(buildDefaultQuery('public.orders'), 'SELECT * FROM "public"."orders" LIMIT 50');
  assert.equal(buildDefaultQuery('odd"name'), 'SELECT * FROM "odd""name" LIMIT 50');
});

test('quoteIdentifier doubles embedded quotes', () => {
  assert.equal(quoteIdentifier('orders'), '"orders"');
  assert.equal(quoteIdentifier('a"b'), '"a""b"');
});
