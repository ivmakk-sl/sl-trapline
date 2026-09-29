using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using UnityEngine;

namespace Trapline
{
    // Builds and starts one Take All route: the traps with prey (in the trap or in the storage box of an
    // Auto Trap Cage), ordered by the game-free RouteLogic, dispatched through the game's own pickup
    // action. A trap whose storage box holds prey goes through TrapManager.OnStartTakeStoredPrey(id, -1),
    // the pickup of the game's "Collect All" button: it takes the prey of the storage box and the prey in
    // the cage. Any other trap goes through TrapManager.OnStartPickupTrapPrey. Both queue the pickup
    // action (TrapManager.PickupPreyActionId) with the trap as the target.
    internal static class TakeAllRoute
    {
        // The full-bag stop (TakeAllOnPickupTrapPrey) reads and updates this on a failed pickup.
        internal static readonly RouteState State = new RouteState();

        // The remaining route trap ids to remove from the action queue after a full-bag stop, set by
        // TakeAllOnPickupTrapPrey's Postfix and consumed by FramePatch on the next ActionManager.Update.
        // Null when nothing is pending. The failed trap's own pickup is still running inside
        // OnPickupTrapPrey when the Postfix fires, so the removal itself must wait a frame.
        private static List<int> pendingRemovalIds;

        // The ids of pendingRemovalIds that the route sent through the storage box path.
        private static List<int> pendingStorePathIds;

        internal static void QueuePendingRemoval(IReadOnlyList<int> remainingIds, IReadOnlyList<int> storePathIds)
        {
            pendingRemovalIds = new List<int>(remainingIds);
            pendingStorePathIds = new List<int>(storePathIds);
        }

        // The count of items in the storage box of a trap. A trap with no storage box has none.
        internal static int StoreCount(long trapInstanceId)
        {
            var items = BaseSingleton<BattleLogicWorld>.Instance._ItemManager.GetTrapStoreItems(trapInstanceId);
            return items?.Count ?? 0;
        }

        // Removes, for each remaining route trap, the whole parent list that holds its queued pickup
        // (action id TrapManager.PickupPreyActionId, target id the trap id): one OnStartPickupTrapPrey
        // call queues one parent list holding both the walk and the pickup, so RemoveParentList also
        // stops the walk toward the next trap. Never removes a parent list whose pickup targets a trap
        // outside the given remaining set, so an action the player queued by hand is untouched.
        // OnStartTakeStoredPrey also put a take request in the game's queue of the trap
        // (_pendingPreyTakeRequests), and only the pickup takes it out again. So for each removed pickup
        // of the storage box path, the removal takes one request out with TryDequeuePreyTakeRequest.
        // Else the next pickup of that Auto Trap Cage would take the left request, also when the player
        // asks the game to open the storage box. The rule of no second pickup in the queue (Start) makes
        // the request of the route the only one of that trap.
        internal static void RunPendingRemoval()
        {
            var ids = pendingRemovalIds;
            var storePathIds = pendingStorePathIds;
            if (ids == null) return;
            pendingRemovalIds = null;
            pendingStorePathIds = null;

            try
            {
                var world = BaseSingleton<BattleLogicWorld>.Instance;
                var actionManager = world._ActionManager;
                long agentId = world._AgentManager.GetLeadingRoleId();

                var source = actionManager.GetActionSource(agentId);
                var actionList = source?.ActionList;
                if (actionList == null) return;

                var parentIds = new List<long>();
                var removedTrapIds = new List<int>();
                for (int i = 0; i < actionList.Count; i++)
                {
                    var action = actionList[i];
                    if (action.ActionId != TrapManager.PickupPreyActionId) continue;
                    int trapId = (int)action.TargetId;
                    if (!ids.Contains(trapId)) continue;
                    if (!parentIds.Contains(action.ParentId)) parentIds.Add(action.ParentId);
                    if (!removedTrapIds.Contains(trapId)) removedTrapIds.Add(trapId);
                }

                foreach (long parentId in parentIds)
                    actionManager.RemoveParentList(agentId, parentId, false);

                if (Plugin.Verbose.Value)
                    Plugin.Log.LogDebug($"Trapline take-all: full-bag stop removed {parentIds.Count} queued pickup(s)");

                int dequeued = 0;
                foreach (int trapId in removedTrapIds)
                {
                    if (!storePathIds.Contains(trapId)) continue;
                    if (world._TrapManager.TryDequeuePreyTakeRequest(trapId, out _)) dequeued++;
                }

                if (dequeued > 0 && Plugin.Verbose.Value)
                    Plugin.Log.LogDebug($"Trapline take-all: full-bag stop dequeued {dequeued} storage box request(s)");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Trapline: full-bag pickup removal failed: {e.Message}");
            }
        }

