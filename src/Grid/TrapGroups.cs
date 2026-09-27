using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using UnityEngine;

namespace Trapline
{
    // Computes the floor groups and the button word, and pushes both to page.js in one call. Every trigger that can change
    // either one - a trap placed or removed, a floor-tag refresh, the floor switcher rebuilding its own
    // button list, or a language switch - only sets the dirty flag; FramePatch's per-frame Postfix
    // on ActionManager.Update drains it at most once per frame, so many triggers in the same frame (for
    // example one RA_AddTrap per trap at load) cost one push. PushSchedule decides between a push, a
    // check, and nothing, and holds the only retry.
    internal static class TrapGroups
    {
        private static readonly PushSchedule schedule = new PushSchedule();
        private static string lastFloorButtonsJson;
        private static bool dirty;
        // The counts of the last built data, for the Verbose push line.
        private static string summary = "";

        internal static void MarkDirty() => dirty = true;

        internal static void ForgetLastPush() => schedule.ForgetLastPush();

        // The last pushed data, or null before the first push.
        internal static string LastJson => schedule.LastJson;

        // The counts of one check that was ok, logged as sums once each real minute.
        internal static void OnCheckCounts(int runs, int passes)
        {
            var sum = schedule.ObserverSum(Time.realtimeSinceStartup, runs, passes);
            if (sum.HasValue && Plugin.Verbose.Value)
                Plugin.Log.LogDebug($"Trapline observer: {sum.Value.Runs} runs, {sum.Value.Passes} passes in 60 s");
        }

        internal static void CaptureFloorButtonsJson(string json)
        {
            if (!string.IsNullOrEmpty(json)) lastFloorButtonsJson = json;
            MarkDirty();
        }

        internal static void Tick()
        {
            bool wasDirty = dirty;
            dirty = false;
            PushSchedule.Step step;
            try { step = schedule.Tick(Time.realtimeSinceStartup, wasDirty, Build); }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Trapline: group/word push failed: {e.Message}");
                return;
            }
            if (step.Kind == PushSchedule.Kind.Push) PageScript.SetData(step.Json, summary);
            else if (step.Kind == PushSchedule.Kind.Check) PageScript.Check();
        }

        // The floor name in the current display language. The floor-button JSON also carries
        // a name, but the floor switcher rebuilds it only on its own triggers (RA_FloorRefreshTags and
        // similar, confirmed by NativeDisasm to be what calls RebuildFloorButtons), not on every
        // SwitchLanguage, so a name read out of a JSON captured before the switch could stay in the old
        // language until the player happens to reopen the floor switcher. Reducer_Web_CoreUI1's own
        // RebuildFloorButtons resolves each button's name from Config_MapPoint.Name (a text key) through
        // ConstantTextTools.GetLocalText; this does the same lookup with ConfigManager.GetLocalTxt directly, every push, so the label
        // always matches ConfigManager's current language with no dependency on when the JSON was last
        // rebuilt. Falls back to the floor id itself when the map point or its name key is missing.
        private static string FloorLabel(ConfigManager config, int floorId)
        {
            try
            {
                var point = config.Get_Config_MapPoint(floorId);
                if (point != null && !string.IsNullOrEmpty(point.Name))
                {
                    string text = config.GetLocalTxt(point.Name);
                    if (!string.IsNullOrEmpty(text)) return text;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Trapline: floor label lookup for {floorId} failed: {e.Message}");
            }
            return floorId.ToString();
        }

        // Reads the game into the data of setData: the button word and the floor groups.
        private static string Build()
        {
            var world = BaseSingleton<BattleLogicWorld>.Instance;
            var trapManager = world._TrapManager;
            var config = ConfigManager.Instance;
            var buildingNodes = config.customCache?.MapConfig?.BuildingNodeMap;

            // Config_TrapSlot is an IL2CPP dictionary field; iterate it with GetEnumerator()/MoveNext()/
            // Current, not foreach (the interop enumerator lacks the foreach pattern).
            var slots = new List<GridLogic.Slot>();
            var slotIt = config._Config_TrapSlot_Dict.GetEnumerator();
            while (slotIt.MoveNext())
            {
                var slot = slotIt.Current.Value;
                slots.Add(new GridLogic.Slot
                {
                    Id = slotIt.Current.Key,
                    NodeName = slot.Position,
                    Floor = config.GetTrapSlotFloorByName(slot.Position),
                    Unlocked = AreaUnlockConstants.IsSlotUnlocked(slot.Position),
                    BlockedByPreDisaster = FloorTagConstants.IsSlotBlockedByPreDisasterFloor(slot.Position),
                    InHomeMap = buildingNodes != null && buildingNodes.ContainsKey(slot.Position),
                });
            }

            var trapNodeNames = new HashSet<string>();
            var trapFloor = new Dictionary<long, int>();
            var trapNode = new Dictionary<long, string>();
            var trapIt = trapManager._placedTraps.GetEnumerator();
            while (trapIt.MoveNext())
            {
                var trap = trapIt.Current.Value;
                trapNodeNames.Add(trap.SlotNodeName);
                trapFloor[trap.InstanceId] = config.GetTrapSlotFloorByName(trap.SlotNodeName);
                trapNode[trap.InstanceId] = trap.SlotNodeName;
            }

            var floorOrder = GridLogic.ParseFloorButtonIds(lastFloorButtonsJson);
            var counts = GridLogic.LayoutFloors(slots, trapNodeNames, floorOrder);

            // The cell of each trap on its floor line, in the fixed slot order. A trap whose node
            // has no cell is left out, and page.js puts it on a line after all floors.
            var trapCell = new Dictionary<long, int>();
            foreach (var kv in trapNode)
            {
                foreach (var layout in counts)
                {
                    if (layout.Cells.TryGetValue(kv.Value, out int cell)) { trapCell[kv.Key] = cell; break; }
                }
            }

            var floors = new List<PageJson.Floor>();
            foreach (var c in counts)
                floors.Add(new PageJson.Floor { Id = c.Floor, Label = FloorLabel(config, c.Floor), Traps = c.Traps, Slots = c.Slots });
            string groupsJson = PageJson.GroupsJson(floors, trapFloor, trapCell);

            summary = $"floors={floors.Count} traps={trapFloor.Count}";
            return PageJson.DataJson(TrapWords.TakeAll(), groupsJson);
        }
    }
}
