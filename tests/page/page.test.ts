// Runs the page script bundle against the game's own CoreUI1.html, so a game update that renames or removes a page
// part page.js depends on shows up here instead of only in the game.
//
// CoreUI1 is a persistent HUD panel (not a pop-up like the event or cooking windows), driven by a plain,
// non-module <script> tag that declares `const state = reactive({...})` as a top-level lexical binding -
// so `iframe.contentWindow.eval('state')` can read it. The root page normally
// supplies MutationObserver, setTimeout, and the iframe list (it is a real browser window), so this test
// also loads a small real jsdom window to run page.js in, instead of a bare object stub.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import url from 'node:url';
import vm from 'node:vm';
import { JSDOM } from 'jsdom';
import { test, type TestContext } from 'vitest';

// The jsdom windows carry the game's page globals (state, eval) and the test's own frame hook, which
// have no types.
type Win = any;

const HERE = path.dirname(url.fileURLToPath(import.meta.url));

const GAME_DIR = process.env.SL_GAME_DIR ||
  'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Survival Log';
const CORE_UI1_HTML = path.join(GAME_DIR, 'SurvivalLog_Data', 'StreamingAssets', 'WebUI', 'UI', 'CoreUI1', 'CoreUI1.html');
// The bundle that Vite builds from src/Web/page/ (npm test builds it first).
const PAGE_JS_PATH = path.join(HERE, '..', '..', 'obj', 'page', 'page.js');
// The source file with the FEATURES table, in the quotes that the static test parses.
const FEATURES_PATH = path.join(HERE, '..', '..', 'src', 'Web', 'page', 'core.ts');

const gameFileExists = fs.existsSync(CORE_UI1_HTML);

