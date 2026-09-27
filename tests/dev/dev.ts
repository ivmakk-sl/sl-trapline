// The root page of the dev harness: loads the page script, then gives CoreUI1 a fake trap state
// through the game's own trap message, and the mod data through setData, as the plugin does.
import '../../src/Web/page/main';
import type { PageData } from '../../src/Web/page/types';
import data from '../fixtures/data.json';

// The traps of tests/fixtures/data.json: 11 (with prey) and 13 on Home, 12 in The "Cellar", and 14 with
// no cell, which goes to the line after all floors.
const traps = [
  { trapInstanceId: 11, trapName: 'Snare', roomName: 'Balcony', iconUrl: '', status: 1 },
  { trapInstanceId: 12, trapName: 'Snare', roomName: 'Cellar', iconUrl: '', status: 0 },
  { trapInstanceId: 13, trapName: 'Snare', roomName: 'Garden', iconUrl: '', status: 2 },
  { trapInstanceId: 14, trapName: 'Snare', roomName: 'Roof', iconUrl: '', status: 0 },
];

const frame = document.querySelector('iframe')!;
let started = false;

function start(): void {
  if (started) return;
  started = true;
  const core = frame.contentWindow as Window & typeof globalThis;
  core.postMessage({ type: 'WebUI_CoreUI1_TrapMsg', data: { containerActive: true, traps } }, '*');
  // Open the trap list, so the grid shows, after Vue has drawn the trap panel.
  setTimeout(() => {
    core.eval('state.trapsExpanded = true');
    setTimeout(() => {
      const result = window.__trapline!.setData(data as PageData);
      console.log('Trapline setData:', result, window.__trapline!.check());
    }, 100);
  }, 100);
}

// CoreUI1 takes messages only after its own start, and then calls this function of the root page, as
// Root.html has it in the game.
(window as Window & { notifyPageReady?: (pageId: string) => void }).notifyPageReady = (pageId) => {
  if (pageId === 'CoreUI1') start();
};
