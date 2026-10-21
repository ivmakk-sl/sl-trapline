// Library copy of shared/dropdown 1.1.0. Do not edit: see src/Shared/dropdown/VERSION.
// The dropdown of a sort, in a toolbar below a grid: a button with an icon and a word, and a list of choices that
// opens above it. The same list also opens with no button under a node that the mod names (an anchor, for example a
// grid cell). Each class name comes from the prefix of the mod: <prefix> (the root), <prefix>-btn, -menu, -opt, -on,
// -active, -ico, -text, -break, and for the list under an anchor -anchored, -count, -dim.
import css from './dropdown.css?inline';

// An icon: key names it (data-choice on the icon node, for the icon colors of the mod), html is its markup.
export interface DropdownIcon { key: string; html: string }

export interface DropdownOptions {
  prefix: string;
  // A selector of the first game button of the toolbar; the dropdown goes at its left.
  anchor: string;
  button: { icon: DropdownIcon; text: string };
  // A choice other than Default is on: the button shows the active look.
  active: boolean;
  choices: { icon: DropdownIcon; text: string }[];
  current: number;
  select: (index: number) => void;
}

// The list under an anchor: each choice with an icon, a word, an optional count at its right end, and the dim look
// for a choice that calls nothing.
export interface DropdownListOptions {
  prefix: string;
  choices: { icon: DropdownIcon; text: string; count?: number; dim?: boolean }[];
  select: (index: number) => void;
}

type DropdownRoot = HTMLElement & { __dropdown?: DropdownOptions };

// The CSS of the library with the prefix of the mod. The mod puts it between its tokens and its own rules.
export function dropdownCss(prefix: string): string {
  return css.replace(/PFX/g, prefix);
}

function button(doc: Document, cls: string): HTMLButtonElement {
  const b = doc.createElement('button');
  b.type = 'button';
  b.className = cls + ' unity-interactive';
  b.setAttribute('data-interactive', '');
  return b;
}

// Writes the icon and the word of a button or a choice, only where they differ. The icon markup is written only
// when its key changes.
function drawLabel(doc: Document, prefix: string, el: HTMLElement, icon: DropdownIcon, text: string): void {
  let ico = el.querySelector(':scope > .' + prefix + '-ico') as HTMLElement | null;
  let label = el.querySelector(':scope > .' + prefix + '-text') as HTMLElement | null;
  if (!ico || !label) {
    el.textContent = '';
    ico = doc.createElement('span');
    ico.className = prefix + '-ico';
    label = doc.createElement('span');
    label.className = prefix + '-text';
    el.appendChild(ico);
    el.appendChild(label);
  }
  if (ico.dataset.choice !== icon.key) {
    ico.dataset.choice = icon.key;
    ico.innerHTML = icon.html;
  }
  if (label.textContent !== text) label.textContent = text;
}

// One click listener for each document and prefix, in the capture phase, so it also sees a click that a button
// stops. It closes each open list whose dropdown does not hold the target, and it does not stop the click.
function listen(doc: Document, prefix: string): void {
  const mark = '__dropdownListener_' + prefix;
  const marked = doc as Document & Record<string, unknown>;
  if (marked[mark]) return;
  marked[mark] = true;
  doc.addEventListener('click', (e) => {
    const target = e.target as Node | null;
    doc.querySelectorAll('.' + prefix + '-menu').forEach((el) => {
      const menu = el as HTMLElement;
      if (!menu.hidden && !(target && menu.parentElement && menu.parentElement.contains(target))) menu.hidden = true;
    });
    const list = doc.querySelector('.' + prefix + '-anchored');
    if (list && !(target && list.contains(target))) list.remove();
  }, true);
}

function addChoice(doc: Document, root: DropdownRoot, menu: HTMLElement, prefix: string): void {
  const opt = button(doc, prefix + '-opt');
  opt.addEventListener('click', (e) => {
    e.stopPropagation();
    menu.hidden = true;
    const index = Array.prototype.indexOf.call(menu.children, opt);
    if (root.__dropdown) root.__dropdown.select(index);
  });
  menu.appendChild(opt);
}

