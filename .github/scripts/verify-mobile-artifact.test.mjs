import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { createHash, randomBytes } from 'node:crypto';
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const script = join(dirname(fileURLToPath(import.meta.url)), 'verify-mobile-artifact.sh');

function writeIpa(dir, { includePayload = false, extraFiles = 0, extraPaths = [] } = {}) {
  const root = join(dir, 'contents');
  mkdirSync(root);
  if (includePayload) {
    mkdirSync(join(root, 'Payload', 'AgentUp.app'), { recursive: true });
    writeFileSync(join(root, 'Payload', 'AgentUp.app', 'Info.plist'), '<plist></plist>');
  }

  writeFileSync(join(root, 'pad.bin'), randomBytes(1024 * 1024));
  for (let index = 0; index < extraFiles; index += 1) {
    writeFileSync(join(root, `extra-${index}.txt`), 'x');
  }
  for (const relative of extraPaths) {
    const full = join(root, relative);
    mkdirSync(dirname(full), { recursive: true });
    writeFileSync(full, 'x');
  }

  const ipa = join(dir, 'app.ipa');
  execFileSync('zip', ['-r', '-q', ipa, '.'], { cwd: root });

  const hash = createHash('sha256').update(readFileSync(ipa)).digest('hex');
  writeFileSync(`${ipa}.sha256`, `${hash}  app.ipa\n`);
  return ipa;
}

function verify(ipa) {
  return execFileSync('bash', [script, ipa, 'ipa'], { encoding: 'utf8' });
}

test('accepts an IPA whose Payload match is not the last zip entry', () => {
  const dir = mkdtempSync(join(tmpdir(), 'verify-ipa-'));
  try {
    const ipa = writeIpa(dir, { includePayload: true, extraFiles: 2000 });
    assert.doesNotThrow(() => verify(ipa));
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('rejects an IPA without Payload/*.app', () => {
  const dir = mkdtempSync(join(tmpdir(), 'verify-ipa-'));
  try {
    const ipa = writeIpa(dir, { includePayload: false, extraFiles: 0 });
    assert.throws(() => verify(ipa), /does not contain Payload\/\*\.app/);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('rejects a nested Payload/*.app that is not at the archive root', () => {
  const dir = mkdtempSync(join(tmpdir(), 'verify-ipa-'));
  try {
    const ipa = writeIpa(dir, { extraPaths: ['nested/Payload/Fake.app/Info.plist'] });
    assert.throws(() => verify(ipa), /does not contain Payload\/\*\.app/);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('rejects a similarly named NotPayload/*.app path', () => {
  const dir = mkdtempSync(join(tmpdir(), 'verify-ipa-'));
  try {
    const ipa = writeIpa(dir, { extraPaths: ['NotPayload/Fake.app/Info.plist'] });
    assert.throws(() => verify(ipa), /does not contain Payload\/\*\.app/);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});
