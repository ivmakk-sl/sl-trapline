// Runs in the root page, reaching the CoreUI1 iframe through iframe.contentWindow, as in Project Cook.
// Defines window.__trapline once; C# then calls window.__trapline.install() (the trap-panel HUD button),
// window.__trapline.setText({collectAll: '...'}) whenever the button word changes (a language switch),
// and window.__trapline.setGroups({floors, trapFloor}) whenever the floor groups of the expanded trap
// list change (a trap placed or picked up, a floor or area unlocked). The CoreUI1 iframe is looked up
// fresh on every call: it persists for the life of the HUD, but a WebView reload still gives it a new
// contentWindow, so nothing here caches it across calls.
window.__trapline = window.__trapline || (function () {
  var FEATURES = {
    button: ['.trap-toggle-row', '.trap-toggle-chevron'],
    groups: ['.trap-popover', '.trap-item-row', '.trap-item-name', '.trap-item-status', '.trap-item-icon-img']
  };

  var texts = { collectAll: 'Collect all' };

  // Set by setGroups(); null until C# has pushed group data at least once, in which case applyGroups
  // does nothing. Shape: { floors: [{id, label, traps, slots}, ...] (in
  // display order), trapFloor: { '<trapInstanceId>': <floorId>, ... }, trapCell: { '<trapInstanceId>': <cell> } }.
  var groupsData = null;

  function partsMissing(doc, feature) {
    var parts = FEATURES[feature], missing = [];
    for (var p = 0; p < parts.length; p++) if (!doc.querySelector(parts[p])) missing.push(parts[p]);
    return missing;
  }

  // The trap panel (and so the header row the button lives in) renders only while the game has at least
  // one placed trap. Reading this from the page's own state, instead of testing the DOM for it, keeps a
  // plain empty trap list from ever being reported as a "missing" part.
  function panelShouldRender(state) {
    return !!(state && state.trapsActive && state.traps && state.traps.length > 0);
  }

  // `.trap-popover` (and so `.trap-item-row`) renders only while the panel renders AND the list is
  // expanded (state.trapsExpanded), the second level of the two-level part check.
  function popoverShouldRender(state) {
    return panelShouldRender(state) && !!(state && state.trapsExpanded);
  }

  // Sends exactly what the page's own sendMessage/UnitySendEvent would send from inside the CoreUI1
  // iframe (window.parent.postMessage). This whole script runs in the root page already (see the header
  // comment), so its own top-level "window" identifier already IS that parent/root window.
  function postCollectAll() {
    window.postMessage({ type: 'TRAPLINE_COLLECT_ALL', data: {}, sourcePageId: 'CoreUI1' }, '*');
  }

  // Adds (or removes) the "Collect all" button just before the header row's chevron. Idempotent: a
  // button already there is kept (only its text is refreshed) and never duplicated. A row that Vue
  // recreates (the trap panel goes from 0 traps to more than 0 again) gets a fresh button, because the
  // lookup is always against the CURRENT row, never a cached one.
  function applyButton(doc, state) {
    if (!panelShouldRender(state)) return '';
    var missing = partsMissing(doc, 'button');
    if (missing.length) return 'button(' + missing.join(',') + ')';

    var row = doc.querySelector(FEATURES.button[0]);
    var chevron = doc.querySelector(FEATURES.button[1]);
    var btn = row.querySelector('.trapline-collect-btn');
    var urgent = state.trapsUrgentCount > 0;

    if (!urgent) { if (btn) btn.remove(); return ''; }

    if (!btn) {
      btn = doc.createElement('button');
      btn.type = 'button';
      btn.className = 'trapline-collect-btn';
      btn.addEventListener('click', function (e) {
        e.stopPropagation();
        postCollectAll();
      });
      row.insertBefore(btn, chevron);
    } else if (btn.nextSibling !== chevron) {
      row.insertBefore(btn, chevron);
    }
    // Setting .textContent always replaces the text node, even to an identical value, which the body
    // observer below would see as a fresh mutation and re-apply forever - so this only writes on a
    // real change.
    if (btn.textContent !== texts.collectAll) btn.textContent = texts.collectAll;
    return '';
  }

  var CELLS_PER_LINE = 6;

  // Writes one inline style property only when its value differs: the body observer below sees every
  // write, even of an identical value, and would re-apply forever.
  function setStyle(el, prop, value) {
    if (el.style.getPropertyValue(prop) !== value) el.style.setProperty(prop, value);
  }

  function place(el, row, column) {
    setStyle(el, 'grid-row', row);
    setStyle(el, 'grid-column', column);
  }

  // Removes everything the grid adds, so the list shows as the game's plain list. It searches the whole document, because a missing part can be the popover
  // itself. Like every write here, it writes only when something is there to remove.
  function clearGrid(doc) {
    var grids = doc.querySelectorAll('.trapline-grid');
    for (var g = 0; g < grids.length; g++) grids[g].classList.remove('trapline-grid');
    var placed = doc.querySelectorAll('[style]');
    for (var i = 0; i < placed.length; i++) {
      if (placed[i].style.getPropertyValue('grid-row')) placed[i].style.removeProperty('grid-row');
      if (placed[i].style.getPropertyValue('grid-column')) placed[i].style.removeProperty('grid-column');
      if (placed[i].style.getPropertyValue('grid-template-columns')) placed[i].style.removeProperty('grid-template-columns');
    }
    var ours = doc.querySelectorAll('.trapline-floor-label, .trapline-empty-cell');
    for (var n = 0; n < ours.length; n++) ours[n].remove();
  }

  // Lays the trap list out as a grid: one or more lines for each floor, the
  // floor label (the floor name) in column 1, and one cell for each usable slot in the columns
  // after it, at most CELLS_PER_LINE in a line. Each game row is the cell of its trap, placed with an
  // inline grid-row / grid-column; the script never moves a row, because Vue owns the rows, and
  // state.traps[i] is row i in page order. A cell index that no current row has gets an empty cell node
  // of our own. A row whose trap has no floor or no cell in the pushed data goes to one line after all
  // floors, with no label. Labels and empty cells carry `data-floor-id` (and `data-cell`), so a later
  // call finds and updates the same node instead of duplicating it, and drops one that the pushed data no
  // longer has.
  function applyGroups(doc, state) {
    var popover = doc.querySelector(FEATURES.groups[0]);
    // A collapse (state.trapsExpanded false) only means the popover is not expected to render anymore -
    // Vue keeps it in the DOM for its leave transition, so the grid must stay as it is until it really
    // turns off (no data, or a missing part below).
    if (!groupsData) {
      clearGrid(doc);
      return '';
    }
    if (!popoverShouldRender(state)) return '';
    var missing = partsMissing(doc, 'groups');
    if (missing.length) {
      clearGrid(doc);
      return 'groups(' + missing.join(',') + ')';
    }

    var rows = popover.querySelectorAll(FEATURES.groups[1]);
    var traps = state.traps || [];
    var floors = groupsData.floors || [];
    var trapFloor = groupsData.trapFloor || {};
    var trapCell = groupsData.trapCell || {};

    // The first line and the line count of each floor.
    var firstLine = {}, lineCount = {}, line = 1;
    for (var f = 0; f < floors.length; f++) {
      var lines = Math.max(1, Math.ceil(floors[f].slots / CELLS_PER_LINE));
      firstLine[floors[f].id] = line;
      lineCount[floors[f].id] = lines;
      line += lines;
    }
    var extraLine = line, extraCount = 0;

    function cellPlace(floorId, cell) {
      return [String(firstLine[floorId] + Math.floor(cell / CELLS_PER_LINE)), String(2 + cell % CELLS_PER_LINE)];
    }

    var used = {};
    for (var i = 0; i < rows.length; i++) {
      var id = traps[i] ? String(traps[i].trapInstanceId) : undefined;
      var floorId = id !== undefined ? trapFloor[id] : undefined;
      var cell = id !== undefined ? trapCell[id] : undefined;
      if (floorId !== undefined && cell !== undefined && firstLine[floorId] !== undefined) {
        var p = cellPlace(floorId, cell);
        place(rows[i], p[0], p[1]);
        used[floorId + ':' + cell] = true;
      } else {
        place(rows[i], String(extraLine + Math.floor(extraCount / CELLS_PER_LINE)), String(2 + extraCount % CELLS_PER_LINE));
        extraCount++;
      }
    }

    var keep = {};
    for (var fi = 0; fi < floors.length; fi++) {
      var floor = floors[fi];
      var label = popover.querySelector('.trapline-floor-label[data-floor-id="' + floor.id + '"]');
      if (!label) {
        label = doc.createElement('div');
        label.className = 'trapline-floor-label';
        label.setAttribute('data-floor-id', String(floor.id));
        popover.appendChild(label);
      }
      place(label, firstLine[floor.id] + ' / span ' + lineCount[floor.id], '1');
      // Only the floor name: the cells show the slot count.
      var text = floor.label;
      if (label.textContent !== text) label.textContent = text;
      keep['label:' + floor.id] = true;

      for (var c = 0; c < floor.slots; c++) {
        if (used[floor.id + ':' + c]) continue;
        var empty = popover.querySelector('.trapline-empty-cell[data-floor-id="' + floor.id + '"][data-cell="' + c + '"]');
        if (!empty) {
          empty = doc.createElement('div');
          empty.className = 'trapline-empty-cell';
          empty.setAttribute('data-floor-id', String(floor.id));
          empty.setAttribute('data-cell', String(c));
          // CoreUI1 tells Unity which areas are HUD from [data-interactive] and a few tags; a click on
          // any other area goes to the world behind the HUD. With it, an empty cell click does nothing.
          empty.setAttribute('data-interactive', '');
          popover.appendChild(empty);
        }
        var ep = cellPlace(floor.id, c);
        place(empty, ep[0], ep[1]);
        keep['cell:' + floor.id + ':' + c] = true;
      }
    }

    var ours = popover.querySelectorAll('.trapline-floor-label, .trapline-empty-cell');
    for (var e = 0; e < ours.length; e++) {
      var key = ours[e].classList.contains('trapline-floor-label')
        ? 'label:' + ours[e].getAttribute('data-floor-id')
        : 'cell:' + ours[e].getAttribute('data-floor-id') + ':' + ours[e].getAttribute('data-cell');
      if (!keep[key]) ours[e].remove();
    }

    // As many cell columns as the longest line has cells, so the popover (as wide as its content in
    // the flex-start .trap-panel) ends right after the cells.
    var widest = Math.min(extraCount, CELLS_PER_LINE);
    for (var w = 0; w < floors.length; w++) widest = Math.max(widest, Math.min(floors[w].slots, CELLS_PER_LINE));
    setStyle(popover, 'grid-template-columns', 'max-content repeat(' + Math.max(1, widest) + ', 22px)');

    if (!popover.classList.contains('trapline-grid')) popover.classList.add('trapline-grid');
    return '';
  }

  function ensureStyle(doc) {
    if (doc.getElementById('trapline-style')) return;
    var style = doc.createElement('style');
    style.id = 'trapline-style';
    style.textContent =
      '.trapline-collect-btn{font-family:inherit;font-size:10px;font-weight:bold;color:#fff;' +
      'background:rgba(255,193,7,0.18);border:1px solid rgba(255,193,7,0.5);border-radius:3px;' +
      'padding:1px 6px;cursor:pointer;line-height:1.4;}' +
      '.trapline-collect-btn:hover{background:rgba(255,193,7,0.32);}' +
      '.trapline-floor-label{font-family:inherit;font-size:9px;font-weight:bold;' +
      'color:rgba(255,193,7,0.85);text-shadow:1px 1px 2px black;letter-spacing:0.3px;' +
      'padding:3px 2px 1px;white-space:nowrap;}' +
      '.trap-popover.trapline-grid{display:grid;grid-template-columns:max-content repeat(6,22px);gap:3px;' +
      'max-width:none;align-items:center;}' +
      '.trap-popover.trapline-grid .trap-item-name,.trap-popover.trapline-grid .trap-item-room,' +
      '.trap-popover.trapline-grid .trap-item-status{display:none;}' +
      '.trap-popover.trapline-grid .trap-item-row{width:22px;height:22px;box-sizing:border-box;padding:0;gap:0;' +
      'justify-content:center;border:1px solid rgba(255,255,255,0.15);}' +
      '.trap-popover.trapline-grid .trap-item-row:hover{transform:none;}' +
      '.trap-popover.trapline-grid .trap-item-icon-img{width:18px;height:18px;}' +
      '.trap-popover.trapline-grid .trap-item-row.is-prey{border-color:rgba(255,193,7,0.9);' +
      'box-shadow:0 0 5px rgba(255,193,7,0.6);}' +
      '.trap-popover.trapline-grid .trap-item-row.is-prey::after{content:"";position:absolute;top:1px;right:1px;' +
      'width:5px;height:5px;border-radius:50%;background:rgba(255,193,7,0.95);}' +
      '.trapline-empty-cell{width:22px;height:22px;box-sizing:border-box;border:1px solid rgba(255,255,255,0.15);' +
      'border-radius:4px;background:rgba(0,0,0,0.3);}';
    doc.head.appendChild(style);
  }

  var applying = false;

  // Applies every implemented feature to one CoreUI1 window and (re)installs its body observer. Returns
  // the "installed"/"missing:"/"error:" text that install()/setText() hand back to C#.
  function apply(w) {
    var doc = w.document, state;
    try { state = w.eval('state'); } catch (e) { return 'error: state not reachable (' + e + ')'; }

    ensureStyle(doc);
    var missing = [];
    try {
      var m1 = applyButton(doc, state); if (m1) missing.push(m1);
      var m2 = applyGroups(doc, state); if (m2) missing.push(m2);
    } catch (e) { return 'error: ' + e; }

    if (!w.__traplineObserver) {
      var observer = new MutationObserver(function () {
        // Guards against the observer re-entering on the DOM changes applyButton/applyGroups/ensureStyle
        // themselves just made (childList+subtree on body sees those too).
        if (applying) return;
        applying = true;
        try {
          ensureStyle(doc);
          var s = w.eval('state');
          applyButton(doc, s);
          applyGroups(doc, s);
        } catch (e) { /* a page update broke a part; the next install()/setText()/setGroups() call reports it */ }
        finally { applying = false; }
      });
      observer.observe(doc.body, { childList: true, subtree: true });
      w.__traplineObserver = observer;
    }

    return missing.length ? ('installed; missing: ' + missing.join(', ')) : 'installed';
  }

  function findFrame() {
    var frames = document.querySelectorAll('iframe');
    for (var i = 0; i < frames.length; i++) {
      var w = frames[i].contentWindow;
      if (!w) continue;
      try { if (!/CoreUI1\.html/i.test(String(w.location))) continue; } catch (e) { continue; }
      if (w.document.readyState !== 'complete') continue;
      return w;
    }
    return null;
  }

  function attempt(retries) {
    var w = findFrame();
    if (!w) {
      if (retries > 0) setTimeout(function () { attempt(retries - 1); }, 300);
      return 'no CoreUI1 frame';
    }
    return apply(w);
  }

  function install() { return attempt(10); }

  function setText(opts) {
    if (opts && typeof opts.collectAll === 'string' && opts.collectAll) texts.collectAll = opts.collectAll;
    var w = findFrame();
    if (!w) return 'no CoreUI1 frame';
    return apply(w);
  }

  function setGroups(data) {
    groupsData = data || null;
    var w = findFrame();
    if (!w) return 'no CoreUI1 frame';
    return apply(w);
  }

  return { install: install, setText: setText, setGroups: setGroups };
})();
