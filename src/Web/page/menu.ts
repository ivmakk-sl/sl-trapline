// The Set Trap menu of a free trap slot: a click on a free trap slot cell asks C# for the trap kinds of the backpack and
// home storage (TRAPLINE_MENU), and the menu opens under the cell in the look of the dropdown library; a click on a line
// asks C# to set a trap of that kind (TRAPLINE_SET).
import { hideCard } from './card';
import { MENU_PREFIX, post } from './core';
import type { CoreWindow, MenuData } from './types';
import { closeDropdownList, isDropdownListOpen, openDropdownList } from '../../Shared/dropdown/web/dropdown';

function escapeHtml(text: string): string {
  return text.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;');
}

// The click on a free trap slot cell, in the bubble phase, so the dropdown library has closed an open menu first.
export function watchMenu(w: CoreWindow): void {
  if (w.__traplineMenu) return;
  w.__traplineMenu = true;
  w.document.addEventListener('click', (e) => {
    const target = e.target as Element | null;
    const cell = target && target.closest ? target.closest('.trapline-empty-cell') : null;
    if (!cell || cell.classList.contains('trapline-pending')) return;
    const slot = cell.getAttribute('data-slot');
    if (slot) post('TRAPLINE_MENU', { slot });
  });
}

// C# sends the lines of the menu of a slot. The menu opens under the free trap slot cell of that slot, or not at all
// when the cell is gone. With no line, it closes an open menu and opens none: C# shows the game's floating message for
// no trap. Returns 'ok'.
export function setMenu(w: CoreWindow | null, menu: MenuData): string {
  if (!w) return 'no CoreUI1 frame';
  const doc = w.document;
  const lines = menu.lines || [];
  if (!lines.length) {
    closeDropdownList(doc, MENU_PREFIX);
    return 'ok';
  }
  const cell = doc.querySelector('.trapline-empty-cell[data-slot="' + CSS.escape(menu.slot) + '"]');
  if (!cell) return 'ok';
  const choices = lines.map((l) => ({
    icon: { key: 'k' + l.kind, html: l.icon ? '<img src="' + escapeHtml(l.icon) + '" alt="">' : '' },
    text: l.name,
    count: l.count,
  }));
  hideCard(doc);
  openDropdownList(doc, cell, {
    prefix: MENU_PREFIX,
    choices,
    select: (i) => { if (lines[i]) post('TRAPLINE_SET', { slot: menu.slot, trap: lines[i].kind }); },
  });
  markCell(doc, cell);
  watchLeave(w, doc.querySelector('.' + MENU_PREFIX + '-anchored'));
  return 'ok';
}

// The cell of the open menu gets a mark until the list goes, whatever closes it (a line, a click outside, the mod).
function markCell(doc: Document, cell: Element): void {
  for (const old of doc.querySelectorAll('.trapline-menu-cell')) old.classList.remove('trapline-menu-cell');
  cell.classList.add('trapline-menu-cell');
  const w = doc.defaultView as CoreWindow & { __traplineMenuObserver?: MutationObserver };
  if (w.__traplineMenuObserver) return;
  w.__traplineMenuObserver = new w.MutationObserver(() => {
    if (isDropdownListOpen(doc, MENU_PREFIX)) return;
    for (const old of doc.querySelectorAll('.trapline-menu-cell')) old.classList.remove('trapline-menu-cell');
  });
  w.__traplineMenuObserver.observe(doc.body, { childList: true });
}

// A click in the game world does not reach the HUD page, so the click-outside close of the dropdown library misses it.
// The menu also closes when the pointer is out of the trap list and out of the menu for LEAVE_MS; the delay lets the
// pointer cross the gap between the cell and the menu.
const LEAVE_MS = 400;
let leaveTimer = 0;

function watchLeave(w: CoreWindow, list: Element | null): void {
  const doc = w.document;
  const popover = doc.querySelector('.trap-popover');
  const out = () => {
    w.clearTimeout(leaveTimer);
    leaveTimer = w.setTimeout(() => closeDropdownList(doc, MENU_PREFIX), LEAVE_MS);
  };
  const back = () => { w.clearTimeout(leaveTimer); };
  w.clearTimeout(leaveTimer);
  if (list) {
    list.addEventListener('mouseleave', out);
    list.addEventListener('mouseenter', back);
  }
  const watched = popover as (Element & { __traplineLeave?: boolean }) | null;
  if (watched && !watched.__traplineLeave) {
    watched.__traplineLeave = true;
    watched.addEventListener('mouseleave', () => { if (isDropdownListOpen(doc, MENU_PREFIX)) out(); });
    watched.addEventListener('mouseenter', back);
  }
}
