// Runs the trappanel bundle against the game's own trap window (TrapUI/TrapPanel.html): the Auto-replace checkbox of
// the trap slot of the window in its header.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import url from 'node:url';
import vm from 'node:vm';
import { JSDOM } from 'jsdom';
import { test, type TestContext } from 'vitest';

// The jsdom windows carry the game's page globals, which have no types.
type Win = any;

const HERE = path.dirname(url.fileURLToPath(import.meta.url));
const GAME_DIR = process.env.SL_GAME_DIR || 'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Survival Log';
const TRAP_PANEL_HTML = path.join(GAME_DIR, 'SurvivalLog_Data', 'StreamingAssets', 'WebUI', 'UI', 'TrapUI', 'TrapPanel.html');
// The bundle that Vite builds from src/Web/trappanel/ (npm test builds it first).
const BUNDLE_PATH = path.join(HERE, '..', '..', 'obj', 'page', 'trappanel.js');

if (!fs.existsSync(TRAP_PANEL_HTML)) {
  test.skip(`trappanel.js against the game page: game files not found under SL_GAME_DIR (${GAME_DIR})`, () => {});
} else {
  const bundle = fs.readFileSync(BUNDLE_PATH, 'utf8');
  const html = fs.readFileSync(TRAP_PANEL_HTML, 'utf8');

  async function loadPanel(t: TestContext): Promise<Win> {
    const dom = new JSDOM(html, { url: url.pathToFileURL(TRAP_PANEL_HTML).href, runScripts: 'dangerously', resources: 'usable', pretendToBeVisual: true });
    t.onTestFinished(() => dom.window.close());
    await new Promise<void>((resolve, reject) => {
      dom.window.addEventListener('load', () => resolve());
      setTimeout(() => reject(new Error('TrapPanel.html did not fire load within 5s')), 5000);
    });
    return dom.window;
  }

  // The root page: the iframe list holds the trap window.
  function makeRoot(t: TestContext, panel: Win | null): Win {
    const root = new JSDOM('<!doctype html><html><body></body></html>', { url: 'file:///Root.html', pretendToBeVisual: true }).window as Win;
    t.onTestFinished(() => root.close());
    const original = root.document.querySelectorAll.bind(root.document);
    root.document.querySelectorAll = (selector: string) =>
      selector === 'iframe' ? (panel ? [{ contentWindow: panel }] : []) : original(selector);
    vm.createContext(root);
    return root;
  }

  function run(root: Win, call: string): string {
    return vm.runInContext(bundle + ';' + call, root, { filename: 'trappanel.js' });
  }

  function send(root: Win, slot: string, on: boolean): string {
    return run(root, 'window.__traplineTrap.setState(' + JSON.stringify({ slot, on, words: { autoReplace: 'Auto-replace' } }) + ');window.__traplineTrap.install()');
  }

  test('jsdom: the header of the trap window gets the Auto-replace checkbox of its trap slot', async (t) => {
    const panel = await loadPanel(t);
    const root = makeRoot(t, panel);
    assert.equal(send(root, 'WarehousePos91', false), 'installed');

    const header = panel.document.querySelector('.header');
    const label = header.querySelector('.trapline-slot-option');
    assert.ok(label, 'the checkbox in the header');
    assert.ok(label.hasAttribute('data-interactive'));
    assert.equal(label.querySelector('.trapline-slot-option-text').textContent, 'Auto-replace');
    assert.equal(label.querySelector('input').checked, false);
    assert.equal(header.querySelectorAll('.trapline-slot-option').length, 1);

    send(root, 'WarehousePos91', true);
    assert.equal(label.querySelector('input').checked, true, 'a second send sets the state of the same checkbox');
    assert.equal(header.querySelectorAll('.trapline-slot-option').length, 1);
  });

  test('jsdom: a click on the checkbox posts the option of the trap slot', async (t) => {
    const panel = await loadPanel(t);
    const root = makeRoot(t, panel);
    send(root, 'WarehousePos92', true);
    const received: Win[] = [];
    root.addEventListener('message', (e: Win) => received.push(e.data));

    panel.document.querySelector('.trapline-slot-option input').click();
    await new Promise((r) => root.setTimeout(r, 30));

    const posts = received.filter((m) => m.type === 'TRAPLINE_SLOT_OPTION');
    assert.equal(posts.length, 1);
    assert.deepEqual({ ...posts[0].data }, { slot: 'WarehousePos92', on: false });
  });

  test('jsdom: a missing header turns off only this part and reports it', async (t) => {
    const panel = await loadPanel(t);
    panel.document.querySelector('.header').classList.remove('header');
    const root = makeRoot(t, panel);
    assert.equal(send(root, 'a', true), 'missing: .header');
    assert.equal(panel.document.querySelector('.trapline-slot-option'), null);
  });

  test('jsdom: with no trap window the script waits for its frame', async (t) => {
    const root = makeRoot(t, null);
    assert.equal(send(root, 'a', true), 'no frame');
  });
}
