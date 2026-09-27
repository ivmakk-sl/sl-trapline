// The apply pass over the CoreUI1 frame, its body observer, and the counts that check() reports.
import { ensureStyle, findFrame } from './core';
import { applyGroups } from './grid';
import { applyButton } from './takeAll';
import type { CoreState, CoreWindow, Groups } from './types';

// The data of the last setData: the button word (an English default until C# sends one) and the floor
// groups (null until C# has pushed group data at least once, in which case the grid stays off).
export const view: { takeAll: string; groups: Groups | null } = { takeAll: 'Take All', groups: null };

let applying = false;

// The runs of the body observer and the apply passes since the last check(), for the Verbose log of C#.
let observerRuns = 0;
let passes = 0;

// Applies every feature to one CoreUI1 window and (re)installs its body observer. Returns the
// "installed"/"missing:"/"error:" text that setData() hands back to C#.
function apply(w: CoreWindow): string {
  passes++;
  const doc = w.document;
  let state: CoreState;
  try { state = w.eval('state'); } catch (e) { return 'error: state not reachable (' + e + ')'; }

  ensureStyle(doc);
  const missing: string[] = [];
  try {
    const m1 = applyButton(doc, state, view.takeAll); if (m1) missing.push(m1);
    const m2 = applyGroups(doc, state, view.groups); if (m2) missing.push(m2);
  } catch (e) { return 'error: ' + e; }

  if (!w.__traplineObserver) {
    const observer = new MutationObserver(() => {
      observerRuns++;
      // Guards against the observer re-entering on the DOM changes applyButton/applyGroups/ensureStyle
      // themselves just made (childList+subtree on body sees those too).
      if (applying) return;
      applying = true;
      try {
        ensureStyle(doc);
        const s: CoreState = w.eval('state');
        applyButton(doc, s, view.takeAll);
        applyGroups(doc, s, view.groups);
      } catch (e) { /* a page update broke a part; the next setData() call reports it */ }
      finally { applying = false; }
    });
    observer.observe(doc.body, { childList: true, subtree: true });
    w.__traplineObserver = observer;
  }

  return missing.length ? ('installed; missing: ' + missing.join(', ')) : 'installed';
}

// One apply pass with the current data. No retry: when the CoreUI1 frame is not there, C# sends again.
export function run(): string {
  const w = findFrame();
  if (!w) return 'no CoreUI1 frame';
  return apply(w);
}

// "ok <runs> <passes>" while the CoreUI1 frame has the body observer, with the counts since the last
// check; any other answer makes C# send the data again.
export function check(): string {
  const w = findFrame();
  if (!w) return 'no CoreUI1 frame';
  if (!w.__traplineObserver) return 'no observer';
  const result = 'ok ' + observerRuns + ' ' + passes;
  observerRuns = 0;
  passes = 0;
  return result;
}
