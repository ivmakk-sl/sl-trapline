// The trap grid in the expanded HUD trap list: the floor lines, the cells, and the floor labels.
import { FEATURES, partsMissing, popoverShouldRender, setStyle } from './core';
import type { CoreState, Groups } from './types';

const CELLS_PER_LINE = 6;

// The grid place of a node, as custom properties that the rules of page.css read, so the look stays
// in the CSS file.
function place(el: HTMLElement, row: string, column: string): void {
  setStyle(el, '--tl-row', row);
  setStyle(el, '--tl-col', column);
}

const GRID_PROPS = ['--tl-row', '--tl-col', '--tl-cols'];

// Removes everything the grid adds, so the list shows as the game's plain list. It searches the whole
// document, because a missing part can be the popover itself, but it touches only the nodes that carry
// the mod's own properties, and only those properties. Like every write here, it writes only when
// something is there to remove.
function clearGrid(doc: Document): void {
  for (const grid of doc.querySelectorAll('.trapline-grid')) grid.classList.remove('trapline-grid');
  for (const placed of doc.querySelectorAll<HTMLElement>('[style*="--tl-"]')) {
    for (const prop of GRID_PROPS) {
      if (placed.style.getPropertyValue(prop)) placed.style.removeProperty(prop);
    }
  }
  for (const ours of doc.querySelectorAll('.trapline-floor-label, .trapline-empty-cell')) ours.remove();
}

// Lays the trap list out as a grid: one or more lines for each floor, the
// floor label (the floor name) in column 1, and one cell for each usable slot in the columns
// after it, at most CELLS_PER_LINE in a line. Each game row is the cell of its trap, placed with the
// --tl-row / --tl-col properties; the script never moves a row, because Vue owns the rows, and
// state.traps[i] is row i in page order. A cell index that no current row has gets an empty cell node
// of our own. A row whose trap has no floor or no cell in the pushed data goes to one line after all
// floors, with no label. Labels and empty cells carry `data-floor-id` (and `data-cell`), so a later
// call finds and updates the same node instead of duplicating it, and drops one that the pushed data no
// longer has. Returns the missing parts, or ''.
export function applyGroups(doc: Document, state: CoreState, groups: Groups | null): string {
  // A collapse (state.trapsExpanded false) only means the popover is not expected to render anymore -
  // Vue keeps it in the DOM for its leave transition, so the grid must stay as it is until it really
  // turns off (no data, or a missing part below).
  if (!groups) {
    clearGrid(doc);
    return '';
  }
  if (!popoverShouldRender(state)) return '';
  const missing = partsMissing(doc, 'groups');
  if (missing.length) {
    clearGrid(doc);
    return 'groups(' + missing.join(',') + ')';
  }

  const popover = doc.querySelector<HTMLElement>(FEATURES.groups[0])!;
  const rows = popover.querySelectorAll<HTMLElement>(FEATURES.groups[1]);
  const traps = state.traps || [];
  const floors = groups.floors || [];
  const trapFloor = groups.trapFloor || {};
  const trapCell = groups.trapCell || {};

  // The first line and the line count of each floor.
  const firstLine: Record<number, number> = {};
  const lineCount: Record<number, number> = {};
  let line = 1;
  for (const floor of floors) {
    const lines = Math.max(1, Math.ceil(floor.slots / CELLS_PER_LINE));
    firstLine[floor.id] = line;
    lineCount[floor.id] = lines;
    line += lines;
  }
  const extraLine = line;
  let extraCount = 0;

  function cellPlace(floorId: number, cell: number): [string, string] {
    return [String(firstLine[floorId] + Math.floor(cell / CELLS_PER_LINE)), String(2 + cell % CELLS_PER_LINE)];
  }

  const used: Record<string, boolean> = {};
  for (let i = 0; i < rows.length; i++) {
    const id = traps[i] ? String(traps[i].trapInstanceId) : undefined;
    const floorId = id !== undefined ? trapFloor[id] : undefined;
    const cell = id !== undefined ? trapCell[id] : undefined;
    if (floorId !== undefined && cell !== undefined && firstLine[floorId] !== undefined) {
      const p = cellPlace(floorId, cell);
      place(rows[i], p[0], p[1]);
      used[floorId + ':' + cell] = true;
    } else {
      place(rows[i], String(extraLine + Math.floor(extraCount / CELLS_PER_LINE)), String(2 + extraCount % CELLS_PER_LINE));
      extraCount++;
    }
  }

  const keep: Record<string, boolean> = {};
  for (const floor of floors) {
    let label = popover.querySelector<HTMLElement>('.trapline-floor-label[data-floor-id="' + floor.id + '"]');
    if (!label) {
      label = doc.createElement('div');
      label.className = 'trapline-floor-label';
      label.setAttribute('data-floor-id', String(floor.id));
      popover.appendChild(label);
    }
    place(label, firstLine[floor.id] + ' / span ' + lineCount[floor.id], '1');
    // Only the floor name: the cells show the slot count.
    const text = floor.label;
    if (label.textContent !== text) label.textContent = text;
    keep['label:' + floor.id] = true;

    for (let c = 0; c < floor.slots; c++) {
      if (used[floor.id + ':' + c]) continue;
      let empty = popover.querySelector<HTMLElement>('.trapline-empty-cell[data-floor-id="' + floor.id + '"][data-cell="' + c + '"]');
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
      const ep = cellPlace(floor.id, c);
      place(empty, ep[0], ep[1]);
      keep['cell:' + floor.id + ':' + c] = true;
    }
  }

  for (const ours of popover.querySelectorAll('.trapline-floor-label, .trapline-empty-cell')) {
    const key = ours.classList.contains('trapline-floor-label')
      ? 'label:' + ours.getAttribute('data-floor-id')
      : 'cell:' + ours.getAttribute('data-floor-id') + ':' + ours.getAttribute('data-cell');
    if (!keep[key]) ours.remove();
  }

  // As many cell columns as the longest line has cells, so the popover (as wide as its content in
  // the flex-start .trap-panel) ends right after the cells.
  let widest = Math.min(extraCount, CELLS_PER_LINE);
  for (const floor of floors) widest = Math.max(widest, Math.min(floor.slots, CELLS_PER_LINE));
  setStyle(popover, '--tl-cols', String(Math.max(1, widest)));

  if (!popover.classList.contains('trapline-grid')) popover.classList.add('trapline-grid');
  return '';
}