        public static void Start()
        {
            var world = BaseSingleton<BattleLogicWorld>.Instance;
            var trapManager = world._TrapManager;
            var actionManager = world._ActionManager;
            var agentManager = world._AgentManager;
            var config = ConfigManager.Instance;

            long agentId = agentManager.GetLeadingRoleId();
            int characterFloor = agentManager.GetAgentFloor(agentId);
            var leadingRole = agentManager.GetLeadingRole();
            Vector3 characterPos = leadingRole?._transformComponent != null
                ? leadingRole._transformComponent.Position
                : default;

            var buildingNodes = config.customCache?.MapConfig?.BuildingNodeMap;

            // _placedTraps is an IL2CPP dictionary field; iterate it with GetEnumerator()/MoveNext()/
            // Current, not foreach (the interop enumerator lacks the foreach pattern).
            var traps = new List<RouteLogic.Trap>();
            var storeCounts = new Dictionary<int, int>();
            var it = trapManager._placedTraps.GetEnumerator();
            while (it.MoveNext())
            {
                var trap = it.Current.Value;
                int storeCount = StoreCount(trap.InstanceId);
                if (!RouteLogic.HasPrey(trap.HasPrey, storeCount)) continue;
                if (actionManager.HasActionInQueue(agentId, TrapManager.PickupPreyActionId, trap.InstanceId)) continue;

                int floor = config.GetTrapSlotFloorByName(trap.SlotNodeName);
                // A slot with no node in BuildingNodeMap still gets a route entry, at the character's own
                // position, so it sorts early on its floor rather than being skipped.
                Vector3 pos = characterPos;
                if (buildingNodes != null) buildingNodes.TryGetValue(trap.SlotNodeName, out pos);

                traps.Add(new RouteLogic.Trap { Id = (int)trap.InstanceId, Floor = floor, X = pos.x, Z = pos.z });
                storeCounts[(int)trap.InstanceId] = storeCount;
            }

            if (traps.Count == 0)
            {
                if (Plugin.Verbose.Value) Plugin.Log.LogDebug("Trapline take-all: no trap with prey to queue");
                return;
            }

            var floorCosts = new Dictionary<int, float>();
            foreach (var trap in traps)
            {
                if (trap.Floor == characterFloor || floorCosts.ContainsKey(trap.Floor)) continue;
                floorCosts[trap.Floor] = PlantChoreBatchManager.FloorCost(characterFloor, trap.Floor);
            }

            var route = RouteLogic.BuildRoute(traps, characterFloor, characterPos.x, characterPos.z, floorCosts);
            State.Start(route);

            if (Plugin.Verbose.Value)
                Plugin.Log.LogDebug($"Trapline take-all: characterFloor={characterFloor} route=[{string.Join(",", route)}]");

            foreach (int trapId in route)
            {
                int storeCount = storeCounts[trapId];
                bool storePath = RouteLogic.UseStorePath(storeCount);
                if (Plugin.Verbose.Value)
                    Plugin.Log.LogDebug($"Trapline take-all: trap={trapId} path={(storePath ? "store" : "trap")} store={storeCount}");

                if (storePath)
                {
                    State.MarkStorePath(trapId);
                    trapManager.OnStartTakeStoredPrey(trapId, -1);
                }
                else
                {
                    trapManager.OnStartPickupTrapPrey(trapId);
                }
            }
        }
    }
}
