using System;
using HarmonyLib;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
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

    // A language switch changes the button word and the floor labels. ConfigManager.ApplyLanguageKey(String languageKey)
    // (SwitchLanguage(Boolean isChinese) before game 1.1.18599) returns a UniTask, so the Postfix fires once the
    // method itself returns, not once the switch finishes; the mark only sets a
    // flag, and the actual push waits for the next ActionManager.Update frame (FramePatch), which
    // gives the switch a frame to complete before TrapGroups reads the words and the floor names again.
    [HarmonyPatch(typeof(ConfigManager), "ApplyLanguageKey")]
    internal static class GridOnApplyLanguageKey
    {
        private static void Postfix()
        {
            TrapGroups.MarkDirty();
        }
    }

    // The end of a language switch. ApplyLanguageKey awaits EnsureDualLanguageLoaded and the load of a
    // community pack (ResolveCommunityPack), which can take more than the one frame above; its state
    // machine (<ApplyLanguageKey>d__300.MoveNext) calls SentryLanguageTags.Apply, its only caller, after it
    // sets LanguageType and LanguageMap on each path (Chinese, English, community pack). Marking here
    // reads the words of the new language however long the load took.
    [HarmonyPatch(typeof(SentryLanguageTags), "Apply")]
    internal static class GridOnLanguageApplied
    {
        private static void Postfix()
        {
            TrapGroups.MarkDirty();
        }
    }

    // A pickup of a storage box, a take from it in the trap window, and its Take All wear the trap: the durability mark
    // can change. Each Postfix only marks the groups dirty, so the one push of the next frame covers each wear.
    // OnPickupTrapPrey marks them in TakeAllOnPickupTrapPrey.
    [HarmonyPatch(typeof(TrapManager), "PickupCagePrey")]
    internal static class GridOnPickupCagePrey
    {
        private static void Postfix()
        {
            TrapGroups.MarkDirty();
        }
    }

    [HarmonyPatch(typeof(TrapManager), "OnTakeTrapStoreItem")]
    internal static class GridOnTakeTrapStoreItem
    {
        private static void Postfix()
        {
            TrapGroups.MarkDirty();
        }
    }

    [HarmonyPatch(typeof(TrapManager), "OnTakeAllTrapStore")]
    internal static class GridOnTakeAllTrapStore
    {
        private static void Postfix()
        {
            TrapGroups.MarkDirty();
        }
    }
}
