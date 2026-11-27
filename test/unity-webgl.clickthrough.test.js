const assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const fs = require('node:fs');
const http = require('node:http');
const os = require('node:os');
const path = require('node:path');
const { once } = require('node:events');
const { test } = require('node:test');
const { chromium } = require('playwright');

const root = path.resolve(__dirname, '..');
const webglDir = path.join(root, 'Builds', 'WebGL');

async function freePort() {
  const server = http.createServer();
  server.listen(0, '127.0.0.1');
  await once(server, 'listening');
  const { port } = server.address();
  await new Promise(resolve => server.close(resolve));
  return port;
}

async function waitForRelay(port, child) {
  const deadline = Date.now() + 15000;
  while (Date.now() < deadline) {
    if (child.exitCode !== null) throw new Error(`Relay exited early (${child.exitCode}).`);
    try {
      const response = await fetch(`http://127.0.0.1:${port}/health`);
      if (response.ok) return;
    } catch {}
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  throw new Error('Timed out waiting for the LAN relay.');
}

test('Unity WebGL clickthrough covers settings, LAN sign-in, and match start', { timeout: 120000 }, async () => {
  assert.ok(fs.existsSync(path.join(webglDir, 'index.html')), 'Build Unity WebGL first with ./build-webgl.sh');
  const tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'rl-survival-e2e-'));
  const port = await freePort();
  const relay = spawn(process.execPath, [path.join(root, 'relay', 'server.js')], {
    cwd: root,
    env: { ...process.env, PORT: String(port), WEB_BUILD: webglDir, ACCOUNTS_FILE: path.join(tempDir, 'accounts.json') },
    stdio: ['ignore', 'pipe', 'pipe']
  });
  let relayOutput = '';
  relay.stdout.on('data', chunk => { relayOutput += chunk; });
  relay.stderr.on('data', chunk => { relayOutput += chunk; });
  let browser;
  try {
    await waitForRelay(port, relay);
    const localAudioLibs = path.join(os.homedir(), '.local/share/rl-playwright-libs/usr/lib/x86_64-linux-gnu');
    if (fs.existsSync(path.join(localAudioLibs, 'libasound.so.2')))
      process.env.LD_LIBRARY_PATH = [localAudioLibs, process.env.LD_LIBRARY_PATH].filter(Boolean).join(path.delimiter);
    const pageErrors = new Set();
    const authResponses = [];
    browser = await chromium.launch({ headless: true, args: ['--enable-webgl', '--ignore-gpu-blocklist', '--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
    async function openClient() {
      const context = await browser.newContext({ viewport: { width: 960, height: 600 }, deviceScaleFactor: 1 });
      const page = await context.newPage();
      page.on('pageerror', error => pageErrors.add(error.message));
      page.on('console', message => { if (message.type() === 'error') pageErrors.add(message.text()); });
      page.on('response', response => {
        if (/\/auth\/(register|login)$/.test(response.url())) authResponses.push(response.status());
      });
      await page.goto(`http://127.0.0.1:${port}`, { waitUntil: 'domcontentloaded', timeout: 120000 });
      try {
        await page.waitForFunction(() => window.__unityReady === true, { timeout: 120000, polling: 200 });
      } catch (error) {
        error.message += ` (Unity readiness: ${await page.evaluate(() => String(window.__unityReady)).catch(() => 'page unavailable')}; browser errors: ${[...pageErrors].join(' | ')})`;
        throw error;
      }
      await page.waitForTimeout(6000);
      const canvas = page.locator('#unity-canvas');
      await canvas.waitFor({ state: 'visible' });
      return { page, canvas };
    }
    async function register(client, username) {
      await client.page.mouse.click(450, 282);
      await client.page.keyboard.type(username);
      await client.page.mouse.click(450, 334);
      await client.page.keyboard.type('testPass123');
      await client.page.mouse.click(365, 369);
      await client.page.waitForTimeout(1200);
    }

    const host = await openClient();
    const page = host.page;
    const canvas = host.canvas;
    const initial = await canvas.screenshot();
    fs.writeFileSync('/tmp/rl-survival-signin.png', initial);
    assert.ok(initial.length > 1000, 'Unity canvas rendered the sign-in screen');

    // Exercise the 4K and 720p choices. The browser owns the tab's canvas size,
    // so applying the saved choice must not shrink the WebGL viewport.
    await page.mouse.click(895, 28);
    await page.waitForTimeout(250);
    const settings = await canvas.screenshot();
    fs.writeFileSync('/tmp/rl-survival-settings.png', settings);
    assert.notDeepEqual(settings, initial, 'settings click changed the rendered game UI');
    await page.mouse.click(480, 299); // 3840 × 2160 · 4K
    const selected4k = await canvas.screenshot();
    assert.notDeepEqual(selected4k, settings, 'the 4K option is selectable');
    await page.mouse.click(480, 183); // 1280 × 720
    await page.mouse.click(365, 465); // Apply
    await page.waitForTimeout(300);
    await page.mouse.click(895, 28); // Close the panel

    const username = `host_${Date.now()}`;
    await register(host, username);
    assert.ok(authResponses.some(status => status >= 200 && status < 300), `Account registration succeeded (responses: ${authResponses.join(', ')})`);
    const lobby = await canvas.screenshot();
    fs.writeFileSync('/tmp/rl-survival-lobby.png', lobby);
    assert.notDeepEqual(lobby, initial, 'successful registration advanced into the lobby');

    await page.mouse.click(640, 399); // Solo run; the LAN two-client protocol flow is covered by npm test.
    await page.waitForTimeout(900);
    const buildPhase = await canvas.screenshot();
    fs.writeFileSync('/tmp/rl-survival-build.png', buildPhase);
    assert.notDeepEqual(buildPhase, lobby, 'Unity client advanced into the build phase');

    // Exercise the gameplay click path that previously crashed the build.
    await page.mouse.click(480, 350);
    await page.waitForTimeout(400);
    assert.deepEqual([...pageErrors], [], `Browser console errors: ${[...pageErrors].join('\n')}`);
  } catch (error) {
    error.message += `\nRelay output:\n${relayOutput}`;
    throw error;
  } finally {
    if (browser) await browser.close();
    const relayExited = relay.exitCode === null ? once(relay, 'exit') : Promise.resolve();
    relay.kill('SIGTERM');
    await relayExited;
    fs.rmSync(tempDir, { recursive: true, force: true });
  }
});
