// The data that the page script reads: the fields of the CoreUI1 page state, and the data of setData.

// One trap of the CoreUI1 state (state.traps, filled from WebUI_CoreUI1_TrapMsg). state.traps[i] is the
// trap list row i in page order.
export interface CoreTrap {
  trapInstanceId: number;
  // 0 armed, 1 prey, 2 no bait.
  status: number;
}

// The fields of `const state = reactive({...})` in CoreUI1.html that the script reads.
export interface CoreState {
  trapsActive: boolean;
  traps: CoreTrap[];
  trapsExpanded: boolean;
  trapsUrgentCount: number;
}

// The CoreUI1 frame: a window whose top-level `state` the script reads with eval.
export type CoreWindow = Window & typeof globalThis;

// A floor line or a zone line of the trap grid (PageJson.Line in C#): its key, its grid row, and its name.
export interface GridLine {
  key: string;
  // 'floor' or 'zone'.
  kind: string;
  row: number;
  name: string;
}

// One cell of the trap grid (PageJson.Cell): the slot node name of its trap slot, its grid place, the
// instance id of the trap on it (none for a free trap slot), and the percent of its durability mark (none for no mark).
export interface GridCell {
  slot: string;
  row: number;
  col: number;
  trap?: number;
  mark?: number;
}

// The groups (PageJson.GroupsJson): the column count, the row count, the lines, and the cells.
export interface Groups {
  cols: number;
  rows: number;
  lines: GridLine[];
  cells: GridCell[];
}

// The data of setData (PageJson.DataJson): the mod texts by name, read through word. groups null turns the
// grid off.
export interface PageData {
  words: Record<string, string>;
  groups: Groups | null;
  // The Auto-replace checkbox.
  replace?: boolean;
  // The pin of the trap list.
  pin?: boolean;
}

// The data of setCard (PageJson.CardJson): the trap, the content of the game's trap card, and its texts.
export interface CardData {
  trap: number;
  name: string;
  cur: number;
  max: number;
  // The fill of the durability bar in percent, and its color band: '' green, 'medium' amber, 'low' red.
  fill: number;
  band: string;
  bait: boolean;
  baitIcon: string;
  prey: boolean;
  preyIcon: string;
  texts: { durability: string; baitSet: string; noBait: string; preyReady: string; noPrey: string };
}

// The data of setMenu (PageJson.MenuJson): the slot and one line for each trap kind.
export interface MenuData {
  slot: string;
  lines: { kind: number; icon: string; name: string; count: number }[];
}

export interface TraplineApi {
  setData(data: PageData): string;
  setCard(card: CardData): string;
  setMenu(menu: MenuData): string;
  check(): string;
}

declare global {
  interface Window {
    // The interface of the page script in the root page.
    __trapline?: TraplineApi;
    // The body observer of the page script in the CoreUI1 frame.
    __traplineObserver?: MutationObserver;
    // The pointer listeners of the trap card in the CoreUI1 frame.
    __traplinePointer?: boolean;
    // The click listener of the Set Trap menu in the CoreUI1 frame.
    __traplineMenu?: boolean;
    // The click listener of the chevron of a pinned trap list in the CoreUI1 frame.
    __traplineChevron?: boolean;
  }
}
