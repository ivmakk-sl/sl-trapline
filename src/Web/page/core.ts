// The helpers that both features use: the game parts, the frame lookup, the style node, and the one
// write of an inline style.
import tokensCss from '../tokens.css?inline';
import pageCss from '../page.css?inline';
import type { CoreState, CoreWindow } from './types';

// The game selectors that each feature needs. A missing one is reported as a missing part, so a game
// update shows in the log and in the page tests.
export const FEATURES = {
  button: ['.trap-toggle-row', '.trap-toggle-chevron'],
  groups: ['.trap-popover', '.trap-item-row', '.trap-item-name', '.trap-item-status', '.trap-item-icon-img']
};

export function partsMissing(doc: Document, feature: keyof typeof FEATURES): string[] {
  return FEATURES[feature].filter((part) => !doc.querySelector(part));
}

// The trap panel (and so the header row the button lives in) renders only while the game has at least
// one placed trap. Reading this from the page's own state, instead of testing the DOM for it, keeps a
// plain empty trap list from ever being reported as a "missing" part.
export function panelShouldRender(state: CoreState | undefined): boolean {
  return !!(state && state.trapsActive && state.traps && state.traps.length > 0);
}

// `.trap-popover` (and so `.trap-item-row`) renders only while the panel renders AND the list is
// expanded (state.trapsExpanded), the second level of the two-level part check.
export function popoverShouldRender(state: CoreState | undefined): boolean {
  return panelShouldRender(state) && !!(state && state.trapsExpanded);
}

// Writes one inline style property only when its value differs: the body observer sees every write,
// even of an identical value, and would re-apply forever.
export function setStyle(el: HTMLElement, prop: string, value: string): void {
  if (el.style.getPropertyValue(prop) !== value) el.style.setProperty(prop, value);
}

// The one style node of the mod in the frame: the tokens, then the rules.
export function ensureStyle(doc: Document): void {
  if (doc.getElementById('trapline-style')) return;
  const style = doc.createElement('style');
  style.id = 'trapline-style';
  style.textContent = tokensCss + '\n' + pageCss;
  doc.head.appendChild(style);
}

// The CoreUI1 frame once it has loaded, looked up fresh on every call: a WebView reload gives it a new
// contentWindow, so nothing here caches it across calls.
export function findFrame(): CoreWindow | null {
  const frames = document.querySelectorAll('iframe');
  for (let i = 0; i < frames.length; i++) {
    const w = frames[i].contentWindow as CoreWindow | null;
    if (!w) continue;
    try { if (!/CoreUI1\.html/i.test(String(w.location))) continue; } catch (e) { continue; }
    if (w.document.readyState !== 'complete') continue;
    return w;
  }
  return null;
}