if (!gameFileExists) {
  test.skip(`page.js against the game page: game files not found under SL_GAME_DIR (${GAME_DIR}); set SL_GAME_DIR to the game folder`, () => {});
} else {
  const pageJs = fs.readFileSync(PAGE_JS_PATH, 'utf8');
  const coreUi1Html = fs.readFileSync(CORE_UI1_HTML, 'utf8');

  // Pulls the page part names out of the FEATURES table of the source, so this test never copies the names.
  function featureNames(): string[] {
    const table = fs.readFileSync(FEATURES_PATH, 'utf8').match(/FEATURES = \{([\s\S]*?)\};/);
    assert.ok(table, 'FEATURES table not found in page.js');
    const names: string[] = [];
    const quoted = /'([^']+)'/g;
    let m;
    while ((m = quoted.exec(table[1]))) names.push(m[1]);
    assert.ok(names.length > 0, 'no part names parsed out of the FEATURES table');
    return names;
  }

  // A '.class' part is a CSS selector; every piece of it (split on '.') must appear as text in the page.
  function partIsPresent(name: string): boolean {
    return name.replace(/^[#.]/, '').split('.').every((piece) => coreUi1Html.includes(piece));
  }

  test('static: every FEATURES part name of page.js exists in CoreUI1.html', () => {
    for (const name of featureNames()) {
      assert.ok(partIsPresent(name), `page part "${name}" not found in CoreUI1.html`);
    }
  });

  // CoreUI1.html starts its own persistent requestAnimationFrame loops (plant-countdown ticks and the
  // like) as soon as it loads, which would otherwise keep every jsdom window (and so the test process)
  // alive forever. t.onTestFinished(...) closes each window once its test is done, which jsdom uses to
  // cancel those loops.
  async function loadCoreWindow(t: TestContext): Promise<Win> {
    const dom = new JSDOM(coreUi1Html, {
      url: url.pathToFileURL(CORE_UI1_HTML).href,
      runScripts: 'dangerously',
      resources: 'usable',
      pretendToBeVisual: true
    });
    t.onTestFinished(() => dom.window.close());
    await new Promise<void>((resolve, reject) => {
      dom.window.addEventListener('load', () => resolve());
      setTimeout(() => reject(new Error('CoreUI1.html did not fire load within 5s')), 5000);
    });
    return dom.window;
  }

  // A blank real window to run page.js in, standing in for the root page (Root.html), which is what
  // supplies MutationObserver, setTimeout, and the iframe list in the real game, and is the target of
  // the button's own postMessage ("posts to the root window"). The CoreUI1 iframe is held in a mutable
  // box so a test can swap it (a fresh iframe on a later setData call), the same way Root.html itself
  // is never recreated between panel reloads.
  function makeRootWindow(t: TestContext, coreWindow: Win | null): Win {
    const root = new JSDOM('<!doctype html><html><body></body></html>', {
      url: 'file:///Root.html',
      pretendToBeVisual: true
    }).window as Win;
    t.onTestFinished(() => root.close());
    const frameBox = { current: coreWindow || null };
    const originalQSA = root.document.querySelectorAll.bind(root.document);
    root.document.querySelectorAll = (selector: string) =>
      selector === 'iframe' ? (frameBox.current ? [{ contentWindow: frameBox.current }] : []) : originalQSA(selector);
    vm.createContext(root);
    root.__setCoreFrame = (w: Win) => { frameBox.current = w; };
    return root;
  }

  // vm.runInContext (not root.eval) so the bare "window" identifier page.js relies on resolves to root
  // itself, the same as it does for a real page evaluated in a browser context.
  function runPageJs(root: Win, callExpr: string): string {
    return vm.runInContext(pageJs + ';' + callExpr, root, { filename: 'page.js' });
  }

  // The last data sent to each root window, so install() and setGroups() can send it again the way
  // C# does: setData is the one call of the page script.
  const lastData = new WeakMap<object, { words: Record<string, string>; groups: unknown; replace?: boolean; pin?: boolean }>();

  function setData(root: Win, data: { words: Record<string, string>; groups: unknown; replace?: boolean; pin?: boolean }): string {
    lastData.set(root, data);
    return runPageJs(root, 'window.__trapline.setData(' + JSON.stringify(data) + ')');
  }

  // Sends the last data again (no data: the English word that C# sends and no groups), which applies the
  // features once, as the next C# push does.
  function install(root: Win): string {
    return setData(root, lastData.get(root) || { words: { takeAll: 'Take All' }, groups: null });
  }

  function wait(win: Win, ms: number): Promise<void> {
    return new Promise((resolve) => win.setTimeout(resolve, ms));
  }

  // Sends the trap-list message the real reducer sends (WebUI_CoreUI1_TrapMsg), the same shape
  // Reducer_Web_CoreUI1 / WebUI_CoreUI1.UpdateTrapsList builds.
  async function postTraps(coreWindow: Win, traps: object[], containerActive?: boolean): Promise<void> {
    coreWindow.postMessage({
      type: 'WebUI_CoreUI1_TrapMsg',
      data: { containerActive: containerActive !== false, traps }
    }, '*');
    await wait(coreWindow, 30);
  }

  function trap(id: number, status: number, roomName?: string, preyCount?: number) {
    return { trapInstanceId: id, trapName: 'Snare', roomName: roomName || '', iconUrl: '', status, preyCount: preyCount || 0 };
  }

  test('jsdom: a status-1 trap shows the Take All button, styled before the chevron', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 1)]);
    const root = makeRootWindow(t, coreWindow);

    const result = install(root);
    assert.equal(result, 'installed', `install result was "${result}"`);

    const row = coreWindow.document.querySelector('.trap-toggle-row');
    const btn = row.querySelector('.trapline-take-all');
    assert.ok(btn, 'the Take All button was not added to the header row');
    assert.equal(btn.textContent, 'Take All');
    assert.ok(btn.nextElementSibling.classList.contains('trap-toggle-chevron'), 'the button must sit right before the chevron');
  });

  test('jsdom: no button before C# sent the words, then the button with the sent word', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 1)]);
    const root = makeRootWindow(t, coreWindow);

    const result = runPageJs(root, 'window.__trapline.setData({"groups":null})');
    assert.equal(result, 'installed', `setData result was "${result}"`);
    assert.equal(coreWindow.document.querySelector('.trapline-take-all'), null, 'a button showed before the words');

    setData(root, { words: { takeAll: '全部拾取' }, groups: null });
    assert.equal(coreWindow.document.querySelector('.trap-toggle-row .trapline-take-all')?.textContent, '全部拾取');
  });

  test('jsdom: a word that C# did not send shows its name, not an English default', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 1)]);
    const root = makeRootWindow(t, coreWindow);

    setData(root, { words: {}, groups: null });
    assert.equal(coreWindow.document.querySelector('.trap-toggle-row .trapline-take-all')?.textContent, 'takeAll');
  });

  test('jsdom: a Stopped Auto Trap Cage as the only trap with prey shows the Take All button', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    // Status 3 Stopped with 8 prey in its storage box; the other traps have no prey.
    await postTraps(coreWindow, [trap(1, 3, '', 8), trap(2, 2), trap(3, 0)]);
    const root = makeRootWindow(t, coreWindow);

    const result = install(root);
    assert.equal(result, 'installed', `install result was "${result}"`);

    const btn = coreWindow.document.querySelector('.trap-toggle-row .trapline-take-all');
    assert.ok(btn, 'no Take All button for a Stopped Auto Trap Cage');
  });

  test('jsdom: no trap with prey shows no button', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 2), trap(2, 0)]);
    const root = makeRootWindow(t, coreWindow);

    const result = install(root);
    assert.equal(result, 'installed', `install result was "${result}"`);

    const row = coreWindow.document.querySelector('.trap-toggle-row');
    assert.ok(row, 'the header row should still render (traps exist, just none with prey)');
    assert.equal(row.querySelector('.trapline-take-all'), null, 'a button appeared with no trap having prey');
  });

  test('jsdom: a click posts TRAPLINE_TAKE_ALL to the root window and does not toggle the list', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 1)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);

    const received: Win[] = [];
    root.addEventListener('message', (e: Win) => received.push(e.data));

    const btn = coreWindow.document.querySelector('.trap-toggle-row .trapline-take-all');
    btn.dispatchEvent(new coreWindow.MouseEvent('click', { bubbles: true }));
    await wait(root, 30);

    // received[0].data crosses realms (postMessage structured-clones it into root's own realm), so it is
    // spread into a plain object of THIS realm before comparing - a strict deepEqual across realms fails
    // on prototype identity alone, even with identical own properties.
    assert.equal(received.length, 1, 'expected exactly one posted message');
    assert.equal(received[0].type, 'TRAPLINE_TAKE_ALL');
    assert.equal(received[0].sourcePageId, 'CoreUI1');
    assert.deepEqual({ ...received[0].data }, {});
    assert.equal(coreWindow.eval('state.trapsExpanded'), false, 'the click must not open the trap list');
  });

  test('jsdom: a second setData gives one button, not two', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 1)]);
    const root = makeRootWindow(t, coreWindow);

    assert.equal(install(root), 'installed');
    assert.equal(install(root), 'installed');

    const buttons = coreWindow.document.querySelectorAll('.trap-toggle-row .trapline-take-all');
    assert.equal(buttons.length, 1, `expected exactly one button, found ${buttons.length}`);
  });

  test('jsdom: the trap panel created again (0 traps, then 1) gets the button again', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    assert.equal(coreWindow.document.querySelector('.trap-panel'), null, 'no traps yet, so no trap panel');

    await postTraps(coreWindow, [trap(1, 1)]);
    assert.equal(install(root), 'installed');
    assert.ok(coreWindow.document.querySelector('.trap-toggle-row .trapline-take-all'), 'the button did not appear on the first trap panel');

    // Back to zero traps: Vue's v-if removes the whole .trap-panel (and so the old header row and button).
    await postTraps(coreWindow, []);
    assert.equal(coreWindow.document.querySelector('.trap-panel'), null, 'the trap panel should be gone with no traps');

    // A trap again: Vue recreates .trap-panel and .trap-toggle-row from scratch.
    await postTraps(coreWindow, [trap(2, 1)]);
    assert.equal(install(root), 'installed');
    const btn = coreWindow.document.querySelector('.trap-toggle-row .trapline-take-all');
    assert.ok(btn, 'the button did not reappear on the recreated trap panel');
  });

  test('jsdom: the urgent badge still shows next to the button', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 1), trap(2, 1)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);

    const row = coreWindow.document.querySelector('.trap-toggle-row');
    const badge = row.querySelector('.trap-toggle-badge');
    assert.ok(badge, 'the urgent badge should still render');
    assert.equal(badge.textContent, '2');
    assert.ok(row.querySelector('.trapline-take-all'), 'the button should render alongside the badge');
  });

  test('jsdom: no "missing" report while the list is collapsed', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 1)]);
    const root = makeRootWindow(t, coreWindow);

    assert.equal(coreWindow.eval('state.trapsExpanded'), false, 'the list should start collapsed');
    const result = install(root);
    assert.equal(result, 'installed', `expected no missing-part report while collapsed, got "${result}"`);
  });

  // Sends group data through setData with the last word, the same shape C# pushes: the column and row counts, the
  // floor lines and zone lines with their rows, and each cell with its slot, its place, and its trap.
  function setGroups(root: Win, groups: unknown): string {
    const words = (lastData.get(root) || {}).words || {};
    return setData(root, { words, groups });
  }

  function expand(coreWindow: Win): Promise<void> {
    coreWindow.eval('state.trapsExpanded = true');
    return wait(coreWindow, 30);
  }

  // The grid place of one node, with spaces removed, as '<row>|<column>'. The script sets it as the
  // custom properties --tl-fg-row and --tl-fg-col (the floor grid library), which its rule reads into grid-row and
  // grid-column.
  function place(el: Win): string {
    const v = (p: string) => el.style.getPropertyValue(p).replace(/\s+/g, '');
    return v('--tl-fg-row') + '|' + v('--tl-fg-col');
  }

  // The cell column count of the popover.
  function columns(doc: Win): string {
    return doc.querySelector('.trap-popover').style.getPropertyValue('--tl-fg-cols').trim();
  }

  function emptyCell(doc: Win, slot: string): Win {
    return doc.querySelector('.trapline-empty-cell[data-slot="' + slot + '"]');
  }

  function lines(doc: Win): { name: string; kind: string; row: string }[] {
    return [...doc.querySelectorAll('.tl-fg-line')].map((l: Win) => ({
      name: l.querySelector('.tl-fg-name').textContent,
      kind: l.classList.contains('tl-fg-floor') ? 'floor' : 'zone',
      row: l.style.getPropertyValue('--tl-fg-row'),
    }));
  }

  const cell = (slot: string, row: number, col: number, trap?: number) => (trap ? { slot, row, col, trap } : { slot, row, col });

  // Home: traps 1 and 3 with the free slot h2 between them. Basement: the free slot b1, then trap 2.
  const TWO_FLOORS = {
    cols: 3,
    rows: 4,
    lines: [{ key: 'f1', kind: 'floor', row: 1, name: 'Home' }, { key: 'f101', kind: 'floor', row: 3, name: 'Basement' }],
    cells: [cell('h1', 2, 1, 1), cell('h2', 2, 2), cell('h3', 2, 3, 3), cell('b1', 4, 1), cell('b2', 4, 2, 2)],
  };

  // The Warehouse Manager home: the floor Home with the zone lines Warehouse Interior and Outdoors, and the 2nd Floor
  // with only its floor line.
  const WAREHOUSE = {
    cols: 3,
    rows: 7,
    lines: [
      { key: 'f102', kind: 'floor', row: 1, name: '2nd Floor' },
      { key: 'f1', kind: 'floor', row: 3, name: 'Home' },
      { key: 'z1:7', kind: 'zone', row: 4, name: 'Warehouse Interior' },
      { key: 'z1:8', kind: 'zone', row: 6, name: 'Outdoors' },
    ],
    cells: [cell('W2F05', 2, 1, 4), cell('W91', 5, 1, 1), cell('W92', 5, 2, 2), cell('W93', 5, 3), cell('W99', 7, 1, 3)],
  };

  test('jsdom: rows and empty cells are placed in the grid under the floor lines, from the left edge', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    // All armed (status 0), so the page's own prey-first sort leaves this insertion order untouched.
    await postTraps(coreWindow, [trap(1, 0), trap(2, 0), trap(3, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);

    const result = setGroups(root, TWO_FLOORS);
    assert.equal(result, 'installed', `setGroups result was "${result}"`);

    const doc = coreWindow.document;
    const popover = doc.querySelector('.trap-popover');
    assert.ok(popover.classList.contains('trapline-grid'));
    assert.ok(popover.classList.contains('tl-fg'), 'the floor grid library draws the grid');

    const rows = [...doc.querySelectorAll('.trap-item-row')];
    assert.equal(place(rows[0]), '2|1', 'trap 1: the first cell under the Home line, at the left edge');
    assert.equal(place(rows[1]), '4|2', 'trap 2: the second cell under the Basement line');
    assert.equal(place(rows[2]), '2|3', 'trap 3: the third cell under the Home line');

    assert.deepEqual(lines(doc), [{ name: 'Home', kind: 'floor', row: '1' }, { name: 'Basement', kind: 'floor', row: '3' }]);
    assert.equal(columns(doc), '3', 'as many cell columns as the longest line');
    assert.equal(place(emptyCell(doc, 'h2')), '2|2');
    assert.equal(place(emptyCell(doc, 'b1')), '4|1');
    assert.equal(doc.querySelectorAll('.trapline-empty-cell').length, 2);
  });

  test('jsdom: the zone lines of a floor with two zones, and only the floor line of a floor with one zone', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0), trap(2, 0), trap(3, 0), trap(4, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, WAREHOUSE);

    const doc = coreWindow.document;
    assert.deepEqual(lines(doc), [
      { name: '2nd Floor', kind: 'floor', row: '1' },
      { name: 'Home', kind: 'floor', row: '3' },
      { name: 'Warehouse Interior', kind: 'zone', row: '4' },
      { name: 'Outdoors', kind: 'zone', row: '6' },
    ]);
    const rows = [...doc.querySelectorAll('.trap-item-row')];
    assert.deepEqual(rows.map(place), ['5|1', '5|2', '7|1', '2|1']);
    assert.equal(place(emptyCell(doc, 'W93')), '5|3');
    for (const line of doc.querySelectorAll('.tl-fg-line')) {
      assert.ok(line.querySelector('.tl-fg-rule'), 'each line has its rule to the right edge');
    }
  });

  test('jsdom: the grid has no label column', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, TWO_FLOORS);

    const doc = coreWindow.document;
    assert.equal(doc.querySelector('.trapline-floor-label'), null, 'no floor label node');
    const css = styleText(doc);
    assert.doesNotMatch(css, /max-content/, 'no label column in the column template');
    assert.match(css, /\.tl-fg\{[^}]*grid-template-columns:var\(--tl-fg-tracks\)/, 'the columns of the floor grid library');
  });

  test('jsdom: the lines are compact: small names and no rule', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, TWO_FLOORS);

    const css = styleText(coreWindow.document);
    assert.match(css, /--tl-fg-floor-size:7px;/, 'a 7px floor name');
    assert.match(css, /--tl-fg-zone-size:6\.5px;/, 'a 6.5px zone name');
    assert.match(css, /\.tl-fg-line\{[^}]*line-height:1;/, 'no extra line height');
    assert.match(css, /\.trap-popover\.trapline-grid\.tl-fg-rule\{[^}]*display:none/, 'no rule after the name');
  });

  test('jsdom: a long Chinese name is cut and does not widen the grid past 8 cells', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    const name = '仓库内部仓库内部仓库内部仓库内部仓库内部';
    setGroups(root, { cols: 1, rows: 2, lines: [{ key: 'f1', kind: 'floor', row: 1, name }], cells: [cell('a', 2, 1, 1)] });

    const doc = coreWindow.document;
    assert.equal(lines(doc)[0].name, name, 'the whole name is in the node; the CSS cuts it');
    const css = styleText(doc);
    assert.match(css, /\.tl-fg-name\{[^}]*text-overflow:ellipsis/);
    assert.match(css, /--tl-fg-max-cols:8;/, 'a name widens the grid to at most 8 cells');
    assert.equal(columns(doc), '1', 'the cell columns stay as the data gives them');
  });

  test('jsdom: a second setGroups with the same data writes nothing to the DOM', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0), trap(2, 0), trap(3, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, TWO_FLOORS);

    const popover = coreWindow.document.querySelector('.trap-popover');
    const watcher = new coreWindow.MutationObserver(() => {});
    watcher.observe(popover, { childList: true, subtree: true, attributes: true, characterData: true });
    setGroups(root, TWO_FLOORS);
    const records = watcher.takeRecords();
    watcher.disconnect();
    assert.equal(records.length, 0, `expected no DOM write, got ${records.length} mutation records`);
  });

  test('jsdom: after a Vue update (a trap added, a trap removed) each row has its cell and the free cells follow', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0), trap(2, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, TWO_FLOORS);

    // Vue update: drop trap 2 (Basement), add trap 3 (Home); C# pushes the groups again with b2 free.
    await postTraps(coreWindow, [trap(1, 0), trap(3, 0)]);
    setGroups(root, { ...TWO_FLOORS, cells: [cell('h1', 2, 1, 1), cell('h2', 2, 2), cell('h3', 2, 3, 3), cell('b1', 4, 1), cell('b2', 4, 2)] });

    const doc = coreWindow.document;
    const rows = [...doc.querySelectorAll('.trap-item-row')];
    assert.equal(rows.length, 2, 'expected exactly one row per current trap');
    assert.equal(place(rows[0]), '2|1', 'trap 1');
    assert.equal(place(rows[1]), '2|3', 'trap 3');
    assert.deepEqual([...doc.querySelectorAll('.trapline-empty-cell')].map((c: Win) => c.getAttribute('data-slot')).sort(), ['b1', 'b2', 'h2']);
    assert.equal(doc.querySelectorAll('.tl-fg-line').length, 2, 'each line once');
  });

  test('jsdom: a new trap that the data does not have yet goes to the line after all rows', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    // The data still has h2 free, and a new trap 5 is on it before C# pushes again.
    setGroups(root, TWO_FLOORS);
    await postTraps(coreWindow, [trap(1, 0), trap(5, 0)]);
    install(root);

    const doc = coreWindow.document;
    const rows = [...doc.querySelectorAll('.trap-item-row')];
    assert.equal(place(rows[1]), '5|1', 'trap 5 has no cell in the data, so it goes to the line after all rows');
  });

  test('jsdom: a collapse and a new expand keep the grid', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0), trap(2, 0), trap(3, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    setGroups(root, TWO_FLOORS);

    await expand(coreWindow);
    install(root);
    let rows = [...coreWindow.document.querySelectorAll('.trap-item-row')];
    assert.equal(place(rows[1]), '4|2');

    // Collapse: Vue's v-if destroys .trap-popover (and everything inside it) once its leave transition
    // finishes.
    coreWindow.eval('state.trapsExpanded = false');
    await wait(coreWindow, 100);
    assert.equal(coreWindow.document.querySelector('.trap-popover'), null);

    // Re-expand: a brand new .trap-popover with brand new rows.
    await expand(coreWindow);
    install(root);
    const doc = coreWindow.document;
    rows = [...doc.querySelectorAll('.trap-item-row')];
    assert.equal(rows.length, 3, 'no leftover rows from before the collapse');
    assert.equal(place(rows[1]), '4|2');
    assert.equal(doc.querySelectorAll('.tl-fg-line').length, 2, 'no duplicate lines');
    assert.equal(doc.querySelectorAll('.trapline-empty-cell').length, 2, 'no duplicate empty cells');
  });

  test('jsdom: a trap with no cell goes to one line after all rows, with no line name', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0), trap(9, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, { cols: 1, rows: 2, lines: [{ key: 'f1', kind: 'floor', row: 1, name: 'Home' }], cells: [cell('a', 2, 1, 1)] });

    const doc = coreWindow.document;
    const rows = [...doc.querySelectorAll('.trap-item-row')];
    assert.equal(place(rows[0]), '2|1');
    assert.equal(place(rows[1]), '3|1', 'trap 9 has no cell, so it goes to the line after Home');
    assert.equal(doc.querySelectorAll('.tl-fg-line').length, 1, 'no line for the extra line');
    assert.equal(columns(doc), '1');
  });

  test('jsdom: more than 8 traps with no cell wrap to a second extra line', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [1, 2, 3, 4, 5, 6, 7, 8, 9].map((id) => trap(id, 0)));
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, { cols: 1, rows: 0, lines: [], cells: [] });

    const doc = coreWindow.document;
    const rows = [...doc.querySelectorAll('.trap-item-row')];
    assert.equal(place(rows[7]), '1|8');
    assert.equal(place(rows[8]), '2|1', 'the 9th extra trap starts a new line instead of overlapping the 1st');
    assert.equal(columns(doc), '8');
  });

  test('jsdom: a floor with no trap shows its line and only empty cells', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, {
      cols: 2,
      rows: 4,
      lines: [{ key: 'f1', kind: 'floor', row: 1, name: 'Home' }, { key: 'f101', kind: 'floor', row: 3, name: 'Basement' }],
      cells: [cell('a', 2, 1, 1), cell('b1', 4, 1), cell('b2', 4, 2)],
    });

    const doc = coreWindow.document;
    assert.deepEqual(lines(doc)[1], { name: 'Basement', kind: 'floor', row: '3' });
    assert.equal(columns(doc), '2');
    assert.deepEqual([emptyCell(doc, 'b1'), emptyCell(doc, 'b2')].map(place), ['4|1', '4|2']);
  });

  test('jsdom: a trap row click still calls the game handler in the grid', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0), trap(2, 0), trap(3, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, TWO_FLOORS);

    // UnitySendEvent posts to window.parent from inside the CoreUI1 script itself; coreWindow was loaded
    // standalone (not as a real iframe), so its own "parent" is itself, and the message loops straight
    // back to a listener added on coreWindow. The page also posts its own PAGE_READY /
    // SYNC_INTERACTIVE_RECTS messages on its own schedule, unrelated to this click, so this filters down
    // to the message type the row's click handler sends.
    const received: Win[] = [];
    coreWindow.addEventListener('message', (e: Win) => received.push(e.data));

    const row = coreWindow.document.querySelectorAll('.trap-item-row')[0];
    row.dispatchEvent(new coreWindow.MouseEvent('click', { bubbles: true }));
    await wait(coreWindow, 30);

    const clicks = received.filter((m) => m.type === 'TRAP_HUD_CLICK');
    assert.equal(clicks.length, 1, 'expected exactly one TRAP_HUD_CLICK message');
    assert.deepEqual({ ...clicks[0].data }, { instanceId: 1 });
  });

  const ONE_CELL = { cols: 1, rows: 2, lines: [{ key: 'f1', kind: 'floor', row: 1, name: 'Home' }], cells: [cell('a', 2, 1, 1)] };

  test('jsdom: a grid row hides its room name', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0, 'Balcony')]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);

    const room = coreWindow.document.querySelector('.trap-item-room');
    assert.notEqual(coreWindow.getComputedStyle(room).display, 'none', 'the room name should show before the grid applies');

    const result = setGroups(root, ONE_CELL);
    assert.equal(result, 'installed', `setGroups result was "${result}"`);

    assert.equal(coreWindow.getComputedStyle(room).display, 'none', 'the room name should be hidden in the grid');
  });

  test('jsdom: a class not in the page text turns off only the grid', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 1)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);

    // Simulates a game update that renamed the popover's class; the button's own part (.trap-toggle-row,
    // .trap-toggle-chevron) is untouched.
    const popover = coreWindow.document.querySelector('.trap-popover');
    popover.classList.remove('trap-popover');

    const result = setGroups(root, ONE_CELL);
    assert.match(result, /missing: groups\(\.trap-popover\)/, `expected a missing-part report, got "${result}"`);

    const row = coreWindow.document.querySelector('.trap-item-row');
    assert.equal(place(row), '|', 'no grid placement when a required groups part is missing');
    assert.ok(!popover.classList.contains('trapline-grid'), 'no grid class when a required groups part is missing');
    assert.equal(coreWindow.document.querySelector('.tl-fg-line'), null, 'no line when a required groups part is missing');
    assert.ok(coreWindow.document.querySelector('.trap-toggle-row .trapline-take-all'), 'the button feature must be unaffected by the groups part going missing');
  });

  const ONE_FLOOR_THREE = {
    cols: 4,
    rows: 2,
    lines: [{ key: 'f1', kind: 'floor', row: 1, name: 'Home' }],
    cells: [cell('a', 2, 1, 1), cell('b', 2, 2, 2), cell('c', 2, 3, 3), cell('d', 2, 4)],
  };

  // The rules page.js adds, as one text with spaces removed. jsdom does not lay out a grid, so the look
  // of a cell is checked in this text and the game checks the rest.
  function styleText(doc: Win): string {
    return doc.getElementById('trapline-style').textContent.replace(/\s+/g, '');
  }

  test('jsdom: a grid row shows only its icon', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 1, 'Balcony')]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, ONE_CELL);

    const doc = coreWindow.document;
    assert.equal(coreWindow.getComputedStyle(doc.querySelector('.trap-item-name')).display, 'none', 'no trap name');
    assert.equal(coreWindow.getComputedStyle(doc.querySelector('.trap-item-status')).display, 'none', 'no status text');
    assert.notEqual(coreWindow.getComputedStyle(doc.querySelector('.trap-item-icon-img')).display, 'none', 'the icon shows');
  });

  test('jsdom: the cell style overrides the game row style', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);

    const css = styleText(coreWindow.document);
    const rowRule = css.match(/\.trap-popover\.trapline-grid\.trap-item-row\{([^}]*)\}/);
    assert.ok(rowRule, 'a rule for a grid row');
    for (const decl of ['width:22px', 'height:22px', 'padding:0', 'gap:0', 'justify-content:center', 'border:1pxsolid']) {
      assert.ok(rowRule[1].includes(decl), `the grid row rule has ${decl}`);
    }
    assert.match(css, /\.trap-popover\.trapline-grid\.trap-item-row:hover\{[^}]*transform:none/, 'no sideways move on hover');
    assert.match(css, /\.trap-popover\.trapline-grid\.trap-item-icon-img\{[^}]*width:18px;height:18px/, 'an 18px icon');
    assert.match(css, /\.trapline-empty-cell\{[^}]*border:1pxsolidvar\(--tl-cell-border\)[^}]*background:var\(--tl-cell-bg\)/, 'the empty cell look');
  });

  test('jsdom: only a prey cell has the gold marker', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    // Status 1 prey, 0 armed, 2 no bait.
    await postTraps(coreWindow, [trap(1, 1), trap(2, 0), trap(3, 2)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, ONE_FLOOR_THREE);

    const doc = coreWindow.document;
    const css = styleText(doc);
    assert.match(css, /\.trap-popover\.trapline-grid\.trap-item-row\.is-prey\{[^}]*border-color:rgb\(fromvar\(--tl-gold\)rgb\/90%\)[^}]*box-shadow:/, 'a gold border and glow on a prey cell');
    assert.match(css, /\.trap-popover\.trapline-grid\.trap-item-row\.is-prey::after\{[^}]*width:5px;height:5px/, 'a gold dot on a prey cell');

    // jsdom computes no color from var() and the relative color syntax, so this checks which game rows
    // the marker rule selects.
    const marker = '.trap-popover.trapline-grid .trap-item-row.is-prey';
    assert.ok(doc.querySelector('.trap-item-row.is-prey').matches(marker), 'the marker rule selects a prey cell');
    assert.ok(!doc.querySelector('.trap-item-row.is-armed').matches(marker), 'the marker rule does not select an armed cell');
    assert.ok(!doc.querySelector('.trap-item-row.is-nobait').matches(marker), 'a no-bait cell looks the same as an armed cell');
  });

  test('jsdom: a Stopped Auto Trap Cage has the red marker and no gold marker', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    // Status 3 Stopped, 1 prey, 0 armed.
    await postTraps(coreWindow, [trap(1, 3), trap(2, 1), trap(3, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, ONE_FLOOR_THREE);

    const doc = coreWindow.document;
    const css = styleText(doc);
    assert.match(css, /\.trap-popover\.trapline-grid\.trap-item-row\.is-halted\{[^}]*border-color:rgb\(fromvar\(--tl-red\)rgb\/90%\)[^}]*box-shadow:/, 'a red border and glow on a Stopped cell');
    assert.match(css, /\.trap-popover\.trapline-grid\.trap-item-row\.is-halted::after\{[^}]*width:5px;height:5px[^}]*background:rgb\(fromvar\(--tl-red\)/, 'a red dot on a Stopped cell');
    assert.match(css, /--tl-red:#e57373;/, 'the red token, the color of the game\'s is-halted border');

    const red = '.trap-popover.trapline-grid .trap-item-row.is-halted';
    const gold = '.trap-popover.trapline-grid .trap-item-row.is-prey';
    const halted = doc.querySelector('.trap-item-row.is-halted');
    assert.ok(halted.matches(red), 'the red marker rule selects a Stopped cell');
    assert.ok(!halted.matches(gold), 'the gold marker rule does not select a Stopped cell');
    assert.ok(!doc.querySelector('.trap-item-row.is-prey').matches(red), 'the red marker rule does not select a prey cell');
    assert.ok(!doc.querySelector('.trap-item-row.is-armed').matches(red), 'the red marker rule does not select an armed cell');
  });

  test('jsdom: the game prey count is a badge in the corner of its cell, and one prey has no badge', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    // Trap 1: an Auto Trap Cage with 4 prey. Trap 2: one prey, so the game's v-if adds no count.
    await postTraps(coreWindow, [trap(1, 1, '', 4), trap(2, 1, '', 1), trap(3, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, ONE_FLOOR_THREE);

    const doc = coreWindow.document;
    const counts = [...doc.querySelectorAll('.trap-item-count')];
    assert.equal(counts.length, 1, 'only the trap with more than one prey has a count');
    const badge = counts[0];
    assert.equal(badge.textContent, '×4', 'the game\'s own text');

    const badgeRule = '.trap-popover.trapline-grid .trap-item-count';
    assert.ok(badge.matches(badgeRule), 'the badge rule selects the count in a grid cell');
    const badgeStyle = coreWindow.getComputedStyle(badge);
    assert.equal(badgeStyle.position, 'absolute', 'the badge is out of the flow of the cell, so the icon stays in place');
    assert.equal(badgeStyle.right, '0px');
    assert.equal(badgeStyle.bottom, '0px');
    assert.match(styleText(doc), /\.trap-popover\.trapline-grid\.trap-item-count\{[^}]*background:var\(--tl-badge-bg\)/, 'a dark background from the tokens');

    const row = badge.closest('.trap-item-row');
    const rowStyle = coreWindow.getComputedStyle(row);
    assert.equal(rowStyle.width, '22px', 'the badge does not make the cell wider');
    assert.equal(rowStyle.height, '22px', 'the badge does not make the cell higher');
  });

  test('jsdom: an empty cell is a HUD area, and a click on it is no trap click', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0), trap(2, 0), trap(3, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, ONE_FLOOR_THREE);

    const empty = coreWindow.document.querySelector('.trapline-empty-cell');
    assert.ok(empty.hasAttribute('data-interactive'), 'the HUD must count an empty cell as its own area, or the click goes to the world');

    const inCore: Win[] = [];
    coreWindow.addEventListener('message', (e: Win) => inCore.push(e.data));
    empty.dispatchEvent(new coreWindow.MouseEvent('click', { bubbles: true }));
    await wait(coreWindow, 30);

    assert.equal(inCore.filter((m) => m.type === 'TRAP_HUD_CLICK').length, 0, 'no trap click');
  });

  test('jsdom: a trap whose next pickup breaks it shows a red danger cell over its cell', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    // Trap 1 with 4 prey in its storage box and the mark 25, trap 2 with no mark.
    await postTraps(coreWindow, [trap(1, 1, '', 4), trap(2, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, { cols: 2, rows: 2, lines: [{ key: 'f1', kind: 'floor', row: 1, name: 'Home' }], cells: [{ slot: 'a', row: 2, col: 1, trap: 1, mark: 25 }, cell('b', 2, 2, 2)] });

    const doc = coreWindow.document;
    const marks = [...doc.querySelectorAll('.trapline-mark')];
    assert.equal(marks.length, 1, 'only the trap with a mark has a mark node');
    assert.equal(marks[0].textContent, '', 'no text: the cell is the mark');
    assert.equal(marks[0].getAttribute('data-percent'), '25', 'the durability percent for the game driver');
    assert.equal(marks[0].getAttribute('data-trap'), '1');
    assert.equal(place(marks[0]), '2|1', 'the mark is in the cell of its trap');
    assert.ok(!doc.querySelector('.trap-item-row').contains(marks[0]), 'the mark is not a child of the game row, so Vue keeps its row');

    const css = styleText(doc);
    const rule = css.match(/\.trap-popover\.trapline-grid\.trapline-mark\{([^}]*)\}/);
    assert.ok(rule, 'a rule for the mark');
    for (const decl of ['width:22px', 'height:22px', 'border:1pxsolidvar(--tl-mark)', 'border-radius:4px', 'background:rgb(fromvar(--tl-mark)rgb/16%)', 'pointer-events:none']) {
      assert.ok(rule[1].includes(decl), `the mark rule has ${decl}`);
    }
    // The gold marker (top right) and the count badge (bottom right) of the row stay as they were.
    const row = doc.querySelectorAll('.trap-item-row')[0];
    assert.equal(coreWindow.getComputedStyle(row).width, '22px', 'the mark does not make the cell wider');
    assert.equal(coreWindow.getComputedStyle(row.querySelector('.trap-item-count')).position, 'absolute');
  });

  test('jsdom: a mark goes when its trap has no mark in the next data, and a second draw writes nothing', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    const marked = { cols: 1, rows: 2, lines: [], cells: [{ slot: 'a', row: 2, col: 1, trap: 1, mark: 40 }] };
    setGroups(root, marked);

    const popover = coreWindow.document.querySelector('.trap-popover');
    const watcher = new coreWindow.MutationObserver(() => {});
    watcher.observe(popover, { childList: true, subtree: true, attributes: true, characterData: true });
    setGroups(root, marked);
    assert.equal(watcher.takeRecords().length, 0, 'no write for the same mark');
    watcher.disconnect();

    setGroups(root, { cols: 1, rows: 2, lines: [], cells: [cell('a', 2, 1, 1)] });
    assert.equal(coreWindow.document.querySelector('.trapline-mark'), null);
  });

  // The data of a trap card, as PageJson.CardJson gives it.
  function cardData(trapId: number, over: Record<string, unknown> = {}) {
    return {
      trap: trapId, name: 'Live Capture Cage', cur: 600, max: 1000, fill: 60, band: '',
      bait: true, baitIcon: '../../Res/bait.png', prey: false, preyIcon: '',
      texts: { durability: 'Durability', baitSet: 'Bait Set', noBait: 'No Bait', preyReady: 'Ready to Take', noPrey: 'No Prey' },
      ...over,
    };
  }

  function setCard(root: Win, data: unknown): string {
    return runPageJs(root, 'window.__trapline.setCard(' + JSON.stringify(data) + ')');
  }

  function enter(coreWindow: Win, el: Win): void {
    el.dispatchEvent(new coreWindow.MouseEvent('mouseenter', { bubbles: false }));
  }

  function leave(coreWindow: Win, el: Win): void {
    el.dispatchEvent(new coreWindow.MouseEvent('mouseleave', { bubbles: false, relatedTarget: coreWindow.document.body }));
  }

  async function gridWithCard(t: TestContext): Promise<{ coreWindow: Win; root: Win; received: Win[] }> {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0), trap(2, 0), trap(3, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, TWO_FLOORS);
    const received: Win[] = [];
    root.addEventListener('message', (e: Win) => received.push(e.data));
    return { coreWindow, root, received };
  }

  test('jsdom: the pointer on a trap cell asks for its card once, and a move on the cell asks nothing', async (t) => {
    const { coreWindow, root, received } = await gridWithCard(t);
    const row = coreWindow.document.querySelectorAll('.trap-item-row')[1];
    enter(coreWindow, row);
    row.dispatchEvent(new coreWindow.MouseEvent('mousemove', { bubbles: true }));
    row.dispatchEvent(new coreWindow.MouseEvent('mousemove', { bubbles: true }));
    await wait(root, 30);

    const asks = received.filter((m) => m.type === 'TRAPLINE_CARD');
    assert.equal(asks.length, 1, 'one ask for the cell under the pointer');
    assert.deepEqual({ ...asks[0].data }, { trap: 2 });
    assert.equal(asks[0].sourcePageId, 'CoreUI1');
  });

  test('jsdom: the card shows the content of the game card at the cell, and hides when the pointer leaves', async (t) => {
    const { coreWindow, root } = await gridWithCard(t);
    const doc = coreWindow.document;
    const row = doc.querySelectorAll('.trap-item-row')[0];
    enter(coreWindow, row);
    assert.equal(setCard(root, cardData(1)), 'ok');

    const hover = doc.querySelector('.trapline-hover');
    assert.ok(hover && !hover.hidden, 'the card shows');
    assert.ok(hover.hasAttribute('data-no-sync'), 'the card is not a HUD area that CoreUI1 reports to the game');
    const tip = hover.querySelector('.entry-tip.trapline-tip');
    assert.ok(tip, 'the frame of the game tooltip, as the pot card of Greenline');
    assert.equal(tip.querySelector('.trapline-card-name').textContent, 'Live Capture Cage');
    assert.equal(tip.querySelector('.trapline-card-dura-label').textContent, 'Durability');
    assert.equal(tip.querySelector('.trapline-card-dura-value').textContent, '600 / 1000');
    const fill = tip.querySelector('.trapline-card-fill');
    assert.equal(fill.style.width, '60%');
    assert.ok(!fill.classList.contains('medium') && !fill.classList.contains('low'), 'green above 0.5');
    const items = [...tip.querySelectorAll('.trapline-card-status')];
    assert.equal(items.length, 2);
    assert.equal(items[0].querySelector('img').getAttribute('src'), '../../Res/bait.png');
    assert.equal(items[0].querySelector('.trapline-card-status-text').textContent, 'Bait Set');
    assert.ok(items[0].classList.contains('active'));
    assert.equal(items[1].querySelector('img'), null, 'no prey icon');
    assert.equal(items[1].querySelector('.trapline-card-placeholder').textContent, '\u{1F43E}', 'the game symbol of no prey');
    assert.equal(items[1].querySelector('.trapline-card-status-text').textContent, 'No Prey');
    assert.ok(!items[1].classList.contains('active'));

    leave(coreWindow, row);
    assert.ok(hover.hidden, 'the card hides when the pointer leaves the cell');
  });

  test('jsdom: a card for a cell that the pointer left already does not show', async (t) => {
    const { coreWindow, root } = await gridWithCard(t);
    const doc = coreWindow.document;
    const rows = doc.querySelectorAll('.trap-item-row');
    enter(coreWindow, rows[0]);
    leave(coreWindow, rows[0]);
    enter(coreWindow, rows[1]);
    setCard(root, cardData(1));
    const hover = doc.querySelector('.trapline-hover');
    assert.ok(!hover || hover.hidden, 'the card of trap 1 does not show over trap 2');
    setCard(root, cardData(2, { cur: 150, fill: 15, band: 'low' }));
    assert.ok(!doc.querySelector('.trapline-hover').hidden);
    assert.ok(doc.querySelector('.trapline-card-fill').classList.contains('low'), 'red at 0.2 or less');
  });

  test('jsdom: the card goes left of the cell at the right edge of the window, with the arrow on its right side', async (t) => {
    const { coreWindow, root } = await gridWithCard(t);
    const doc = coreWindow.document;
    const row = doc.querySelectorAll('.trap-item-row')[0];
    const right = coreWindow.innerWidth - 4;
    row.getBoundingClientRect = () => ({ left: right - 22, right, top: 100, bottom: 122, width: 22, height: 22, x: right - 22, y: 100 });
    enter(coreWindow, row);
    setCard(root, cardData(1));
    const hover = doc.querySelector('.trapline-hover');
    assert.ok(hover.classList.contains('trapline-hover-left'));

    const near = doc.querySelectorAll('.trap-item-row')[1];
    near.getBoundingClientRect = () => ({ left: 10, right: 32, top: 100, bottom: 122, width: 22, height: 22, x: 10, y: 100 });
    leave(coreWindow, row);
    enter(coreWindow, near);
    setCard(root, cardData(2));
    assert.ok(!hover.classList.contains('trapline-hover-left'), 'right of the cell with room to the right');
    // jsdom gives the card no height: its middle (its top) and the arrow are at the middle of the cell.
    assert.equal(hover.style.top, '111px');
    assert.equal(hover.style.left, '42px', 'the gap of 10px right of the cell');
    assert.equal(hover.style.getPropertyValue('--tl-arrow-y'), '0px');
  });

  test('jsdom: the card takes no click and lies above the HUD panels with the arrow of the pot card of Greenline', async (t) => {
    const { coreWindow } = await gridWithCard(t);
    const css = styleText(coreWindow.document);
    assert.match(css, /\.trapline-hover\.has-entry-tip\{[^}]*position:fixed[^}]*z-index:9999[^}]*pointer-events:none/);
    assert.match(css, /\.trapline-hover::before\{[^}]*top:calc\(var\(--tl-arrow-y,50%\)-5px\)[^}]*transform:rotate\(45deg\)/);
    assert.match(css, /\.trapline-hover\.trapline-hover-left::before\{[^}]*right:-5px/);
    assert.match(css, /\.entry-tip\.trapline-tip\{[^}]*background:rgb\(fromvar\(--tl-tip\)rgb\/96%\)/);
  });

  const WORDS = { takeAll: 'Take All', freeSlot: 'Free trap slot', freeSlotHint: 'Click: set a trap' };

  async function gridWithMenu(t: TestContext): Promise<{ coreWindow: Win; root: Win; received: Win[] }> {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0), trap(2, 0), trap(3, 0)]);
    const root = makeRootWindow(t, coreWindow);
    setData(root, { words: WORDS, groups: null });
    await expand(coreWindow);
    setGroups(root, TWO_FLOORS);
    const received: Win[] = [];
    root.addEventListener('message', (e: Win) => received.push(e.data));
    return { coreWindow, root, received };
  }

  function setMenu(root: Win, data: unknown): string {
    return runPageJs(root, 'window.__trapline.setMenu(' + JSON.stringify(data) + ')');
  }

  const MENU = {
    slot: 'h2',
    lines: [
      { kind: 2, icon: '../../Res/cage.png', name: 'Live Capture Cage', count: 3 },
      { kind: 4, icon: '../../Res/iron.png', name: 'Iron Animal Trap', count: 2 },
    ],
  };

  test('jsdom: a free trap slot cell shows the plus and the card of a free trap slot, and asks C# nothing', async (t) => {
    const { coreWindow, received } = await gridWithMenu(t);
    const doc = coreWindow.document;
    const empty = emptyCell(doc, 'h2');
    enter(coreWindow, empty);
    const hover = doc.querySelector('.trapline-hover');
    assert.ok(hover && !hover.hidden, 'the card shows');
    assert.equal(hover.querySelector('.trapline-card-name').textContent, 'Free trap slot');
    assert.equal(hover.querySelector('.trapline-card-hint').textContent, 'Click: set a trap');
    assert.equal(received.filter((m) => m.type === 'TRAPLINE_CARD').length, 0);
    assert.match(styleText(doc), /\.trapline-empty-cell:hover::before\{[^}]*content:"\+"/, 'the plus on hover');
    leave(coreWindow, empty);
    assert.ok(hover.hidden);
  });

  test('jsdom: a click on a free trap slot cell asks for its Set Trap menu, which opens under the cell', async (t) => {
    const { coreWindow, root, received } = await gridWithMenu(t);
    const doc = coreWindow.document;
    const empty = emptyCell(doc, 'h2');
    empty.dispatchEvent(new coreWindow.MouseEvent('click', { bubbles: true }));
    await wait(root, 30);
    const asks = received.filter((m) => m.type === 'TRAPLINE_MENU');
    assert.equal(asks.length, 1);
    assert.deepEqual({ ...asks[0].data }, { slot: 'h2' });

    assert.equal(setMenu(root, MENU), 'ok');
    const list = doc.querySelector('.trapline-menu-anchored');
    assert.ok(list, 'the menu shows');
    const options = [...list.querySelectorAll('.trapline-menu-opt')];
    assert.deepEqual(options.map((o: Win) => o.querySelector('.trapline-menu-text').textContent), ['Live Capture Cage', 'Iron Animal Trap']);
    assert.deepEqual(options.map((o: Win) => o.querySelector('.trapline-menu-count').textContent), ['3', '2']);
    assert.equal(options[0].querySelector('img').getAttribute('src'), '../../Res/cage.png');
    const hover = doc.querySelector('.trapline-hover');
    assert.ok(!hover || hover.hidden, 'the card of the cell hides while the menu is open');
  });

  test('jsdom: a line of the menu sets a trap of its kind on the slot and closes the menu', async (t) => {
    const { coreWindow, root, received } = await gridWithMenu(t);
    const doc = coreWindow.document;
    emptyCell(doc, 'h2').dispatchEvent(new coreWindow.MouseEvent('click', { bubbles: true }));
    setMenu(root, MENU);
    (doc.querySelectorAll('.trapline-menu-opt')[1] as Win).click();
    await wait(root, 30);
    const sets = received.filter((m) => m.type === 'TRAPLINE_SET');
    assert.equal(sets.length, 1);
    assert.deepEqual({ ...sets[0].data }, { slot: 'h2', trap: 4 });
    assert.equal(doc.querySelector('.trapline-menu-anchored'), null);
  });

  test('jsdom: with no trap no menu opens, and an open menu of another cell closes', async (t) => {
    const { coreWindow, root } = await gridWithMenu(t);
    const doc = coreWindow.document;
    setMenu(root, MENU);
    assert.ok(doc.querySelector('.trapline-menu-anchored'));
    assert.equal(setMenu(root, { slot: 'h2', lines: [] }), 'ok');
    assert.equal(doc.querySelector('.trapline-menu-anchored'), null);
    assert.equal(doc.querySelectorAll('.trapline-menu-opt').length, 0);
  });

  test('jsdom: a click on a trap cell closes the menu, and the trap click works as without the menu', async (t) => {
    const { coreWindow, root } = await gridWithMenu(t);
    const doc = coreWindow.document;
    emptyCell(doc, 'h2').dispatchEvent(new coreWindow.MouseEvent('click', { bubbles: true }));
    setMenu(root, MENU);
    const inCore: Win[] = [];
    coreWindow.addEventListener('message', (e: Win) => inCore.push(e.data));
    doc.querySelectorAll('.trap-item-row')[0].dispatchEvent(new coreWindow.MouseEvent('click', { bubbles: true }));
    await wait(coreWindow, 30);
    assert.equal(doc.querySelector('.trapline-menu-anchored'), null);
    assert.equal(inCore.filter((m) => m.type === 'TRAP_HUD_CLICK').length, 1);
  });

  test('jsdom: the cell of the open menu shows the plus and a gold border until the menu closes', async (t) => {
    const { coreWindow, root } = await gridWithMenu(t);
    const doc = coreWindow.document;
    setMenu(root, MENU);
    assert.ok(emptyCell(doc, 'h2').classList.contains('trapline-menu-cell'));
    assert.match(styleText(doc), /\.trapline-empty-cell\.trapline-menu-cell::before,[^{]*\{[^}]*content:"\+"/, 'the plus while the menu is open');
    assert.match(styleText(doc), /\.trapline-empty-cell\.trapline-menu-cell\{[^}]*border-color:var\(--tl-gold\)/, 'a gold border');
    (doc.querySelectorAll('.trapline-menu-opt')[0] as Win).click();
    await wait(root, 30);
    assert.ok(!emptyCell(doc, 'h2').classList.contains('trapline-menu-cell'), 'no mark after the close');
  });

  test('jsdom: the menu closes when the pointer leaves the trap list and the menu, and stays open on the way to the menu', async (t) => {
    const { coreWindow, root } = await gridWithMenu(t);
    const doc = coreWindow.document;
    setMenu(root, MENU);
    const popover = doc.querySelector('.trap-popover');
    const list = doc.querySelector('.trapline-menu-anchored');
    popover.dispatchEvent(new coreWindow.MouseEvent('mouseleave'));
    list.dispatchEvent(new coreWindow.MouseEvent('mouseenter'));
    await wait(coreWindow, 500);
    assert.ok(doc.querySelector('.trapline-menu-anchored'), 'open: the pointer went from the list to the menu');
    list.dispatchEvent(new coreWindow.MouseEvent('mouseleave'));
    await wait(coreWindow, 500);
    assert.equal(doc.querySelector('.trapline-menu-anchored'), null, 'closed: the pointer is out of both');
    assert.ok(!emptyCell(doc, 'h2').classList.contains('trapline-menu-cell'));
  });

  test('jsdom: a menu for a slot with no free cell does not open', async (t) => {
    const { coreWindow, root } = await gridWithMenu(t);
    setMenu(root, { ...MENU, slot: 'gone' });
    assert.equal(coreWindow.document.querySelector('.trapline-menu-anchored'), null);
  });

  const LAST_WORDS = { takeAll: 'Take All', autoReplace: 'Auto-replace', autoReplaceTip: 'A broken trap is replaced by a trap of the same kind from the backpack or home storage.' };

  async function gridWithLastLine(t: TestContext, replace: boolean): Promise<{ coreWindow: Win; root: Win; received: Win[] }> {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0), trap(2, 0), trap(3, 0)]);
    const root = makeRootWindow(t, coreWindow);
    await expand(coreWindow);
    setData(root, { words: LAST_WORDS, groups: TWO_FLOORS, replace });
    const received: Win[] = [];
    root.addEventListener('message', (e: Win) => received.push(e.data));
    return { coreWindow, root, received };
  }

  test('jsdom: the last line of the open list has the Auto-replace checkbox with its tip, and a free place at its right end', async (t) => {
    const { coreWindow } = await gridWithLastLine(t, false);
    const doc = coreWindow.document;
    const last = doc.querySelector('.trap-popover > .trapline-last');
    assert.ok(last, 'the last line');
    assert.equal(place(last), '5|', 'the row after the cells (4 rows)');
    assert.equal(last.style.getPropertyValue('grid-column') || coreWindow.getComputedStyle(last).gridColumn, '1 / -1');
    const label = last.querySelector('.trapline-replace');
    assert.ok(label.hasAttribute('data-interactive'));
    assert.equal(label.querySelector('.trapline-replace-text').textContent, 'Auto-replace');
    assert.equal(label.querySelector('.entry-tip').textContent, LAST_WORDS.autoReplaceTip);
    assert.equal(label.querySelector('input').checked, false);
    assert.ok(last.querySelector(':scope > .trapline-last-end'), 'the free place at the right end');
  });

  test('jsdom: the checkbox follows the data, and a click posts its new state', async (t) => {
    const { coreWindow, root, received } = await gridWithLastLine(t, true);
    const doc = coreWindow.document;
    const box = doc.querySelector('.trapline-replace input');
    assert.equal(box.checked, true);
    box.click();
    await wait(root, 30);
    const posts = received.filter((m) => m.type === 'TRAPLINE_REPLACE');
    assert.equal(posts.length, 1);
    assert.deepEqual({ ...posts[0].data }, { on: false });
    assert.equal(coreWindow.eval('state.trapsExpanded'), true, 'the click does not close the list');
  });

  test('jsdom: the last line goes with the grid', async (t) => {
    const { coreWindow, root } = await gridWithLastLine(t, false);
    setGroups(root, null);
    assert.equal(coreWindow.document.querySelector('.trapline-last'), null);
  });

  async function gridWithPin(t: TestContext, pin: boolean): Promise<{ coreWindow: Win; root: Win; received: Win[] }> {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0), trap(2, 0), trap(3, 0)]);
    const root = makeRootWindow(t, coreWindow);
    await expand(coreWindow);
    setData(root, { words: LAST_WORDS, groups: TWO_FLOORS, replace: false, pin });
    await wait(coreWindow, 30);
    const received: Win[] = [];
    root.addEventListener('message', (e: Win) => received.push(e.data));
    return { coreWindow, root, received };
  }

  test('jsdom: the pin is at the right end of the last line, white, and an outline when not pinned', async (t) => {
    const { coreWindow, root, received } = await gridWithPin(t, false);
    const doc = coreWindow.document;
    const pin = doc.querySelector('.trapline-last > .trapline-last-end > .tl-pin');
    assert.ok(pin, 'the pin in the free place at the right end of the last line');
    assert.ok(!pin.classList.contains('tl-pin-on'));
    assert.match(styleText(doc), /--tl-pin-color:var\(--tl-white\)/, 'a white pin');
    assert.match(styleText(doc), /\.trapline-last-end\{[^}]*font-size:9px/, 'a pin about the 11px of the checkbox (1.2em of 9px)');
    assert.match(styleText(doc), /\.tl-pin\.tl-pin-on\.tl-pin-head\{fill:currentColor;?\}/, 'the head filled when pinned');
    pin.click();
    await wait(root, 30);
    const posts = received.filter((m) => m.type === 'TRAPLINE_PIN');
    assert.equal(posts.length, 1);
    assert.deepEqual({ ...posts[0].data }, { on: true });
    assert.equal(coreWindow.eval('state.trapsExpanded'), true, 'the click does not close the list');
  });

  test('jsdom: a pinned list stays open when the game closes it for the plant list, the search, or Escape', async (t) => {
    const { coreWindow } = await gridWithPin(t, true);
    const doc = coreWindow.document;
    assert.ok(doc.querySelector('.tl-pin').classList.contains('tl-pin-on'), 'a filled pin');
    // The handlers of the plant list header and the search toggle set the field as the game does.
    coreWindow.eval('state.maturePlantsExpanded = true; state.searchExpanded = false; state.trapsExpanded = false');
    assert.equal(coreWindow.eval('state.trapsExpanded'), true, 'open after the plant list opens');
    coreWindow.eval('state.searchExpanded = true; state.trapsExpanded = false');
    assert.equal(coreWindow.eval('state.trapsExpanded'), true, 'open after the search opens');
    coreWindow.document.dispatchEvent(new coreWindow.KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    coreWindow.dispatchEvent(new coreWindow.KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    await wait(coreWindow, 30);
    assert.equal(coreWindow.eval('state.trapsExpanded'), true, 'open after Escape');
    assert.ok(doc.querySelector('.trap-popover'), 'the list shows');
  });

  test('jsdom: a click on the chevron of a pinned list unpins it and the list closes', async (t) => {
    const { coreWindow, root, received } = await gridWithPin(t, true);
    const doc = coreWindow.document;
    doc.querySelector('.trap-toggle-chevron').dispatchEvent(new coreWindow.MouseEvent('click', { bubbles: true }));
    await wait(root, 30);
    assert.deepEqual(received.filter((m) => m.type === 'TRAPLINE_PIN').map((m) => ({ ...m.data })), [{ on: false }]);
    assert.equal(coreWindow.eval('state.trapsExpanded'), false, 'the list closes');
  });

  test('jsdom: a pinned list shows open again after the game rebuilt its frame', async (t) => {
    const { root } = await gridWithPin(t, true);
    const fresh = await loadCoreWindow(t);
    await postTraps(fresh, [trap(1, 0)]);
    root.__setCoreFrame(fresh);
    assert.equal(fresh.eval('state.trapsExpanded'), false, 'a new frame starts closed');
    setData(root, { words: LAST_WORDS, groups: TWO_FLOORS, replace: false, pin: true });
    assert.equal(fresh.eval('state.trapsExpanded'), true, 'the pin opens the list');
  });

  test('jsdom: a missing chevron turns off only the pin and reports it', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 1), trap(2, 0), trap(3, 0)]);
    const root = makeRootWindow(t, coreWindow);
    await expand(coreWindow);
    coreWindow.document.querySelector('.trap-toggle-chevron').classList.remove('trap-toggle-chevron');
    const result = setData(root, { words: LAST_WORDS, groups: TWO_FLOORS, replace: false, pin: true });
    assert.match(result, /missing: .*pin\(\.trap-toggle-chevron\)/);
    assert.equal(coreWindow.document.querySelector('.tl-pin'), null);
    assert.ok(coreWindow.document.querySelector('.trapline-last'), 'the last line still shows');
    coreWindow.eval('state.trapsExpanded = false');
    assert.equal(coreWindow.eval('state.trapsExpanded'), false, 'the list closes as without the pin');
  });

  for (const part of ['.trap-item-name', '.trap-item-status', '.trap-item-icon-img']) {
    test(`jsdom: a renamed ${part} turns off the grid and is reported`, async (t) => {
      const coreWindow = await loadCoreWindow(t);
      await postTraps(coreWindow, [trap(1, 0)]);
      const root = makeRootWindow(t, coreWindow);
      install(root);
      await expand(coreWindow);

      const doc = coreWindow.document;
      doc.querySelector(part).classList.remove(part.slice(1));
      const result = setGroups(root, ONE_CELL);

      assert.ok(result.includes('missing: groups(' + part + ')'), `expected a missing-part report, got "${result}"`);
      assert.ok(!doc.querySelector('.trap-popover').classList.contains('trapline-grid'), 'no grid');
    });
  }

  test('jsdom: a collapse keeps the grid while the popover is only leaving', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0, 'Balcony'), trap(2, 0), trap(3, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, TWO_FLOORS);

    coreWindow.eval('state.trapsExpanded = false');

    // The leaving `.trap-popover` stays in the DOM for a short, environment-dependent stretch (Vue's
    // leave transition) before Vue removes it outright, so this polls for that window instead of
    // trusting one fixed wait.
    let popover = null;
    for (let i = 0; i < 20 && !popover; i++) {
      await wait(coreWindow, 5);
      popover = coreWindow.document.querySelector('.trap-popover');
    }
    assert.ok(popover, 'expected the leaving .trap-popover still in the DOM to check');

    assert.ok(popover.classList.contains('trapline-grid'), 'the grid class stays while the popover is only leaving');
    assert.equal(place(popover.querySelectorAll('.trap-item-row')[1]), '4|2', 'the placement stays');
    assert.equal(coreWindow.getComputedStyle(popover.querySelector('.trap-item-room')).display, 'none', 'the room name stays hidden');
  });

  // Everything the grid adds to the popover, so a test can check that all of it is gone.
  function assertGridGone(coreWindow: Win): void {
    const doc = coreWindow.document;
    const popover = doc.querySelector('.trap-popover') || doc.querySelector('.trap-item-row').parentNode;
    assert.ok(!popover.classList.contains('trapline-grid'), 'no grid class');
    assert.ok(!popover.classList.contains('tl-fg'), 'no class of the floor grid library');
    assert.equal(popover.style.getPropertyValue('--tl-fg-cols'), '', 'no column count');
    assert.equal(popover.style.getPropertyValue('--tl-fg-tracks'), '', 'no column template');
    for (const row of doc.querySelectorAll('.trap-item-row')) assert.equal(place(row), '|', 'no grid placement on a row');
    assert.equal(doc.querySelector('.tl-fg-line'), null, 'no line');
    assert.equal(doc.querySelector('.trapline-empty-cell'), null, 'no empty cell');
    assert.equal(doc.querySelector('.trapline-mark'), null, 'no durability mark');
    const room = doc.querySelector('.trap-item-room');
    assert.notEqual(coreWindow.getComputedStyle(room).display, 'none', 'the room name shows again');
  }

  test('jsdom: the grid turns off fully when there is no group data', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0, 'Balcony'), trap(2, 0), trap(3, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, TWO_FLOORS);

    const result = setGroups(root, null);
    assert.equal(result, 'installed', `setGroups result was "${result}"`);
    assertGridGone(coreWindow);
  });

  test('jsdom: the grid turns off fully when a required part goes missing', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0, 'Balcony'), trap(2, 0), trap(3, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, TWO_FLOORS);

    // Simulates a game update that renamed the status class of the rows; .trap-popover is untouched.
    for (const el of coreWindow.document.querySelectorAll('.trap-item-status')) el.classList.remove('trap-item-status');

    const result = setGroups(root, TWO_FLOORS);
    assert.match(result, /missing: groups\(\.trap-item-status\)/, `expected a missing-part report, got "${result}"`);
    assertGridGone(coreWindow);
  });

  test('jsdom: a class not in the page text turns off only the button and reports it as missing', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 1)]);
    const root = makeRootWindow(t, coreWindow);

    // Simulates a game update that renamed the chevron's class.
    coreWindow.document.querySelector('.trap-toggle-chevron').classList.remove('trap-toggle-chevron');

    const result = install(root);
    assert.match(result, /missing: button\(\.trap-toggle-chevron\)/, `expected a missing-part report, got "${result}"`);
    assert.equal(coreWindow.document.querySelector('.trapline-take-all'), null, 'no button should be added when a required part is missing');
    // The row itself (and the rest of the page) is unaffected by the missing chevron.
    assert.ok(coreWindow.document.querySelector('.trap-toggle-row'), 'the header row itself should be untouched');
  });

  function check(root: Win): string {
    return runPageJs(root, 'window.__trapline.check()');
  }

  // The runs of the observer and the apply passes that check() reports.
  function counts(result: string): { runs: number; passes: number } {
    const m = /^ok (\d+) (\d+)$/.exec(result);
    assert.ok(m, `expected "ok <runs> <passes>", got "${result}"`);
    return { runs: Number(m[1]), passes: Number(m[2]) };
  }

  test('jsdom: setData shows the word and the groups in one apply pass', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 1), trap(2, 0), trap(3, 0)]);
    const root = makeRootWindow(t, coreWindow);
    await expand(coreWindow);

    const result = setData(root, { words: { takeAll: 'Take All' }, groups: TWO_FLOORS });
    assert.equal(result, 'installed', `setData result was "${result}"`);
    assert.equal(counts(check(root)).passes, 1, 'one setData is one apply pass');

    const doc = coreWindow.document;
    assert.equal(doc.querySelector('.trap-toggle-row .trapline-take-all').textContent, 'Take All');
    assert.ok(doc.querySelector('.trap-popover').classList.contains('trapline-grid'));
    assert.equal(place(doc.querySelectorAll('.trap-item-row')[1]), '4|2');
  });

  test('jsdom: setData with no CoreUI1 frame schedules no retry', async (t) => {
    const root = makeRootWindow(t, null);
    let timers = 0;
    const realSetTimeout = root.setTimeout;
    root.setTimeout = (...args: unknown[]) => { timers++; return realSetTimeout(...args); };

    const result = setData(root, { words: { takeAll: 'Take All' }, groups: TWO_FLOORS });
    assert.equal(result, 'no CoreUI1 frame');
    assert.equal(timers, 0, 'the page has no retry loop: C# retries');
  });

  test('jsdom: check() reports the observer runs and the passes since the last check', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 1)]);
    const root = makeRootWindow(t, coreWindow);
    setData(root, { words: { takeAll: 'Take All' }, groups: null });
    setData(root, { words: { takeAll: 'Take All' }, groups: null });

    const first = counts(check(root));
    assert.equal(first.passes, 2);

    // A Vue update in the frame makes the observer run.
    await postTraps(coreWindow, [trap(1, 1), trap(2, 1)]);
    const second = counts(check(root));
    assert.equal(second.passes, 0, 'the counts start again after each check');
    assert.ok(second.runs >= 1, `expected an observer run after a Vue update, got ${second.runs}`);
  });

  test('jsdom: check() is not ok while the frame has no observer', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    const root = makeRootWindow(t, coreWindow);
    const result = check(root);
    assert.ok(!result.startsWith('ok'), `expected a result that is not ok, got "${result}"`);
  });

  test('jsdom: check() with no CoreUI1 frame is not ok', async (t) => {
    const root = makeRootWindow(t, null);
    assert.equal(check(root), 'no CoreUI1 frame');
  });

  const CSS_DIR = path.join(HERE, '..', '..', 'src', 'Web');

  test('jsdom: the style node holds the tokens of tokens.css and the rules of page.css', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 1)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);

    const css = styleText(coreWindow.document);
    const tokens = fs.readFileSync(path.join(CSS_DIR, 'tokens.css'), 'utf8');
    const rules = fs.readFileSync(path.join(CSS_DIR, 'page.css'), 'utf8');
    const names = [...tokens.matchAll(/(--tl-[\w-]+)\s*:/g)].map((m) => m[1]);
    assert.ok(names.length > 0, 'tokens.css defines --tl- tokens');
    for (const name of names) assert.ok(css.includes(name + ':'), `the style node defines ${name}`);
    const selectors = [...rules.replace(/\/\*[\s\S]*?\*\//g, '').matchAll(/([^{}]+)\{/g)].map((m) => m[1].replace(/\s+/g, ''));
    assert.ok(selectors.length > 0, 'page.css has rules');
    for (const sel of selectors) assert.ok(css.includes(sel + '{'), `the style node has the rule ${sel}`);
  });

  test('jsdom: a game node with its own grid place keeps it after the grid turns off', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0, 'Balcony'), trap(2, 0), trap(3, 0)]);
    const doc = coreWindow.document;
    const gameNode = doc.createElement('div');
    gameNode.style.setProperty('grid-row', '3');
    gameNode.style.setProperty('grid-column', '2');
    doc.body.appendChild(gameNode);
    const root = makeRootWindow(t, coreWindow);
    await expand(coreWindow);
    setGroups(root, TWO_FLOORS);

    setGroups(root, null);
    assertGridGone(coreWindow);
    assert.equal(gameNode.style.getPropertyValue('grid-row'), '3', 'the game node keeps its grid-row');
    assert.equal(gameNode.style.getPropertyValue('grid-column'), '2', 'the game node keeps its grid-column');
  });
}
