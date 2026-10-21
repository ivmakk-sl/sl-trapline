using System;
using System.Collections.Generic;
using HarmonyLib;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;

namespace Trapline
{
    // Auto-replace: a trap that breaks gets a new trap of the same kind on its trap slot, with the game's own Set Trap
    // right after the current action. The break is the removal of a trap with no durability left (TrapManager.RemoveTrap):
    // OnTrapBroken of a pickup and the inline break of a take from the storage box both call it, and a dismantle or a move
    // removes a trap with durability left. The replace runs on the next frame (FramePatch), when the pickup that broke the
    // trap is the current action, so the install goes between that pickup and the next one of a Take All route (seen in
    // the game 1.1.19131 with a route of three breaks).
    internal static class Replace
    {
        private static readonly List<(string slot, int kind)> pending = new List<(string, int)>();
        private static readonly HashSet<string> refused = new HashSet<string>();

        // True between the Prefix and the Postfix of TrapManager.LoadGame: a load breaks a saved trap with no durability
        // left, and that break gets no replace.
        internal static bool Loading;

        internal static void OnRemoveTrap(TrapManager manager, long trapId)
        {
            var trap = manager.GetTrapById(trapId);
            if (trap == null || trap.CurrentDurability >= 1) return;
            if (Loading)
            {
                if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Trapline replace: slot {trap.SlotNodeName} kind {trap.TrapConfigId} skipped (save load)");
                return;
            }
            pending.Add((trap.SlotNodeName, trap.TrapConfigId));
        }

        // FramePatch, each frame: a flag check, and the replace on the frame after a break.
        internal static void Tick()
        {
            // A load runs within one frame; a LoadGame that throws skips its Postfix, and this keeps the flag from staying set.
            Loading = false;
            if (pending.Count == 0) return;
            var breaks = pending.ToArray();
            pending.Clear();
            foreach (var (slot, kind) in breaks)
            {
                try { Run(slot, kind); }
                catch (Exception e) { Plugin.WarnOnce($"Trapline: Auto-replace failed: {e.Message}"); }
            }
        }

        private static void Run(string slot, int kind)
        {
            bool globalOn = SaveKeys.GlobalOn();
            bool slotOn = SaveKeys.SlotOn(slot);
            var pool = globalOn && slotOn ? TrapPool.Read() : new List<MenuLogic.Entry>();
            var decision = ReplaceLogic.Decide(false, globalOn, slotOn, kind, pool);
            if (decision.Pick == null)
            {
                if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Trapline replace: slot {slot} kind {kind} skipped ({decision.Skip})");
                return;
            }
            var pick = decision.Pick;
            bool queued = TrapInstall.Send(slot, pick.Item, pick.ItemConfigId, kind, true, out bool moved);
            if (!queued)
            {
                // The game refused the install (a waiting install, a wrong zone): the trap slot stays free.
                string message = $"Trapline replace: slot {slot} kind {kind} -> item {pick.Item} refused by the game";
                if (Plugin.Verbose.Value && refused.Add(message)) Plugin.Log.LogDebug(message);
                return;
            }
            if (Plugin.Verbose.Value)
            {
                Plugin.Log.LogDebug($"Trapline replace: slot {slot} kind {kind} -> item {pick.Item} owner {pick.Owner} queued after current" + (moved ? "" : " (no move: the install is current)"));
                Plugin.Log.LogDebug("Trapline replace: queue " + TrapInstall.Queue());
            }
        }
    }

    [HarmonyPatch(typeof(TrapManager), "RemoveTrap")]
    internal static class ReplaceOnRemoveTrap
    {
        private static void Prefix(TrapManager __instance, long trapInstanceId)
        {
            try { Replace.OnRemoveTrap(__instance, trapInstanceId); }
            catch (Exception e) { Plugin.WarnOnce($"Trapline: Auto-replace break check failed: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(TrapManager), "LoadGame")]
    internal static class ReplaceOnLoadGame
    {
        private static void Prefix() => Replace.Loading = true;

        private static void Postfix() => Replace.Loading = false;
    }
}