// Draws the dropdown at the left of the anchor button, on its line, only where it differs. A toolbar with the robot
// switch (has-rsw) wraps, so a full-width break keeps the switch on its own line and puts the dropdown on the line of
// the button. With no anchor button the dropdown is the first child of the toolbar.
export function drawDropdown(doc: Document, toolbar: Element, opts: DropdownOptions): void {
  const p = opts.prefix;
  listen(doc, p);
  let root = toolbar.querySelector(':scope > .' + p) as DropdownRoot | null;
  if (!root) {
    root = doc.createElement('div') as DropdownRoot;
    root.className = p;
    const btn = button(doc, p + '-btn');
    const menu = doc.createElement('div');
    menu.className = p + '-menu';
    menu.hidden = true;
    btn.addEventListener('click', (e) => {
      e.stopPropagation();
      menu.hidden = !menu.hidden;
    });
    root.appendChild(btn);
    root.appendChild(menu);
  }
  root.__dropdown = opts;
  const anchor = Array.prototype.find.call(toolbar.children, (c: Element) => c !== root && c.matches(opts.anchor)) as Element | undefined;
  if (anchor ? root.nextElementSibling !== anchor : toolbar.firstElementChild !== root)
    toolbar.insertBefore(root, anchor || toolbar.firstChild);
  let brk = toolbar.querySelector(':scope > .' + p + '-break');
  if (toolbar.classList.contains('has-rsw')) {
    if (!brk) {
      brk = doc.createElement('div');
      brk.className = p + '-break';
    }
    if (root.previousElementSibling !== brk) toolbar.insertBefore(brk, root);
  } else if (brk) {
    brk.remove();
  }

  const btn = root.querySelector(':scope > .' + p + '-btn') as HTMLElement;
  drawLabel(doc, p, btn, opts.button.icon, opts.button.text);
  if (btn.classList.contains(p + '-active') !== opts.active) btn.classList.toggle(p + '-active', opts.active);
  const menu = root.querySelector(':scope > .' + p + '-menu') as HTMLElement;
  while (menu.children.length > opts.choices.length) menu.lastElementChild!.remove();
  while (menu.children.length < opts.choices.length) addChoice(doc, root, menu, p);
  opts.choices.forEach((choice, i) => {
    const opt = menu.children[i] as HTMLElement;
    drawLabel(doc, p, opt, choice.icon, choice.text);
    // Gold means a sort is on: with Default (not active) no choice is marked.
    const on = opts.active && i === opts.current;
    if (opt.classList.contains(p + '-on') !== on) opt.classList.toggle(p + '-on', on);
  });
}

export function removeDropdown(toolbar: Element | null, prefix: string): void {
  if (!toolbar) return;
  toolbar.querySelectorAll(':scope > .' + prefix + ', :scope > .' + prefix + '-break').forEach((el) => el.remove());
}

// The space between the anchor and the list.
const LIST_GAP = 4;

// Opens the list under the anchor, with its left edge at the left edge of the anchor, and above the anchor when it
// would pass the bottom of the window. One list of a prefix shows at a time: a new open replaces the old list. The list
// closes when the player selects a choice that is not dim, or clicks outside the list (that click still reaches its
// target), or when the mod closes it. The body of a HUD page can have a CSS zoom (HUD Scale changes it), which scales
// the left and the top of the list, so the anchor is divided by it.
export function openDropdownList(doc: Document, anchor: Element, opts: DropdownListOptions): void {
  const p = opts.prefix;
  listen(doc, p);
  closeDropdownList(doc, p);
  const root = doc.createElement('div');
  root.className = p + ' ' + p + '-anchored';
  root.setAttribute('data-interactive', '');
  const menu = doc.createElement('div');
  menu.className = p + '-menu';
  root.appendChild(menu);
  opts.choices.forEach((choice, i) => {
    const opt = button(doc, p + '-opt' + (choice.dim ? ' ' + p + '-dim' : ''));
    drawLabel(doc, p, opt, choice.icon, choice.text);
    if (typeof choice.count === 'number') {
      const count = doc.createElement('span');
      count.className = p + '-count';
      count.textContent = String(choice.count);
      opt.appendChild(count);
    }
    opt.addEventListener('click', (e) => {
      e.stopPropagation();
      if (choice.dim) return;
      root.remove();
      opts.select(i);
    });
    menu.appendChild(opt);
  });
  doc.body.appendChild(root);

  const w = doc.defaultView || window;
  const zoom = (root as HTMLElement & { currentCSSZoom?: number }).currentCSSZoom || 1;
  const box = anchor.getBoundingClientRect();
  const height = root.offsetHeight;
  let top = box.bottom / zoom + LIST_GAP;
  if (top + height > w.innerHeight / zoom) top = box.top / zoom - LIST_GAP - height;
  root.style.left = Math.round(box.left / zoom) + 'px';
  root.style.top = Math.round(Math.max(0, top)) + 'px';
}

// Closes the list under an anchor of the prefix, if one is open.
export function closeDropdownList(doc: Document, prefix: string): void {
  doc.querySelectorAll('.' + prefix + '-anchored').forEach((el) => el.remove());
}

export function isDropdownListOpen(doc: Document, prefix: string): boolean {
  return !!doc.querySelector('.' + prefix + '-anchored');
}
