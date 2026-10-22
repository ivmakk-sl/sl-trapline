// The trap grid in the expanded HUD trap list: the floor lines and zone lines (the floor grid library), the cells of
// the traps, and the empty cells of the free trap slots.
import { FEATURES, partsMissing, popoverShouldRender } from './core';
import { applyLast } from './last';
import type { CoreState, Groups } from './types';
import { drawFloorGrid, placeCell, removeFloorGrid } from '../../Shared/floor-grid/web/floorGrid';

// The prefix of the floor grid library: its classes are tl-fg-*, its properties --tl-fg-*.
export const GRID_PREFIX = 'tl';

// The most cells of one line, as GridLogic.CellsPerLine and --tl-fg-max-cols.
const CELLS_PER_LINE = 8;

// Removes everything the grid adds, so the list shows as the game's plain list. It searches the whole document,
// because a missing part can be the popover itself, and touches only the nodes and properties of the mod. Like every
// write here, it writes only when something is there to remove.
function clearGrid(doc: Document): void {
  for (const grid of doc.querySelectorAll('.trapline-grid')) grid.classList.remove('trapline-grid');
  removeFloorGrid(doc, GRID_PREFIX);
  for (const ours of doc.querySelectorAll('.trapline-empty-cell, .trapline-mark, .trapline-last')) ours.remove();
}

// Lays the trap list out as a grid: the floor lines and zone lines at their rows, and one cell for each usable trap
// slot at its row and column, as C# gives them. Each game row is the cell of its trap, placed with two properties; the
// script never moves a row, because Vue owns the rows, and state.traps[i] is row i in page order. A cell with no
// current row gets an empty cell node of the mod, keyed by its slot node name (data-slot), so a later call finds and
// updates the same node, and drops one that the data no longer has. A row whose trap has no cell in the data goes to
// one line after all rows. Returns the missing parts, or ''.
// last: the mod texts and the Auto-replace checkbox of the last line of the list.
export function applyGroups(doc: Document, state: CoreState, groups: Groups | null,
  last: { words: Record<string, string> | undefined; replace: boolean } = { words: undefined, replace: false }): string {
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
  const cells = groups.cells || [];

  const byTrap: Record<string, { row: number; col: number; mark?: number }> = {};
  for (const c of cells) if (c.trap) byTrap[String(c.trap)] = c;

  const placed: Record<string, boolean> = {};
  let extra = 0;
  for (let i = 0; i < rows.length; i++) {
    const id = traps[i] ? String(traps[i].trapInstanceId) : undefined;
    const c = id !== undefined ? byTrap[id] : undefined;
    if (c) {
      placeCell(rows[i], GRID_PREFIX, c.row, c.col);
      placed[id!] = true;
    } else {
      placeCell(rows[i], GRID_PREFIX, groups.rows + 1 + Math.floor(extra / CELLS_PER_LINE), 1 + extra % CELLS_PER_LINE);
      extra++;
    }
  }

  // An empty cell for each cell with no trap, and for a cell whose trap has no row yet: the cell stays in the grid.
  const keep = new Set<Element>();
  for (const c of cells) {
    if (c.trap && placed[String(c.trap)]) continue;
    let empty = popover.querySelector<HTMLElement>(':scope > .trapline-empty-cell[data-slot="' + CSS.escape(c.slot) + '"]');
    if (!empty) {
      empty = doc.createElement('div');
      empty.className = 'trapline-empty-cell';
      empty.setAttribute('data-slot', c.slot);
      // CoreUI1 tells Unity which areas are HUD from [data-interactive] and a few tags; a click on
      // any other area goes to the world behind the HUD. With it, an empty cell click does nothing.
      empty.setAttribute('data-interactive', '');
      popover.appendChild(empty);
    }
    // A cell whose trap has no row yet is not a free trap slot: no free slot card and no Set Trap menu.
    if (empty.classList.contains('trapline-pending') !== !!c.trap) empty.classList.toggle('trapline-pending', !!c.trap);
    placeCell(empty, GRID_PREFIX, c.row, c.col);
    keep.add(empty);
  }
  for (const ours of popover.querySelectorAll(':scope > .trapline-empty-cell')) {
    if (!keep.has(ours)) ours.remove();
  }

  // The durability mark of a trap (the red danger cell): a node of the mod over the same grid cell as the game row, so
  // Vue keeps its row. It has no text; data-percent holds the durability percent for the game driver.
  const keepMarks = new Set<Element>();
  for (const id in placed) {
    const mark = byTrap[id].mark;
    if (mark === undefined || mark === null) continue;
    let el = popover.querySelector<HTMLElement>(':scope > .trapline-mark[data-trap="' + id + '"]');
    if (!el) {
      el = doc.createElement('div');
      el.className = 'trapline-mark';
      el.setAttribute('data-trap', id);
      popover.appendChild(el);
    }
    const percent = String(mark);
    if (el.getAttribute('data-percent') !== percent) el.setAttribute('data-percent', percent);
    placeCell(el, GRID_PREFIX, byTrap[id].row, byTrap[id].col);
    keepMarks.add(el);
  }
  for (const ours of popover.querySelectorAll(':scope > .trapline-mark')) {
    if (!keepMarks.has(ours)) ours.remove();
  }

  // As many cell columns as the longest line has cells, so the popover (as wide as its content in the flex-start
  // .trap-panel) ends right after the cells.
  const columns = Math.max(groups.cols || 1, Math.min(extra, CELLS_PER_LINE));
  const lines = (groups.lines || []).map((l) => ({ key: l.key, kind: l.kind === 'zone' ? 'zone' as const : 'floor' as const, row: l.row, name: l.name }));
  drawFloorGrid(doc, popover, { prefix: GRID_PREFIX, columns, lines });

  // The last line of the list, after the line of the traps with no cell.
  applyLast(doc, popover, groups.rows + Math.ceil(extra / CELLS_PER_LINE) + 1, last.words, last.replace);

  if (!popover.classList.contains('trapline-grid')) popover.classList.add('trapline-grid');
  return '';
}
