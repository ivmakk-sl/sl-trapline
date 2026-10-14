// Runs in the root page, reaching the CoreUI1 iframe through iframe.contentWindow, as in Project Cook.
// Defines window.__trapline once. C# then calls window.__trapline.setData({words: {takeAll}, groups})
// when the button word or the floor groups change (a language switch, a trap placed or picked up, a
// floor or area unlocked), and window.__trapline.check() about once each second while nothing changes.
// Each call applies once and does not retry: when the CoreUI1 frame is not there, C# sends again later.
// A second full send of the script keeps the first interface and its state.
import { check, run, view } from './install';
import type { PageData } from './types';

// Stores the mod texts and the floor groups, and applies them in one pass. groups null turns the
// grid off.
function setData(data: PageData): string {
  if (data && data.words) view.words = data.words;
  view.groups = (data && data.groups) || null;
  return run();
}

window.__trapline = window.__trapline || { setData, check };
