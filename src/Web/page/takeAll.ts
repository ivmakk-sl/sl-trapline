// The Take All button in the header row of the HUD trap list.
import { FEATURES, panelShouldRender, partsMissing } from './core';
import type { CoreState } from './types';

// Sends exactly what the page's own sendMessage/UnitySendEvent would send from inside the CoreUI1
// iframe (window.parent.postMessage). This whole script runs in the root page already, so its own
// top-level "window" identifier already IS that parent/root window.
function postTakeAll(): void {
  window.postMessage({ type: 'TRAPLINE_TAKE_ALL', data: {}, sourcePageId: 'CoreUI1' }, '*');
}

// Adds (or removes) the Take All button just before the header row's chevron. Idempotent: a
// button already there is kept (only its text is refreshed) and never duplicated. A row that Vue
// recreates (the trap panel goes from 0 traps to more than 0 again) gets a fresh button, because the
// lookup is always against the CURRENT row, never a cached one. Returns the missing parts, or ''.
export function applyButton(doc: Document, state: CoreState, word: string): string {
  if (!panelShouldRender(state)) return '';
  const missing = partsMissing(doc, 'button');
  if (missing.length) return 'button(' + missing.join(',') + ')';

  const row = doc.querySelector(FEATURES.button[0])!;
  const chevron = doc.querySelector(FEATURES.button[1])!;
  let btn = row.querySelector<HTMLButtonElement>('.trapline-take-all');
  const urgent = state.trapsUrgentCount > 0;

  if (!urgent) { if (btn) btn.remove(); return ''; }

  if (!btn) {
    btn = doc.createElement('button');
    btn.type = 'button';
    btn.className = 'trapline-take-all';
    btn.addEventListener('click', (e) => {
      e.stopPropagation();
      postTakeAll();
    });
    row.insertBefore(btn, chevron);
  } else if (btn.nextSibling !== chevron) {
    row.insertBefore(btn, chevron);
  }
  // Setting .textContent always replaces the text node, even to an identical value, which the body
  // observer would see as a fresh mutation and re-apply forever - so this only writes on a real change.
  if (btn.textContent !== word) btn.textContent = word;
  return '';
}
