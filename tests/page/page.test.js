// Runs page.js against the game's own CoreUI1.html, so a game update that renames or removes a page
// part page.js depends on shows up here instead of only in the game.
//
// CoreUI1 is a persistent HUD panel (not a pop-up like the event or cooking windows), driven by a plain,
// non-module <script> tag that declares `const state = reactive({...})` as a top-level lexical binding -
// so `iframe.contentWindow.eval('state')` can read it. The root page normally
// supplies MutationObserver, setTimeout, and the iframe list (it is a real browser window), so this test
// also loads a small real jsdom window to run page.js in, instead of a bare object stub.
'use strict';

const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const url = require('node:url');
const vm = require('node:vm');
const { JSDOM } = require('jsdom');

const GAME_DIR = process.env.SL_GAME_DIR ||
  'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Survival Log';
const CORE_UI1_HTML = path.join(GAME_DIR, 'SurvivalLog_Data', 'StreamingAssets', 'WebUI', 'UI', 'CoreUI1', 'CoreUI1.html');
const PAGE_JS_PATH = path.join(__dirname, '..', '..', 'src', 'page.js');

const gameFileExists = fs.existsSync(CORE_UI1_HTML);

if (!gameFileExists) {
  test('page.js against the game page', { skip: `game files not found under SL_GAME_DIR (${GAME_DIR}); set SL_GAME_DIR to the game folder` }, () => {});
} else {
  const pageJs = fs.readFileSync(PAGE_JS_PATH, 'utf8');
  const coreUi1Html = fs.readFileSync(CORE_UI1_HTML, 'utf8');

  // Pulls the page part names out of the FEATURES table of page.js, so this test never copies the names.
  function featureNames() {
    const table = pageJs.match(/var FEATURES = \{([\s\S]*?)\};/);
    assert.ok(table, 'FEATURES table not found in page.js');
    const names = [];
    const quoted = /'([^']+)'/g;
    let m;
    while ((m = quoted.exec(table[1]))) names.push(m[1]);
    assert.ok(names.length > 0, 'no part names parsed out of the FEATURES table');
    return names;
  }

  // A '.class' part is a CSS selector; every piece of it (split on '.') must appear as text in the page.
  function partIsPresent(name) {
    return name.replace(/^[#.]/, '').split('.').every((piece) => coreUi1Html.includes(piece));
  }

  test('static: every FEATURES part name of page.js exists in CoreUI1.html', () => {
    for (const name of featureNames()) {
      assert.ok(partIsPresent(name), `page part "${name}" not found in CoreUI1.html`);
    }
  });

  // CoreUI1.html starts its own persistent requestAnimationFrame loops (plant-countdown ticks and the
  // like) as soon as it loads, which would otherwise keep every jsdom window (and so the test process)
  // alive forever. t.after(...) closes each window once its test is done, which jsdom uses to cancel
  // those loops, so `node --test` still exits on its own (the --test-force-exit npm script flag is only
  // a backstop against a page part this file does not yet know to close).
  async function loadCoreWindow(t) {
    const dom = new JSDOM(coreUi1Html, {
      url: url.pathToFileURL(CORE_UI1_HTML).href,
      runScripts: 'dangerously',
      resources: 'usable',
      pretendToBeVisual: true
    });
    t.after(() => dom.window.close());
    await new Promise((resolve, reject) => {
      dom.window.addEventListener('load', resolve);
      setTimeout(() => reject(new Error('CoreUI1.html did not fire load within 5s')), 5000);
    });
    return dom.window;
  }

  // A blank real window to run page.js in, standing in for the root page (Root.html), which is what
  // supplies MutationObserver, setTimeout, and the iframe list in the real game, and is the target of
  // the button's own postMessage ("posts to the root window"). The CoreUI1 iframe is held in a mutable
  // box so a test can swap it (a fresh iframe on a later install() call), the same way Root.html itself
  // is never recreated between panel reloads.
  function makeRootWindow(t, coreWindow) {
    const root = new JSDOM('<!doctype html><html><body></body></html>', {
      url: 'file:///Root.html',
      pretendToBeVisual: true
    }).window;
    t.after(() => root.close());
    const frameBox = { current: coreWindow || null };
    const originalQSA = root.document.querySelectorAll.bind(root.document);
    root.document.querySelectorAll = (selector) =>
      selector === 'iframe' ? (frameBox.current ? [{ contentWindow: frameBox.current }] : []) : originalQSA(selector);
    vm.createContext(root);
    root.__setCoreFrame = (w) => { frameBox.current = w; };
    return root;
  }

  // vm.runInContext (not root.eval) so the bare "window" identifier page.js relies on resolves to root
  // itself, the same as it does for a real page evaluated in a browser context.
  function runPageJs(root, callExpr) {
    return vm.runInContext(pageJs + ';' + callExpr, root, { filename: 'page.js' });
  }

  function install(root) { return runPageJs(root, 'window.__trapline.install()'); }

  function wait(win, ms) {
    return new Promise((resolve) => win.setTimeout(resolve, ms));
  }

  // Sends the trap-list message the real reducer sends (WebUI_CoreUI1_TrapMsg), the same shape
  // Reducer_Web_CoreUI1 / WebUI_CoreUI1.UpdateTrapsList builds.
  async function postTraps(coreWindow, traps, containerActive) {
    coreWindow.postMessage({
      type: 'WebUI_CoreUI1_TrapMsg',
      data: { containerActive: containerActive !== false, traps }
    }, '*');
    await wait(coreWindow, 30);
  }

  function trap(id, status, roomName) {
    return { trapInstanceId: id, trapName: 'Snare', roomName: roomName || '', iconUrl: '', status };
  }

  test('jsdom: a status-1 trap shows the "Collect all" button, styled before the chevron', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 1)]);
    const root = makeRootWindow(t, coreWindow);

    const result = install(root);
    assert.equal(result, 'installed', `install result was "${result}"`);

    const row = coreWindow.document.querySelector('.trap-toggle-row');
    const btn = row.querySelector('.trapline-collect-btn');
    assert.ok(btn, 'the "Collect all" button was not added to the header row');
    assert.equal(btn.textContent, 'Collect all');
    assert.ok(btn.nextElementSibling.classList.contains('trap-toggle-chevron'), 'the button must sit right before the chevron');
  });

  test('jsdom: no trap with prey shows no button', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 2), trap(2, 0)]);
    const root = makeRootWindow(t, coreWindow);

    const result = install(root);
    assert.equal(result, 'installed', `install result was "${result}"`);

    const row = coreWindow.document.querySelector('.trap-toggle-row');
    assert.ok(row, 'the header row should still render (traps exist, just none with prey)');
    assert.equal(row.querySelector('.trapline-collect-btn'), null, 'a button appeared with no trap having prey');
  });

  test('jsdom: a click posts TRAPLINE_COLLECT_ALL to the root window and does not toggle the list', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 1)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);

    const received = [];
    root.addEventListener('message', (e) => received.push(e.data));

    const btn = coreWindow.document.querySelector('.trap-toggle-row .trapline-collect-btn');
    btn.dispatchEvent(new coreWindow.MouseEvent('click', { bubbles: true }));
    await wait(root, 30);

    // received[0].data crosses realms (postMessage structured-clones it into root's own realm), so it is
    // spread into a plain object of THIS realm before comparing - a strict deepEqual across realms fails
    // on prototype identity alone, even with identical own properties.
    assert.equal(received.length, 1, 'expected exactly one posted message');
    assert.equal(received[0].type, 'TRAPLINE_COLLECT_ALL');
    assert.equal(received[0].sourcePageId, 'CoreUI1');
    assert.deepEqual({ ...received[0].data }, {});
    assert.equal(coreWindow.eval('state.trapsExpanded'), false, 'the click must not open the trap list');
  });

  test('jsdom: a second install() gives one button, not two', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 1)]);
    const root = makeRootWindow(t, coreWindow);

    assert.equal(install(root), 'installed');
    assert.equal(install(root), 'installed');

    const buttons = coreWindow.document.querySelectorAll('.trap-toggle-row .trapline-collect-btn');
    assert.equal(buttons.length, 1, `expected exactly one button, found ${buttons.length}`);
  });

  test('jsdom: the trap panel created again (0 traps, then 1) gets the button again', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    assert.equal(coreWindow.document.querySelector('.trap-panel'), null, 'no traps yet, so no trap panel');

    await postTraps(coreWindow, [trap(1, 1)]);
    assert.equal(install(root), 'installed');
    assert.ok(coreWindow.document.querySelector('.trap-toggle-row .trapline-collect-btn'), 'the button did not appear on the first trap panel');

    // Back to zero traps: Vue's v-if removes the whole .trap-panel (and so the old header row and button).
    await postTraps(coreWindow, []);
    assert.equal(coreWindow.document.querySelector('.trap-panel'), null, 'the trap panel should be gone with no traps');

    // A trap again: Vue recreates .trap-panel and .trap-toggle-row from scratch.
    await postTraps(coreWindow, [trap(2, 1)]);
    assert.equal(install(root), 'installed');
    const btn = coreWindow.document.querySelector('.trap-toggle-row .trapline-collect-btn');
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
    assert.ok(row.querySelector('.trapline-collect-btn'), 'the button should render alongside the badge');
  });

  test('jsdom: no "missing" report while the list is collapsed', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 1)]);
    const root = makeRootWindow(t, coreWindow);

    assert.equal(coreWindow.eval('state.trapsExpanded'), false, 'the list should start collapsed');
    const result = install(root);
    assert.equal(result, 'installed', `expected no missing-part report while collapsed, got "${result}"`);
  });

  // Sends TRAPLINE-shaped group data through the script's own setGroups() entry point, the same shape
  // C# pushes: floors in display order, a trap id -> floor id map, and a
  // trap id -> cell index map.
  function setGroups(root, data) {
    return runPageJs(root, 'window.__trapline.setGroups(' + JSON.stringify(data) + ')');
  }

  function expand(coreWindow) {
    coreWindow.eval('state.trapsExpanded = true');
    return wait(coreWindow, 30);
  }

  // The inline grid placement of one node, with spaces removed, as '<row>|<column>'.
  function place(el) {
    const v = (p) => el.style.getPropertyValue(p).replace(/\s+/g, '');
    return v('grid-row') + '|' + v('grid-column');
  }

  // The inline column template of the popover, spaces normalized.
  function columns(doc) {
    return doc.querySelector('.trap-popover').style.getPropertyValue('grid-template-columns').replace(/\s+/g, ' ').replace(/,\s*/g, ', ');
  }

  function emptyCells(doc, floorId) {
    return [...doc.querySelectorAll('.trapline-empty-cell[data-floor-id="' + floorId + '"]')];
  }

  // Home: slots 0-2, traps 1 (cell 0) and 3 (cell 2), cell 1 free. Basement: slots 0-1, trap 2 (cell 1),
  // cell 0 free.
  const TWO_FLOORS = {
    floors: [{ id: 1, label: 'Home', traps: 2, slots: 3 }, { id: 2, label: 'Basement', traps: 1, slots: 2 }],
    trapFloor: { 1: 1, 2: 2, 3: 1 },
    trapCell: { 1: 0, 2: 1, 3: 2 }
  };

  test('jsdom: rows, labels, and empty cells are placed in the grid for traps on two floors', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    // All armed (status 0), so the page's own prey-first sort leaves this insertion order untouched.
    await postTraps(coreWindow, [trap(1, 0), trap(2, 0), trap(3, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);

    const result = setGroups(root, TWO_FLOORS);
    assert.equal(result, 'installed', `setGroups result was "${result}"`);

    const doc = coreWindow.document;
    assert.ok(doc.querySelector('.trap-popover').classList.contains('trapline-grid'));

    const rows = [...doc.querySelectorAll('.trap-item-row')];
    assert.equal(place(rows[0]), '1|2', 'trap 1: Home line, cell 0');
    assert.equal(place(rows[1]), '2|3', 'trap 2: Basement line, cell 1');
    assert.equal(place(rows[2]), '1|4', 'trap 3: Home line, cell 2');

    const labels = [...doc.querySelectorAll('.trapline-floor-label')];
    assert.equal(labels.length, 2);
    const home = labels.find((l) => l.getAttribute('data-floor-id') === '1');
    const basement = labels.find((l) => l.getAttribute('data-floor-id') === '2');
    assert.equal(home.textContent, 'Home', 'only the floor name, no slot count');
    assert.equal(basement.textContent, 'Basement');
    assert.equal(columns(doc), 'max-content repeat(3, 22px)', 'as many cell columns as the longest line (Home, 3 cells)');
    assert.equal(place(home), '1/span1|1');
    assert.equal(place(basement), '2/span1|1');

    const homeEmpty = emptyCells(doc, 1);
    assert.equal(homeEmpty.length, 1);
    assert.equal(homeEmpty[0].getAttribute('data-cell'), '1');
    assert.equal(place(homeEmpty[0]), '1|3');
    const basementEmpty = emptyCells(doc, 2);
    assert.equal(basementEmpty.length, 1);
    assert.equal(basementEmpty[0].getAttribute('data-cell'), '0');
    assert.equal(place(basementEmpty[0]), '2|2');
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

    // Vue update: drop trap 2 (Basement), add trap 3 (Home), with no further setGroups() call.
    await postTraps(coreWindow, [trap(1, 0), trap(3, 0)]);
    install(root); // re-applies against the freshly patched rows, as the body observer would

    const doc = coreWindow.document;
    const rows = [...doc.querySelectorAll('.trap-item-row')];
    assert.equal(rows.length, 2, 'expected exactly one row per current trap');
    assert.equal(place(rows[0]), '1|2', 'trap 1, Home cell 0');
    assert.equal(place(rows[1]), '1|4', 'trap 3, Home cell 2');
    assert.deepEqual(emptyCells(doc, 1).map((c) => c.getAttribute('data-cell')), ['1']);
    assert.deepEqual(emptyCells(doc, 2).map((c) => c.getAttribute('data-cell')).sort(), ['0', '1'], 'trap 2 is gone, so both Basement cells are free');
    assert.equal(doc.querySelectorAll('.trapline-floor-label').length, 2, 'each label once');
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
    assert.equal(place(rows[1]), '2|3');

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
    assert.equal(place(rows[1]), '2|3');
    assert.equal(doc.querySelectorAll('.trapline-floor-label').length, 2, 'no duplicate labels');
    assert.equal(doc.querySelectorAll('.trapline-empty-cell').length, 2, 'no duplicate empty cells');
  });

  test('jsdom: a floor with 8 cells uses two lines under one label, and the next floor starts after them', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0), trap(2, 0), trap(3, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, {
      floors: [{ id: 1, label: 'Home', traps: 2, slots: 8 }, { id: 2, label: 'Basement', traps: 1, slots: 1 }],
      trapFloor: { 1: 1, 2: 1, 3: 2 },
      trapCell: { 1: 5, 2: 7, 3: 0 }
    });

    const doc = coreWindow.document;
    const rows = [...doc.querySelectorAll('.trap-item-row')];
    assert.equal(place(rows[0]), '1|7', 'Home cell 5: last cell of line 1');
    assert.equal(place(rows[1]), '2|3', 'Home cell 7: second cell of line 2');
    assert.equal(place(rows[2]), '3|2', 'Basement cell 0: first line after the two Home lines');
    assert.equal(place(doc.querySelector('.trapline-floor-label[data-floor-id="1"]')), '1/span2|1');
    assert.equal(place(doc.querySelector('.trapline-floor-label[data-floor-id="2"]')), '3/span1|1');
    assert.equal(emptyCells(doc, 1).length, 6);
    assert.equal(columns(doc), 'max-content repeat(6, 22px)', 'never more than 6 cell columns');
    assert.equal(place(doc.querySelector('.trapline-empty-cell[data-floor-id="1"][data-cell="6"]')), '2|2');
  });

  test('jsdom: a trap with no cell goes to one line after all floors, with no label', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0), trap(9, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, {
      floors: [{ id: 1, label: 'Home', traps: 1, slots: 1 }],
      trapFloor: { 1: 1 },
      trapCell: { 1: 0 }
    });

    const doc = coreWindow.document;
    const rows = [...doc.querySelectorAll('.trap-item-row')];
    assert.equal(place(rows[0]), '1|2');
    assert.equal(place(rows[1]), '2|2', 'trap 9 has no cell, so it goes to the line after Home');
    assert.equal(doc.querySelectorAll('.trapline-floor-label').length, 1, 'no label for the extra line');
    assert.equal(columns(doc), 'max-content repeat(1, 22px)');
  });

  test('jsdom: more than 6 traps with no cell wrap to a second extra line', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [1, 2, 3, 4, 5, 6, 7].map((id) => trap(id, 0)));
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, { floors: [], trapFloor: {}, trapCell: {} });

    const rows = [...coreWindow.document.querySelectorAll('.trap-item-row')];
    assert.equal(place(rows[5]), '1|7');
    assert.equal(place(rows[6]), '2|2', 'the 7th extra trap starts a new line instead of overlapping the 1st');
  });

  test('jsdom: a floor with no trap shows its label and only empty cells', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, {
      floors: [{ id: 1, label: 'Home', traps: 1, slots: 1 }, { id: 2, label: 'Basement', traps: 0, slots: 2 }],
      trapFloor: { 1: 1 },
      trapCell: { 1: 0 }
    });

    const doc = coreWindow.document;
    const basement = doc.querySelector('.trapline-floor-label[data-floor-id="2"]');
    assert.equal(basement.textContent, 'Basement');
    assert.equal(columns(doc), 'max-content repeat(2, 22px)', 'the Basement line of 2 empty cells is the longest');
    assert.equal(place(basement), '2/span1|1');
    assert.deepEqual(emptyCells(doc, 2).map(place), ['2|2', '2|3']);
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
    const received = [];
    coreWindow.addEventListener('message', (e) => received.push(e.data));

    const row = coreWindow.document.querySelectorAll('.trap-item-row')[0];
    row.dispatchEvent(new coreWindow.MouseEvent('click', { bubbles: true }));
    await wait(coreWindow, 30);

    const clicks = received.filter((m) => m.type === 'TRAP_HUD_CLICK');
    assert.equal(clicks.length, 1, 'expected exactly one TRAP_HUD_CLICK message');
    assert.deepEqual({ ...clicks[0].data }, { instanceId: 1 });
  });

  test('jsdom: a grid row hides its room name', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0, 'Balcony')]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);

    const room = coreWindow.document.querySelector('.trap-item-room');
    assert.notEqual(coreWindow.getComputedStyle(room).display, 'none', 'the room name should show before the grid applies');

    const result = setGroups(root, { floors: [{ id: 1, label: 'Home', traps: 1, slots: 1 }], trapFloor: { 1: 1 }, trapCell: { 1: 0 } });
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

    const result = setGroups(root, { floors: [{ id: 1, label: 'Home', traps: 1, slots: 1 }], trapFloor: { 1: 1 }, trapCell: { 1: 0 } });
    assert.match(result, /missing: groups\(\.trap-popover\)/, `expected a missing-part report, got "${result}"`);

    const row = coreWindow.document.querySelector('.trap-item-row');
    assert.equal(place(row), '|', 'no grid placement when a required groups part is missing');
    assert.ok(!popover.classList.contains('trapline-grid'), 'no grid class when a required groups part is missing');
    assert.equal(coreWindow.document.querySelector('.trapline-floor-label'), null, 'no label when a required groups part is missing');
    assert.ok(coreWindow.document.querySelector('.trap-toggle-row .trapline-collect-btn'), 'the button feature must be unaffected by the groups part going missing');
  });

  const ONE_FLOOR_THREE = {
    floors: [{ id: 1, label: 'Home', traps: 3, slots: 4 }],
    trapFloor: { 1: 1, 2: 1, 3: 1 },
    trapCell: { 1: 0, 2: 1, 3: 2 }
  };

  // The rules page.js adds, as one text with spaces removed. jsdom does not lay out a grid, so the look
  // of a cell is checked in this text and the game checks the rest.
  function styleText(doc) {
    return doc.getElementById('trapline-style').textContent.replace(/\s+/g, '');
  }

  test('jsdom: a grid row shows only its icon', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 1, 'Balcony')]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, { floors: [{ id: 1, label: 'Home', traps: 1, slots: 1 }], trapFloor: { 1: 1 }, trapCell: { 1: 0 } });

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
    assert.match(css, /\.trapline-empty-cell\{[^}]*border:1pxsolidrgba\(255,255,255,0\.15\)[^}]*background:rgba\(0,0,0,0\.3\)/, 'the empty cell look');
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
    assert.match(css, /\.trap-popover\.trapline-grid\.trap-item-row\.is-prey\{[^}]*border-color:rgba\(255,193,7,0\.9\)[^}]*box-shadow:/, 'a gold border and glow on a prey cell');
    assert.match(css, /\.trap-popover\.trapline-grid\.trap-item-row\.is-prey::after\{[^}]*width:5px;height:5px/, 'a gold dot on a prey cell');

    const color = (sel) => coreWindow.getComputedStyle(doc.querySelector(sel)).borderLeftColor;
    assert.notEqual(color('.trap-item-row.is-prey'), color('.trap-item-row.is-armed'), 'prey and armed cells differ');
    assert.equal(color('.trap-item-row.is-nobait'), color('.trap-item-row.is-armed'), 'a no-bait cell looks the same as an armed cell');
  });

  test('jsdom: an empty cell is a HUD area and a click on it posts nothing', async (t) => {
    const coreWindow = await loadCoreWindow(t);
    await postTraps(coreWindow, [trap(1, 0), trap(2, 0), trap(3, 0)]);
    const root = makeRootWindow(t, coreWindow);
    install(root);
    await expand(coreWindow);
    setGroups(root, ONE_FLOOR_THREE);

    const empty = coreWindow.document.querySelector('.trapline-empty-cell');
    assert.ok(empty.hasAttribute('data-interactive'), 'the HUD must count an empty cell as its own area, or the click goes to the world');

    const inCore = [], inRoot = [];
    coreWindow.addEventListener('message', (e) => inCore.push(e.data));
    root.addEventListener('message', (e) => inRoot.push(e.data));
    empty.dispatchEvent(new coreWindow.MouseEvent('click', { bubbles: true }));
    await wait(coreWindow, 30);

    assert.equal(inCore.filter((m) => m.type === 'TRAP_HUD_CLICK').length, 0, 'no trap click');
    assert.equal(inRoot.length, 0, 'no mod message');
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
      const result = setGroups(root, { floors: [{ id: 1, label: 'Home', traps: 1, slots: 1 }], trapFloor: { 1: 1 }, trapCell: { 1: 0 } });

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
    assert.equal(place(popover.querySelectorAll('.trap-item-row')[1]), '2|3', 'the placement stays');
    assert.equal(coreWindow.getComputedStyle(popover.querySelector('.trap-item-room')).display, 'none', 'the room name stays hidden');
  });

  // Everything the grid adds to the popover, so a test can check that all of it is gone.
  function assertGridGone(coreWindow) {
    const doc = coreWindow.document;
    const popover = doc.querySelector('.trap-popover') || doc.querySelector('.trap-item-row').parentNode;
    assert.ok(!popover.classList.contains('trapline-grid'), 'no grid class');
    assert.equal(popover.style.getPropertyValue('grid-template-columns'), '', 'no inline column template');
    for (const row of doc.querySelectorAll('.trap-item-row')) assert.equal(place(row), '|', 'no grid placement on a row');
    assert.equal(doc.querySelector('.trapline-floor-label'), null, 'no label');
    assert.equal(doc.querySelector('.trapline-empty-cell'), null, 'no empty cell');
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
    assert.equal(coreWindow.document.querySelector('.trapline-collect-btn'), null, 'no button should be added when a required part is missing');
    // The row itself (and the rest of the page) is unaffected by the missing chevron.
    assert.ok(coreWindow.document.querySelector('.trap-toggle-row'), 'the header row itself should be untouched');
  });
}
