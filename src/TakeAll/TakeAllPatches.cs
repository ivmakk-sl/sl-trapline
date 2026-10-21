using System;
using HarmonyLib;
using GameCore.HotUpdate.Battle.Logic;

namespace Trapline
{
    // Marks a route trap done or failed when its pickup resolves: on a full bag the game shows its own
    // notification, and the mod stops the route. The Prefix records whether the trap was part of the
    // active route before the game's own pickup logic runs; the Postfix then reads the trap's HasPrey
    // and the count of its storage box after that logic. The pickup is done only when no prey is left
    // in the trap or in the storage box (RouteLogic.PickupDone). Else the bag had no room, so the prey
    // stays in the trap, or the storage box path took only a part of the prey. A trap that is gone
    // after the pickup counts as done. A failed pickup queues the remaining route ids, and those that
    // used the storage box path, for FramePatch to remove on the next ActionManager.Update, because the
    // failed trap's own pickup action is still running here in the Postfix. A pickup the route did not
    // queue (__state false, a click by hand or a trap already resolved) changes nothing.
    [HarmonyPatch(typeof(TrapManager), "OnPickupTrapPrey")]
    internal static class TakeAllOnPickupTrapPrey
    {
        private static void Prefix(long trapInstanceId, out bool __state)
        {
            __state = TakeAllRoute.State.Contains((int)trapInstanceId);
        }

        private static void Postfix(TrapManager __instance, long trapInstanceId, bool __state)
        {
            // Each pickup wears the trap, so the durability mark of the grid can change.
            TrapGroups.MarkDirty();
            if (!__state) return;
            try
            {
                int trapId = (int)trapInstanceId;
                var trap = __instance.GetTrapById(trapInstanceId);
                bool preyInTrap = trap != null && trap.HasPrey;
                int storeCount = trap != null ? TakeAllRoute.StoreCount(trapInstanceId) : 0;
                bool done = RouteLogic.PickupDone(preyInTrap, storeCount);

                if (Plugin.Verbose.Value)
                    Plugin.Log.LogDebug($"Trapline take-all: pickup trap={trapId} prey={preyInTrap} store={storeCount} -> {(done ? "done" : "failed")}");

                if (done)
                {
                    TakeAllRoute.State.MarkDone(trapId);
                }
                else
                {
                    var remaining = TakeAllRoute.State.Fail(trapId);
                    if (remaining.Count > 0)
                        TakeAllRoute.QueuePendingRemoval(remaining, TakeAllRoute.State.StorePathIds(remaining));
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Trapline: OnPickupTrapPrey postfix failed: {e.Message}");
            }
        }
    }
}
