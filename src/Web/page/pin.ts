// The pin of the HUD trap list, at the right end of the last line of the open list (the HUD pin library). While the list
// is pinned, it stays open when the game would close it (the plant list, the search, Escape), and it opens again after
// the game rebuilt the web UI. A click on the chevron of a pinned list unpins it, and the game's own click handler then
// closes the list.
import { FEATURES, panelShouldRender, partsMissing, post } from './core';
import { LAST_END } from './last';
import type { CoreState, CoreWindow } from './types';
import { drawPin, removePin } from '../../Shared/hud-pin/web/hudPin';

export const PIN_PREFIX = 'tl';

// The field of the CoreUI1 state that opens the trap list.
const FIELD = 'trapsExpanded';

// The pin of the last data of C#: a click on the chevron sets it off at once, before C# sends it again.
let view: { pin: boolean } = { pin: false };

type VueWindow = CoreWindow & { Vue?: { watch: Parameters<typeof drawPin>[0]['watch'] } };

// Draws the pin and keeps the list open while it is pinned. Returns the missing parts as 'pin(<part>)', or ''.
export function applyPin(w: VueWindow, state: CoreState, data: { pin: boolean }): string {
  view = data;
  const pinned = data.pin;
  if (!panelShouldRender(state)) return '';
  const doc = w.document;
  const missing = partsMissing(doc, 'pin');
  if (!w.Vue) missing.push('Vue');
  if (missing.length) {
    removePin(doc, PIN_PREFIX, state);
    return 'pin(' + missing.join(',') + ')';
  }
  watchChevron(w);
  // A pinned list that the game shows closed (a new frame, or the list back after no trap) opens; Vue then renders the
  // list, and the next pass draws the pin in its last line.
  if (pinned && !state[FIELD]) state[FIELD] = true;
  const place = doc.querySelector('.trap-popover .' + LAST_END);
  // With no last line yet (the list is still rendering) the pin draws at the next pass.
  if (place) drawPin({ prefix: PIN_PREFIX, place, state, field: FIELD, watch: w.Vue!.watch, pinned, select: (on) => post('TRAPLINE_PIN', { on }) });
  return '';
}

// A click on the header row of a pinned list (its chevron, not the Take All button) unpins it before the game's own
// handler toggles the list, so that handler closes it.
function watchChevron(w: VueWindow): void {
  if (w.__traplineChevron) return;
  w.__traplineChevron = true;
  w.document.addEventListener('click', (e) => {
    const target = e.target as Element | null;
    if (!view.pin || !target || !target.closest) return;
    if (!target.closest(FEATURES.pin[0]) || target.closest('.trapline-take-all')) return;
    view.pin = false;
    let state: CoreState;
    try { state = w.eval('state'); } catch (err) { return; }
    const place = w.document.querySelector('.trap-popover .' + LAST_END);
    if (place && w.Vue) drawPin({ prefix: PIN_PREFIX, place, state, field: FIELD, watch: w.Vue.watch, pinned: false, select: (on) => post('TRAPLINE_PIN', { on }) });
    else removePin(w.document, PIN_PREFIX, state);
    post('TRAPLINE_PIN', { on: false });
  }, true);
}
