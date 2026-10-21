// Runs in the root page. Adds the Auto-replace checkbox of the trap slot of the trap window (TrapUI/TrapPanel.html) to
// the header of that window. C# sends the slot, its option, and the mod texts with setState() when the window opens,
// and then calls install(). A reopened window is a new iframe, so install() finds the frame again each time, and it
// retries every 300 ms for 3 seconds when the frame does not exist yet. A frame observer puts the checkbox back when
// Vue renders it away. install() returns "installed", "no frame", "missing: <parts>", or "error: <text>".
//
// Vite builds this entry into one IIFE file (obj/page/trappanel.js). C# sends it with each setState, and a second run
// keeps the first interface and its state.
import tokensCss from '../tokens.css?inline';
import panelCss from '../trappanel.css?inline';
import { word } from '../../Shared/i18n/web/words';

interface TrapState {
  slot: string;
  on: boolean;
  words: Record<string, string>;
}

// The game parts that the checkbox needs. A missing one is reported, and only the checkbox is off.
const PARTS = ['.header'];

const store: { current: TrapState } = { current: { slot: '', on: true, words: {} } };

type WatchedDoc = Document & { __traplineObserver?: MutationObserver };

declare global {
  interface Window {
    __traplineTrap?: { setState(state: Partial<TrapState> | null): void; install(): string };
  }
}

// The document of each open trap window.
function windowDocs(): Document[] {
  const frames = document.querySelectorAll('iframe'), found: Document[] = [];
  for (let i = 0; i < frames.length; i++) {
    let w: Window | null;
    try { w = frames[i].contentWindow; } catch (e) { continue; }
    if (!w || !/TrapPanel\.html/i.test(String(w.location))) continue;
    const doc = w.document;
    if (!doc || !doc.body || doc.readyState === 'loading') continue;
    found.push(doc);
  }
  return found;
}

function missingParts(doc: Document): string[] {
  return PARTS.filter((part) => !doc.querySelector(part));
}

function ensureStyle(doc: Document): void {
  if (doc.getElementById('trapline-trap-style')) return;
  const style = doc.createElement('style');
  style.id = 'trapline-trap-style';
  style.textContent = tokensCss + '\n' + panelCss;
  doc.head.appendChild(style);
}

// Sends exactly what the page's own UnitySendEvent would send from the frame: this script runs in the root page.
function post(type: string, data: object): void {
  window.postMessage({ type, data, sourcePageId: 'TrapPanel' }, '*');
}

// The checkbox at the end of the header, only where it differs. Its slot is read at the click, so a window that opens
// for another trap keeps the right slot.
function apply(doc: Document): void {
  ensureStyle(doc);
  const header = doc.querySelector('.header')!;
  let label = header.querySelector<HTMLElement>(':scope > .trapline-slot-option');
  if (!label) {
    label = doc.createElement('label');
    label.className = 'trapline-slot-option';
    label.setAttribute('data-interactive', '');
    const box = doc.createElement('input');
    box.type = 'checkbox';
    box.addEventListener('change', (e) => {
      // The new state stays in the store, so a header that Vue renders again gets the checkbox as the player set it.
      store.current.on = (e.target as HTMLInputElement).checked;
      post('TRAPLINE_SLOT_OPTION', { slot: store.current.slot, on: store.current.on });
    });
    label.appendChild(box);
    const text = doc.createElement('span');
    text.className = 'trapline-slot-option-text';
    label.appendChild(text);
    header.appendChild(label);
  }
  const text = label.querySelector('.trapline-slot-option-text')!;
  const want = word(store.current.words, 'autoReplace');
  if (text.textContent !== want) text.textContent = want;
  const box = label.querySelector<HTMLInputElement>('input')!;
  // checked is a property, so this writes no DOM attribute.
  if (box.checked !== store.current.on) box.checked = store.current.on;
}

function watch(doc: WatchedDoc): void {
  if (doc.__traplineObserver) return;
  const Observer = doc.defaultView && (doc.defaultView as Window & typeof globalThis).MutationObserver;
  if (!Observer) return;
  doc.__traplineObserver = new Observer(() => {
    if (!missingParts(doc).length && !doc.querySelector('.header > .trapline-slot-option')) apply(doc);
  });
  doc.__traplineObserver.observe(doc.body, { childList: true, subtree: true });
}

function attempt(retries: number): string {
  try {
    const docs = windowDocs();
    if (!docs.length) {
      if (retries > 0) setTimeout(() => { attempt(retries - 1); }, 300);
      return 'no frame';
    }
    const missing: string[] = [];
    for (const doc of docs) {
      const m = missingParts(doc);
      if (m.length) { missing.push(...m); continue; }
      apply(doc);
      watch(doc);
    }
    return missing.length ? 'missing: ' + missing.join(', ') : 'installed';
  } catch (e) {
    return 'error: ' + (e && (e as Error).message ? (e as Error).message : String(e));
  }
}

function setState(state: Partial<TrapState> | null): void {
  const s = state || {};
  store.current = { slot: String(s.slot || ''), on: s.on !== false, words: s.words || {} };
  for (const doc of windowDocs()) if (!missingParts(doc).length) apply(doc);
}

window.__traplineTrap = window.__traplineTrap || { install: () => attempt(10), setState };
