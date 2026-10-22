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

        private static readonly HashSet<string> warned = new HashSet<string>();

        // Runs on each frame from FramePatch, so nothing here may throw into the game's ActionManager.Update.
        // A failed build keeps its change in PushSchedule, and a failed send sends again one real second
        // later. Each distinct warning logs once, because a failure can repeat each second.
        internal static void Tick()
        {
            bool wasDirty = dirty;
            dirty = false;
            try
            {
                var step = schedule.Tick(Time.realtimeSinceStartup, wasDirty, Build);
                if (step.Kind == PushSchedule.Kind.Push) PageScript.SetData(step.Json, summary);
                else if (step.Kind == PushSchedule.Kind.Check) PageScript.Check();
            }
            catch (Exception e)
            {
                schedule.ForgetLastPush();
                string message = $"Trapline: group/word push failed: {e.Message}";
                if (warned.Add(message)) Plugin.Log.LogWarning(message);
            }
        }

        // The floor name in the current display language. The floor-button JSON also carries
        // a name, but the floor switcher rebuilds it only on its own triggers (RA_FloorRefreshTags and
        // similar, confirmed by NativeDisasm to be what calls RebuildFloorButtons), not on every
        // language switch, so a name read out of a JSON captured before the switch could stay in the old
        // language until the player happens to reopen the floor switcher. Reducer_Web_CoreUI1's own
        // RebuildFloorButtons resolves each button's name from Config_MapPoint.Name (a text key) through
        // ConstantTextTools.GetLocalText; this does the same lookup with ConfigManager.GetLocalTxt directly, every push, so the label
        // always matches ConfigManager's current language with no dependency on when the JSON was last
        // rebuilt. Falls back to the floor id itself when the map point or its name key is missing.
        private static string FloorLabel(ConfigManager config, int floorId) =>
            Text(config, config.Get_Config_MapPoint(floorId)?.Name) ?? floorId.ToString();

        // The zone name (Config_MapRoom.Name) in the current display language, or the zone id.
        private static string ZoneLabel(ConfigManager config, int zoneId) =>
            Text(config, config.Get_Config_MapRoom(zoneId)?.Name) ?? zoneId.ToString();

        // The text of a text key in the display language; null for no key or no text.
        private static string Text(ConfigManager config, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            try
            {
                string text = config.GetLocalTxt(key);
                return string.IsNullOrEmpty(text) ? null : text;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Trapline: text lookup for {key} failed: {e.Message}");
                return null;
            }
        }

        // The talent key of the endless mode that lowers the wear of a pickup (TrapManager.GetCaptureDurabilityCost).
        private const string EndlessTrapCostKey = "Buff/AE_EndlessTrapCost";

        // The ratio of the endless talent of the character, only while the pure endless mode is active, as the game reads it.
        private static float EndlessRatio(BattleLogicWorld world)
        {
            if (!GameKey.IsPureEndlessActive) return 0f;
            var role = world._AgentManager?.GetLeadingRole();
            var buffs = role != null ? AgentTools.GetAgentComponent<BuffComponent>(role) : null;
            return buffs != null ? buffs.GetTalentEffectRatio(EndlessTrapCostKey) : 0f;
        }

        // Reads the game into the data of setData: the button word and the groups.
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
                    Zone = slot.Room_Id,
                    Unlocked = AreaUnlockConstants.IsSlotUnlocked(slot.Position),
                    BlockedByPreDisaster = FloorTagConstants.IsSlotBlockedByPreDisasterFloor(slot.Position),
                    InHomeMap = buildingNodes != null && buildingNodes.ContainsKey(slot.Position),
                });
            }

            // The wear of one pickup for each trap kind (TrapManager.GetCaptureDurabilityCost), and the durability mark of
            // each trap with DurabilityLogic.MarkPickups pickups left or less.
            int reduction = world._ProficiencyManager?.GetTrapCaptureCostReduction() ?? 0;
            float ratio = EndlessRatio(world);
            var trapByNode = new Dictionary<string, long>();
            var markByNode = new Dictionary<string, int>();
            var marks = new List<string>();
            var trapIt = trapManager._placedTraps.GetEnumerator();
            while (trapIt.MoveNext())
            {
                var trap = trapIt.Current.Value;
                trapByNode[trap.SlotNodeName] = trap.InstanceId;
                var trapConfig = config.Get_Config_Trap(trap.TrapConfigId);
                if (trapConfig == null) continue;
                int wear = DurabilityLogic.Wear(trapConfig.Capture_Cost, reduction, ratio);
                int? mark = DurabilityLogic.Mark(trap.CurrentDurability, trap.MaxDurability, trapConfig.Durability_Max, wear);
                if (!mark.HasValue) continue;
                markByNode[trap.SlotNodeName] = mark.Value;
                if (Plugin.Verbose.Value) marks.Add(trap.InstanceId + ":" + mark.Value + ":" + DurabilityLogic.PickupsLeft(trap.CurrentDurability, wear));
            }

            var floorOrder = GridLogic.ParseFloorButtonIds(lastFloorButtonsJson);
            var layout = GridLogic.Layout(slots, trapByNode.Keys, floorOrder);

            var lines = new List<PageJson.Line>();
            int floors = 0;
            foreach (var l in layout.Lines)
            {
                if (l.IsFloor) floors++;
                lines.Add(new PageJson.Line
                {
                    Key = l.IsFloor ? "f" + l.Floor : "z" + l.Floor + ":" + l.Zone,
                    Floor = l.IsFloor,
                    Row = l.Row,
                    Name = l.IsFloor ? FloorLabel(config, l.Floor) : ZoneLabel(config, l.Zone),
                });
            }

            // The cells in row and column order. A trap whose node has no cell is in no cell, and page.js puts it on a
            // line after all rows.
            var cells = new List<PageJson.Cell>();
            foreach (var kv in layout.Cells)
                cells.Add(new PageJson.Cell
                {
                    Slot = kv.Key,
                    Row = kv.Value.Row,
                    Column = kv.Value.Column,
                    Trap = trapByNode.TryGetValue(kv.Key, out long id) ? id : 0,
                    Mark = markByNode.TryGetValue(kv.Key, out int m) ? m : (int?)null,
                });
            cells.Sort((a, b) => a.Row != b.Row ? a.Row.CompareTo(b.Row) : a.Column.CompareTo(b.Column));
            if (Plugin.Verbose.Value)
            {
                int zones = new HashSet<(int, int)>(slots.FindAll(sl => layout.Cells.ContainsKey(sl.NodeName)).ConvertAll(sl => (sl.Floor, sl.Zone))).Count;
                Plugin.Log.LogDebug($"Trapline grid: floors {floors} zones {zones} lines {lines.Count}");
                Plugin.Log.LogDebug("Trapline grid: marks [" + string.Join(" ", marks) + "]");
            }

            string groupsJson = PageJson.GroupsJson(layout.Columns, layout.Rows, lines, cells);
            summary = $"floors={floors} traps={trapByNode.Count}";
            return PageJson.DataJson(ModTexts.Current().ToDictionary(), groupsJson, SaveKeys.GlobalOn(), Plugin.PinGrid.Value);
        }
    }
}
