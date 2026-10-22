// Library copy of shared/hud-pin 2.0.0. Do not edit: see src/Shared/hud-pin/VERSION.
// The pin of a HUD list: a push pin button at a place that the mod names, an outline when the list is not pinned and
// filled when it is. While the list is pinned, the library keeps the open field of the page state true: a watch of the
// page's own Vue with flush 'sync' sets it back in the same tick when the game sets it to false, so Vue never renders
// the closed list. Each class name and CSS variable comes from the prefix of the mod: the classes <prefix>-pin, -pin-on,
// and -pin-head, and the variable --<prefix>-pin-color (see hudPin.css).
import css from './hudPin.css?inline';

export interface PinOptions {
  prefix: string;
  // The node that holds the pin, for example the free place at the right end of the last line of the list.
  place: Element | null;
  // The reactive state of the page, the name of its field that opens the list, and the watch of the page's Vue.
  state: object;
  field: string;
  watch: (source: () => unknown, callback: (value: unknown) => void, options: { flush: 'sync' }) => () => void;
  pinned: boolean;
  // A click on the pin gives the mod the new state.
  select: (pinned: boolean) => void;
}

// The keep-open watch of a state and field, one for each prefix.
interface Keeper {
  pinned: boolean;
  stop: () => void;
}

const keepers = new WeakMap<object, Map<string, Keeper>>();

// A push pin: the head, which the CSS fills when pinned, and the needle.
const PIN_SVG = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linejoin="round" stroke-linecap="round">'
  + '<path class="PFX-pin-head" d="M8 3h8l-1.5 6.5 3.5 3.5v2H6v-2l3.5-3.5z"/><path d="M12 15v6"/></svg>';

// The CSS of the library with the prefix of the mod. The mod puts it between its tokens and its own rules.
export function hudPinCss(prefix: string): string {
  return css.replace(/PFX/g, prefix);
}

function keeper(state: object, prefix: string, field: string, watch: PinOptions['watch']): Keeper {
  let byPrefix = keepers.get(state);
  if (!byPrefix) keepers.set(state, byPrefix = new Map());
  const key = prefix + '\u0000' + field;
  let k = byPrefix.get(key);
  if (!k) {
    const created: Keeper = { pinned: false, stop: () => {} };
    const s = state as Record<string, unknown>;
    created.stop = watch(() => s[field], (value) => {
      if (created.pinned && value === false) s[field] = true;
    }, { flush: 'sync' });
    byPrefix.set(key, k = created);
  }
  return k;
}

// Draws the pin, only where it differs, and keeps the list open while it is pinned (and opens it at once). Returns '' or
// the missing part as 'pin(<part>)'; then no pin shows, and the list works as without the pin.
export function drawPin(opts: PinOptions): string {
  const p = opts.prefix;
  const s = opts.state as Record<string, unknown>;
  if (!opts.place) return 'pin(place)';
  if (!(opts.field in s)) return 'pin(' + opts.field + ')';
  const doc = opts.place.ownerDocument;
  let pin = opts.place.querySelector<HTMLElement & { __pinSelect?: (pinned: boolean) => void }>(':scope > .' + p + '-pin');
  if (!pin) {
    pin = doc.createElement('button') as HTMLElement & { __pinSelect?: (pinned: boolean) => void };
    pin.className = p + '-pin unity-interactive';
    pin.setAttribute('type', 'button');
    pin.setAttribute('data-interactive', '');
    pin.innerHTML = PIN_SVG.replace(/PFX/g, p);
    const node = pin;
    pin.addEventListener('click', (e) => {
      e.stopPropagation();
      if (node.__pinSelect) node.__pinSelect(!node.classList.contains(p + '-pin-on'));
    });
    opts.place.appendChild(pin);
  }
  pin.__pinSelect = opts.select;
  if (pin.classList.contains(p + '-pin-on') !== opts.pinned) pin.classList.toggle(p + '-pin-on', opts.pinned);

  const k = keeper(opts.state, p, opts.field, opts.watch);
  k.pinned = opts.pinned;
  if (opts.pinned && s[opts.field] === false) s[opts.field] = true;
  return '';
}

// Removes each pin of the prefix and stops keeping the list of the state open.
export function removePin(root: Document | Element, prefix: string, state: object | null): void {
  for (const el of root.querySelectorAll('.' + prefix + '-pin')) el.remove();
  const byPrefix = state ? keepers.get(state) : undefined;
  if (!byPrefix) return;
  for (const [key, k] of byPrefix) {
    if (!key.startsWith(prefix + '\u0000')) continue;
    k.stop();
    byPrefix.delete(key);
  }
}
