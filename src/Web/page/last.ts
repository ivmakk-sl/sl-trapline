// The last line of the open trap list: the Auto-replace checkbox with its hover tip, and a free place at its right
// end for the pin. A click on the checkbox posts TRAPLINE_REPLACE with its new state; C# writes the save key and sends
// the data again, which sets the checkbox.
import { post, setStyle } from './core';
import { word } from '../../Shared/i18n/web/words';

// The parts of the last line that other features fill: the free place at its right end.
export const LAST_END = 'trapline-last-end';

// Draws the last line at the grid row, only where it differs. With no mod texts yet, it draws nothing: the page has no
// word of its own.
export function applyLast(doc: Document, popover: HTMLElement, row: number, words: Record<string, string> | undefined, replace: boolean): void {
  let last = popover.querySelector<HTMLElement>(':scope > .trapline-last');
  if (!words) {
    if (last) last.remove();
    return;
  }
  if (!last) {
    last = doc.createElement('div');
    last.className = 'trapline-last';
    const label = doc.createElement('label');
    label.className = 'trapline-replace has-entry-tip';
    label.setAttribute('data-interactive', '');
    const box = doc.createElement('input');
    box.type = 'checkbox';
    box.addEventListener('click', (e) => { e.stopPropagation(); });
    box.addEventListener('change', (e) => {
      post('TRAPLINE_REPLACE', { on: (e.target as HTMLInputElement).checked });
    });
    label.appendChild(box);
    const text = doc.createElement('span');
    text.className = 'trapline-replace-text';
    label.appendChild(text);
    const tip = doc.createElement('div');
    tip.className = 'entry-tip trapline-tip';
    label.appendChild(tip);
    last.appendChild(label);
    const end = doc.createElement('div');
    end.className = LAST_END;
    last.appendChild(end);
    popover.appendChild(last);
  }
  setStyle(last, '--tl-fg-row', String(row));
  setStyle(last, 'grid-column', '1 / -1');
  const text = last.querySelector('.trapline-replace-text')!;
  const label = word(words, 'autoReplace');
  if (text.textContent !== label) text.textContent = label;
  const tip = last.querySelector('.trapline-replace > .entry-tip')!;
  const tipText = word(words, 'autoReplaceTip');
  if (tip.textContent !== tipText) tip.textContent = tipText;
  const box = last.querySelector<HTMLInputElement>('.trapline-replace input')!;
  // checked is a property, so this writes no DOM attribute.
  if (box.checked !== replace) box.checked = replace;
}
