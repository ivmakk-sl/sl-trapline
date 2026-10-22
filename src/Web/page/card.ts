// The trap card: with the pointer on a trap cell, the content of the game's trap card in the world
// (TrapUI/TrapFloatPopup.html) in the frame and the arrow of Greenline's pot card, pinned next to the cell.
import { MENU_PREFIX, post, setStyle } from './core';
import type { CardData, CoreState, CoreWindow } from './types';
import { isDropdownListOpen } from '../../Shared/dropdown/web/dropdown';
import { word } from '../../Shared/i18n/web/words';

// The space between the cell and the card, where the arrow sits (Greenline's pointer.ts).
const GAP = 10;

interface Anchor {
  left: number;
  top: number;
  right: number;
  bottom: number;
}

// The cell under the pointer: a trap cell with its trap, or a free trap slot cell (trap 0), or none; and the card data
// of the trap once C# sent it.
const hover: { el: Element | null; trap: number; card: CardData | null } = { el: null, trap: 0, card: null };

// The mod texts of the card of a free trap slot, from the last data of C#.
let words: Record<string, string> | undefined;

export function setCardWords(w: Record<string, string> | undefined): void {
  words = w;
}

function toggleClass(el: Element, cls: string, on: boolean): void {
  if (el.classList.contains(cls) !== on) el.classList.toggle(cls, on);
}

// Places the card right of the anchor with the arrow at the middle of the anchor, on the left side when it would pass
// the right edge of the window, and inside the window at the top and the bottom. A copy of placeCard of Greenline's
// pointer.ts. The body of CoreUI1 has a CSS zoom (HUD Scale changes it), which scales the node's left and top, so the
// anchor is divided by it.
export function placeCard(w: Window, node: HTMLElement, anchor: Anchor): void {
  const zoom = (node as HTMLElement & { currentCSSZoom?: number }).currentCSSZoom || 1;
  const width = node.offsetWidth, height = node.offsetHeight;
  const mid = (anchor.top + anchor.bottom) / 2 / zoom;
  let left = anchor.right / zoom + GAP;
  const leftSide = left + width > w.innerWidth / zoom;
  if (leftSide) left = anchor.left / zoom - GAP - width;
  const top = Math.round(Math.min(Math.max(0, mid - height / 2), Math.max(0, w.innerHeight / zoom - height)));
  setStyle(node, 'left', Math.round(Math.max(0, left)) + 'px');
  setStyle(node, 'top', top + 'px');
  setStyle(node, '--tl-arrow-y', Math.round(mid - top) + 'px');
  toggleClass(node, 'trapline-hover-left', leftSide);
}

// The trap of a game row of the grid: state.traps[i] is row i in page order.
function trapOfRow(row: Element, state: CoreState | undefined): number {
  const rows = row.parentElement ? row.parentElement.querySelectorAll(':scope > .trap-item-row') : [];
  const index = Array.prototype.indexOf.call(rows, row);
  const t = state && state.traps ? state.traps[index] : undefined;
  return t ? t.trapInstanceId : 0;
}

function el(doc: Document, tag: string, cls: string, text?: string): HTMLElement {
  const node = doc.createElement(tag);
  node.className = cls;
  if (text !== undefined) node.textContent = text;
  return node;
}

// The card node: one mod node at the end of the frame body that takes no pointer, so each click still reaches the
// HUD and the world. It moves with its cell, so it has the game's data-no-sync marker: CoreUI1 sends the places of its
// HUD parts to the game after each style change outside such a node.
function cardNode(doc: Document): HTMLElement {
  let node = doc.querySelector<HTMLElement>('.trapline-hover');
  if (!node) {
    node = el(doc, 'div', 'trapline-hover has-entry-tip');
    node.setAttribute('data-no-sync', '');
    doc.body.appendChild(node);
  }
  return node;
}

// The content of the game's trap card: the trap name, the durability with its bar, the bait, and the prey.
function drawCard(doc: Document, node: HTMLElement, card: CardData): void {
  const key = JSON.stringify(card);
  const tagged = node as HTMLElement & { __traplineCard?: string };
  if (tagged.__traplineCard === key) return;
  tagged.__traplineCard = key;
  while (node.firstChild) node.removeChild(node.firstChild);

  const tip = el(doc, 'div', 'entry-tip trapline-tip');
  tip.appendChild(el(doc, 'div', 'trapline-card-name', card.name));
  const dura = el(doc, 'div', 'trapline-card-dura');
  const label = el(doc, 'div', 'trapline-card-dura-row');
  label.appendChild(el(doc, 'span', 'trapline-card-dura-label', card.texts.durability));
  label.appendChild(el(doc, 'span', 'trapline-card-dura-value', card.cur + ' / ' + card.max));
  dura.appendChild(label);
  const bar = el(doc, 'div', 'trapline-card-bar');
  const fill = el(doc, 'div', 'trapline-card-fill' + (card.band ? ' ' + card.band : ''));
  fill.style.width = card.fill + '%';
  bar.appendChild(fill);
  dura.appendChild(bar);
  tip.appendChild(dura);

  const status = el(doc, 'div', 'trapline-card-statuses');
  status.appendChild(statusItem(doc, card.bait, card.baitIcon, BAIT_PLACEHOLDER, card.bait ? card.texts.baitSet : card.texts.noBait));
  status.appendChild(statusItem(doc, card.prey, card.preyIcon, PREY_PLACEHOLDER, card.prey ? card.texts.preyReady : card.texts.noPrey));
  tip.appendChild(status);
  node.appendChild(tip);
}

