import assert from 'node:assert/strict';
import net from 'node:net';
import test from 'node:test';
import { ensurePortAvailable, isPortInUse, resolveMobilePort, waitForPortAvailable } from './mobile-port.mjs';

test('uses the Agent-Up allocated web port', () => {
  assert.equal(resolveMobilePort('10901'), 10901);
});

test('uses the Expo default when no managed port is present', () => {
  assert.equal(resolveMobilePort(undefined), 8081);
});

test('rejects invalid managed ports', () => {
  assert.throws(() => resolveMobilePort('abc'), /numeric/);
  assert.throws(() => resolveMobilePort('70000'), /between/);
});

test('detects when a local port is free', async () => {
  const server = net.createServer();
  await new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', resolve);
  });
  const { port } = server.address();
  server.close();

  assert.equal(await isPortInUse(port), false);
});

test('detects when a local port is in use', async () => {
  const server = net.createServer();
  await new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', resolve);
  });
  const { port } = server.address();

  assert.equal(await isPortInUse(port), true);
  await new Promise((resolve) => server.close(resolve));
});

test('waits until a busy port becomes available', async () => {
  const server = net.createServer();
  await new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', resolve);
  });
  const { port } = server.address();

  const logs = [];
  setTimeout(() => server.close(), 250);

  await ensurePortAvailable(port, {
    maxGraceAttempts: 5,
    graceDelayMs: 100,
    log: (message) => logs.push(message),
  });

  assert.ok(logs.length > 0);
});

test('reports a helpful error when the port stays busy', async () => {
  const server = net.createServer();
  await new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', resolve);
  });
  const { port } = server.address();

  await assert.rejects(
    () => ensurePortAvailable(port, {
      maxGraceAttempts: 1,
      graceDelayMs: 0,
      log: () => {},
    }),
    /already in use/,
  );

  await new Promise((resolve) => server.close(resolve));
});

test('waitForPortAvailable remains available as an alias', async () => {
  const server = net.createServer();
  await new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', resolve);
  });
  const { port } = server.address();
  setTimeout(() => server.close(), 100);

  await waitForPortAvailable(port, {
    maxGraceAttempts: 5,
    graceDelayMs: 50,
    log: () => {},
  });
});

test('waitForPortAvailable maps legacy option names', async () => {
  const server = net.createServer();
  await new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', resolve);
  });
  const { port } = server.address();
  const logs = [];
  setTimeout(() => server.close(), 250);

  await waitForPortAvailable(port, {
    maxAttempts: 5,
    delayMs: 100,
    log: (message) => logs.push(message),
  });

  assert.ok(logs.length > 0);
});
