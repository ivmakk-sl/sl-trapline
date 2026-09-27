using System;
using HarmonyLib;
using GameCore.HotUpdate;
using GameCore.HotUpdate.ReduxUI;

namespace Trapline
{
    // The trap list changed: the same two reducer methods that fill the list itself. A placed trap can
    // change which floor a trap slot counts against, so the floor groups are due for a re-push;
    // TrapGroups.Tick coalesces this with every other trigger that fires in the same frame.
    [HarmonyPatch(typeof(Reducer_Web_CoreUI1), "RA_AddTrap")]
    internal static class GridOnAddTrap
    {
        private static void Postfix()
        {
            TrapGroups.MarkDirty();
        }
    }

    [HarmonyPatch(typeof(Reducer_Web_CoreUI1), "RA_RemoveTrap")]
    internal static class GridOnRemoveTrap
    {
        private static void Postfix()
        {
            TrapGroups.MarkDirty();
        }
    }

    // Captures State_Web_CoreUI1.FloorButtonsJson each time the game rebuilds the floor switcher's own
    // button list, the only source for which floor ids exist and their display order (for example ids
    // 1, 101, 102 in button order 102, 1, 101). The floor NAME is deliberately not read from this JSON - see
    // TrapGroups.FloorLabel.
    [HarmonyPatch(typeof(Reducer_Web_CoreUI1), "RebuildFloorButtons")]
    internal static class GridOnRebuildFloorButtons
    {
        private static void Postfix(State_Web_CoreUI1 state)
        {
            try { TrapGroups.CaptureFloorButtonsJson(state?.FloorButtonsJson?.Value); }
            catch (Exception e) { Plugin.Log.LogWarning($"Trapline: capture FloorButtonsJson failed: {e.Message}"); }
        }
    }

    // A slot or floor unlock refreshes the floor tags that feed the usable-slot flags, so the usable
    // slots of a floor can change even with no trap placed or removed.
    [HarmonyPatch(typeof(Reducer_Web_CoreUI1), "RA_FloorRefreshTags")]
    internal static class GridOnFloorRefreshTags
    {
        private static void Postfix()
        {
            TrapGroups.MarkDirty();
        }
    }

    // A language switch changes the button word and the floor labels. ConfigManager.SwitchLanguage(Boolean isChinese) returns a UniTask, so the
    // Postfix fires once the method itself returns, not once the switch finishes; the mark only sets a
    // flag, and the actual push waits for the next ActionManager.Update frame (FramePatch), which
    // gives the switch a frame to complete before TrapGroups reads the words and the floor names again.
    [HarmonyPatch(typeof(ConfigManager), "SwitchLanguage")]
    internal static class GridOnSwitchLanguage
    {
        private static void Postfix()
        {
            TrapGroups.MarkDirty();
        }
    }
}