// The symbols of the game's trap card for a bait or a prey with no icon (TrapFloatPopup.html: &#x1F356; and &#x1F43E;).
const BAIT_PLACEHOLDER = '\u{1F356}';
const PREY_PLACEHOLDER = '\u{1F43E}';

function statusItem(doc: Document, active: boolean, icon: string, placeholder: string, text: string): HTMLElement {
  const item = el(doc, 'div', 'trapline-card-status' + (active ? ' active' : ''));
  const wrap = el(doc, 'div', 'trapline-card-icon');
  if (icon) {
    const img = doc.createElement('img');
    img.setAttribute('src', icon);
    img.setAttribute('alt', '');
    wrap.appendChild(img);
  } else {
    wrap.appendChild(el(doc, 'span', 'trapline-card-placeholder', placeholder));
  }
  item.appendChild(wrap);
  item.appendChild(el(doc, 'span', 'trapline-card-status-text', text));
  return item;
}

// The card of a free trap slot: its name and a short text that a click sets a trap.
function drawFreeSlot(doc: Document, node: HTMLElement): void {
  if (!words) return;
  const name = word(words, 'freeSlot'), hint = word(words, 'freeSlotHint');
  const key = JSON.stringify(['free', name, hint]);
  const tagged = node as HTMLElement & { __traplineCard?: string };
  if (tagged.__traplineCard === key) return;
  tagged.__traplineCard = key;
  while (node.firstChild) node.removeChild(node.firstChild);
  const tip = el(doc, 'div', 'entry-tip trapline-tip');
  tip.appendChild(el(doc, 'div', 'trapline-card-name', name));
  tip.appendChild(el(doc, 'div', 'trapline-card-hint', hint));
  node.appendChild(tip);
}

// Hides the card, for example while the Set Trap menu is open.
export function hideCard(doc: Document): void {
  const node = doc.querySelector<HTMLElement>('.trapline-hover');
  if (node && !node.hidden) node.hidden = true;
}

// A cell that left the page (the list closed, a trap broke, a trap was set on a free trap slot) gets no mouseleave, so
// each apply pass forgets it and hides its card.
export function dropGoneCell(doc: Document): void {
  if (!hover.el || hover.el.isConnected) return;
  hover.el = null;
  hover.trap = 0;
  hideCard(doc);
}

// Shows the card of the cell under the pointer once its data is there, or hides it. No card shows while the Set Trap
// menu is open.
function update(doc: Document): void {
  const free = !!hover.el && hover.trap === 0;
  const ready = free ? !!words : !!hover.el && !!hover.card && hover.card.trap === hover.trap;
  if (!ready || isDropdownListOpen(doc, MENU_PREFIX)) {
    hideCard(doc);
    return;
  }
  const card = cardNode(doc);
  if (free) drawFreeSlot(doc, card);
  else drawCard(doc, card, hover.card!);
  if (card.hidden) card.hidden = false;
  placeCard(doc.defaultView!, card, hover.el!.getBoundingClientRect());
}

// mouseenter and mouseleave do not bubble, so the listeners take them in the capture phase. A move on the same cell
// changes nothing, and the card asks C# once for each cell that the pointer comes onto.
export function watchPointer(w: CoreWindow): void {
  if (w.__traplinePointer) return;
  w.__traplinePointer = true;
  const doc = w.document;
  doc.addEventListener('mouseenter', (e) => {
    const target = e.target as Element;
    if (!target || !target.classList) return;
    if (target.classList.contains('trapline-empty-cell')) {
      if (target.classList.contains('trapline-pending')) return;
      hover.el = target;
      hover.trap = 0;
      update(doc);
      return;
    }
    if (!target.classList.contains('trap-item-row')) return;
    if (!target.closest('.trap-popover.trapline-grid')) return;
    let state: CoreState | undefined;
    try { state = w.eval('state'); } catch (err) { return; }
    const trap = trapOfRow(target, state);
    if (!trap) return;
    hover.el = target;
    hover.trap = trap;
    post('TRAPLINE_CARD', { trap });
    update(doc);
  }, true);
  doc.addEventListener('mouseleave', (e) => {
    if (e.target !== hover.el) return;
    hover.el = null;
    hover.trap = 0;
    update(doc);
  }, true);
}

// C# sends the card of the trap that the page asked for. A card of a cell that the pointer left already is kept but not
// shown. Returns 'ok'.
export function setCard(w: CoreWindow | null, card: CardData): string {
  hover.card = card;
  if (w) update(w.document);
  return 'ok';
}
