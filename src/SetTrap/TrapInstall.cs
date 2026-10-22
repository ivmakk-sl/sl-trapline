using System.Collections.Generic;
using System.Text;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;

namespace Trapline
{
    // The game's own Set Trap: the install event that the trap select window sends (EnumNotify_Trap.ConfirmTrapSelect,
    // 4031, handled by TrapPlanManager.OnConfirmTrapSelect), so the game checks the trap slot, the zone, and the item, and
    // queues the walk and the install as for a trap set from the game world. The game removes the item at the install.
    internal static class TrapInstall
    {
        // Sends the install of the item on the trap slot. With afterCurrent, the new action list goes right after the
        // current action (ActionManager.MoveParentListAfterCurrent): the list is the one parent id of the character that
        // is new after the event. With an idle character the new list starts at once, so there is none to move. Gives
        // whether the game queued the install (its action, Config_Item.UseAction on the item, is current or queued).
        internal static bool Send(string slot, long item, int itemConfigId, int kind, bool afterCurrent, out bool moved)
        {
            moved = false;
            var world = BaseSingleton<BattleLogicWorld>.Instance;
            long agent = world._AgentManager.GetLeadingRoleId();
            var before = afterCurrent ? ParentIds(world, agent) : null;
            EventDispatcherTools.NotifyGlobal<long, int, string>(EnumNotify_Trap.ConfirmTrapSelect, item, kind, slot);
            if (afterCurrent)
            {
                long added = 0;
                int count = 0;
                foreach (long id in ParentIds(world, agent))
                {
                    if (before.Contains(id)) continue;
                    added = id;
                    count++;
                }
                if (count == 1)
                {
                    world._ActionManager.MoveParentListAfterCurrent(agent, added);
                    moved = true;
                }
            }
            int useAction = ConfigManager.Instance.Get_Config_Item(itemConfigId)?.UseAction ?? 0;
            return useAction > 0 && HasAction(world, agent, useAction, item);
        }

        // The parent ids of the character: the current parent and the queued ones.
        private static List<long> ParentIds(BattleLogicWorld world, long agent)
        {
            var ids = new List<long>();
            var sources = world._ActionManager.AgentActionSourceDict;
            if (sources == null || !sources.TryGetValue(agent, out var source) || source == null) return ids;
            long current = world._ActionManager.GetCurrentParentId(agent);
            if (current != 0) ids.Add(current);
            var list = source.ParentIdList;
            for (int i = 0; list != null && i < list.Count; i++) ids.Add(list[i]);
            return ids;
        }

        private static bool HasAction(BattleLogicWorld world, long agent, int actionId, long target)
        {
            var sources = world._ActionManager.AgentActionSourceDict;
            if (sources == null || !sources.TryGetValue(agent, out var source) || source == null) return false;
            var cur = source.CurrentAction;
            if (cur != null && cur.ActionId == actionId && cur.TargetId == target) return true;
            var list = source.ActionList;
            for (int i = 0; list != null && i < list.Count; i++)
                if (list[i] != null && list[i].ActionId == actionId && list[i].TargetId == target) return true;
            return false;
        }

        // The current action (marked *) and the queue of the character, as action:target, for a Verbose line.
        internal static string Queue()
        {
            var world = BaseSingleton<BattleLogicWorld>.Instance;
            long agent = world._AgentManager.GetLeadingRoleId();
            var parts = new List<string>();
            var sources = world._ActionManager.AgentActionSourceDict;
            if (sources != null && sources.TryGetValue(agent, out var source) && source != null)
            {
                var cur = source.CurrentAction;
                if (cur != null) parts.Add("*" + cur.ActionId + ":" + cur.TargetId);
                var list = source.ActionList;
                for (int i = 0; list != null && i < list.Count; i++)
                    if (list[i] != null) parts.Add(list[i].ActionId + ":" + list[i].TargetId);
            }
            return "[" + string.Join(" ", parts) + "]";
        }
    }
}
