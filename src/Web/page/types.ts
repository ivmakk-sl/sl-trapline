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

// One floor line of the trap grid (PageJson.GroupsJson in C#).
export interface Floor {
  id: number;
  label: string;
  traps: number;
  slots: number;
}

// The floor groups (PageJson.GroupsJson): the floors in display order, the floor of each trap, and the
// cell of each trap on its floor line, both by trap instance id.
export interface Groups {
  floors: Floor[];
  trapFloor: Record<string, number>;
  trapCell: Record<string, number>;
}

// The data of setData (PageJson.DataJson). groups null turns the grid off.
export interface PageData {
  words: { takeAll?: string };
  groups: Groups | null;
}

export interface TraplineApi {
  setData(data: PageData): string;
  check(): string;
}

declare global {
  interface Window {
    // The interface of the page script in the root page.
    __trapline?: TraplineApi;
    // The body observer of the page script in the CoreUI1 frame.
    __traplineObserver?: MutationObserver;
  }
}
