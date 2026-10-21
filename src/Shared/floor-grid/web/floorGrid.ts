// Library copy of shared/floor-grid 1.0.0. Do not edit: see src/Shared/floor-grid/VERSION.
// The floor lines and zone lines of a HUD grid: the name, a rule to the right edge of the grid, and a free place at
// the right end of the rule for the content of the mod. The grid places (the rows of the lines, the place of each
// cell, the column count) come from the mod, for example from FloorGridLayout of cs/. Each class name and property
// comes from the prefix of the mod: <prefix>-fg (the grid), -fg-line, -fg-floor, -fg-zone, -fg-name, -fg-rule, -fg-end,
// and the properties --<prefix>-fg-cols, --<prefix>-fg-tracks, --<prefix>-fg-row, --<prefix>-fg-col.
import css from './floorGrid.css?inline';

export interface FloorGridLine {
  // The id of the line for the mod; a second draw finds the same node by it.
  key: string;
  kind: 'floor' | 'zone';
  // The grid row, from 1.
  row: number;
  name: string;
}

export interface FloorGridModel {
  prefix: string;
  // The cell columns of the grid: the most cells of one line.
  columns: number;
  lines: FloorGridLine[];
}

// The CSS of the library with the prefix of the mod. The mod puts it between its tokens and its own rules.
export function floorGridCss(prefix: string): string {
  return css.replace(/PFX/g, prefix);
}

// Writes one inline style property only where it differs: an observer of the page sees each write.
function setProp(el: HTMLElement, prop: string, value: string): void {
  if (el.style.getPropertyValue(prop) !== value) el.style.setProperty(prop, value);
}

function span(doc: Document, cls: string): HTMLElement {
  const el = doc.createElement('span');
  el.className = cls;
  return el;
}

// Draws the lines of the model into the grid node, only where they differ, and removes each line of the prefix that
// the model no longer has. Gives the free place at the right end of each line by its key; the library keeps what the
// mod puts there.
export function drawFloorGrid(doc: Document, grid: HTMLElement, model: FloorGridModel): Map<string, HTMLElement> {
  const p = model.prefix;
  if (!grid.classList.contains(p + '-fg')) grid.classList.add(p + '-fg');
  const cols = Math.max(1, model.columns);
  setProp(grid, '--' + p + '-fg-cols', String(cols));
  // The last cell column is the one that a line can widen, so the grid has no gap after the cells.
  const cell = 'var(--' + p + '-fg-cell)';
  const last = 'minmax(' + cell + ', auto)';
  setProp(grid, '--' + p + '-fg-tracks', cols > 1 ? 'repeat(' + (cols - 1) + ', ' + cell + ') ' + last : last);

  const ends = new Map<string, HTMLElement>();
  const keep = new Set<HTMLElement>();
  for (const line of model.lines) {
    let el = grid.querySelector<HTMLElement>(':scope > .' + p + '-fg-line[data-fg-key="' + CSS.escape(line.key) + '"]');
    if (!el) {
      el = doc.createElement('div');
      el.className = p + '-fg-line';
      el.setAttribute('data-fg-key', line.key);
      el.appendChild(span(doc, p + '-fg-name'));
      el.appendChild(span(doc, p + '-fg-rule'));
      el.appendChild(span(doc, p + '-fg-end'));
      grid.appendChild(el);
    }
    const floor = line.kind === 'floor';
    if (el.classList.contains(p + '-fg-floor') !== floor) el.classList.toggle(p + '-fg-floor', floor);
    if (el.classList.contains(p + '-fg-zone') === floor) el.classList.toggle(p + '-fg-zone', !floor);
    setProp(el, '--' + p + '-fg-row', String(line.row));
    const name = el.querySelector(':scope > .' + p + '-fg-name')!;
    if (name.textContent !== line.name) name.textContent = line.name;
    ends.set(line.key, el.querySelector<HTMLElement>(':scope > .' + p + '-fg-end')!);
    keep.add(el);
  }
  for (const el of grid.querySelectorAll<HTMLElement>(':scope > .' + p + '-fg-line')) {
    if (!keep.has(el)) el.remove();
  }
  return ends;
}

// Sets the grid place of a cell (a node of the mod, or a game node whose class the game owns) as two properties.
export function placeCell(el: HTMLElement, prefix: string, row: number, column: number): void {
  setProp(el, '--' + prefix + '-fg-row', String(row));
  setProp(el, '--' + prefix + '-fg-col', String(column));
}

// Removes each line, the grid class, and each property of the prefix in the document, so the grid shows as without
// the library. A property of the game stays.
export function removeFloorGrid(root: Document | Element, prefix: string): void {
  for (const el of root.querySelectorAll('.' + prefix + '-fg-line')) el.remove();
  for (const el of root.querySelectorAll('.' + prefix + '-fg')) el.classList.remove(prefix + '-fg');
  for (const el of root.querySelectorAll<HTMLElement>('[style*="--' + prefix + '-fg-"]')) {
    for (const prop of ['cols', 'tracks', 'row', 'col']) {
      const name = '--' + prefix + '-fg-' + prop;
      if (el.style.getPropertyValue(name)) el.style.removeProperty(name);
    }
  }
}
