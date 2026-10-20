#!/usr/bin/env node
import { spawn } from 'node:child_process';
import { access } from 'node:fs/promises';
import { mkdir } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const html = resolve(root, 'scripts', 'social-card.html');
const out = resolve(root, 'static', 'img', 'social-card.png');
const candidates = [
  process.env.CHROME_PATH,
  '/usr/local/bin/google-chrome',
  '/usr/bin/google-chrome-stable',
  '/usr/bin/chromium',
].filter(Boolean);
let chrome;
for (const candidate of candidates) {
  try {
    await access(candidate);
    chrome = candidate;
    break;
  } catch {
    // try the next browser
  }
}
if (!chrome) throw new Error('No Chrome binary found for social-card render.');

await mkdir(dirname(out), { recursive: true });

const child = spawn(chrome, [
  '--headless=new',
  '--disable-gpu',
  '--hide-scrollbars',
  '--no-sandbox',
  '--force-device-scale-factor=1',
  '--window-size=1200,630',
  `--screenshot=${out}`,
  `file://${html}`,
], { stdio: 'inherit' });

const code = await new Promise(resolveCode => child.on('exit', resolveCode));
if (code !== 0) process.exit(code ?? 1);
