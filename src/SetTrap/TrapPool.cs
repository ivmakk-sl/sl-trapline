using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using Il2CppInterop.Runtime;

namespace Trapline
{
    // The trap items of the backpack and home storage, read from the bag mirror of the web UI (State_Data_Item.OwnerCache)
    // and the home scope rule (ItemManager.IsInHomeScope), as Better Crafting reads its home storage: never from
    // ItemManager.GetAllAvailableItems, which slows each later item move (AGENTS.md). The backpack comes first, then the
    // home storages in the order of the furniture list.
    internal static class TrapPool
    {
        internal static List<MenuLogic.Entry> Read()
        {
            long start = Stopwatch.GetTimestamp();
            var pool = new List<MenuLogic.Entry>();
            var cache = ReduxUISystem.Instance?.GetData<State_Data_Item>(Il2CppType.Of<State_Data_Item>())?.OwnerCache;
            var agents = BaseSingleton<BattleLogicWorld>.Instance?._AgentManager;
            var lead = agents?.GetLeadingRole();
            if (cache == null || lead == null) return pool;
            var config = ConfigManager.Instance;
            int items = Take(cache, lead.InstanceId, config, pool);
            int homeMapId = agents.GetHomeMapId();
            int homeGroup = config.Get_Config_MapPoint(homeMapId)?.HomeGroup ?? 0;
            var furnitures = agents.GetAllFurnitures();
            for (int i = 0; furnitures != null && i < furnitures.Count; i++)
            {
                var f = furnitures[i];
                if (f == null || !cache.ContainsKey(f.InstanceId)) continue;
                if (!ItemManager.IsInHomeScope(f, homeMapId, homeGroup)) continue;
                items += Take(cache, f.InstanceId, config, pool);
            }
            if (Plugin.Verbose.Value)
            {
                double ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                Plugin.Log.LogDebug("Trapline pool: " + items + " items in " + ms.ToString("F2", CultureInfo.InvariantCulture) + " ms");
            }
            return pool;
        }

        // Adds the trap stacks of one owner; gives the count of all its stacks.
        private static int Take(Il2CppSystem.Collections.Generic.Dictionary<long, Il2CppSystem.Collections.Generic.Dictionary<long, Data_Item>> cache,
                                long owner, ConfigManager config, List<MenuLogic.Entry> pool)
        {
            if (!cache.ContainsKey(owner)) return 0;
            var stacks = cache[owner];
            if (stacks == null) return 0;
            int count = 0;
            var e = stacks.Values.GetEnumerator();
            while (e.MoveNext())
            {
                var item = e.Current;
                if (item == null) continue;
                count++;
                int kind = config.Get_Config_Item(item.ItemConfigId)?.Trap ?? 0;
                if (kind > 0) pool.Add(new MenuLogic.Entry { Owner = owner, Item = item.LogicId, ItemConfigId = item.ItemConfigId, Kind = kind, Count = item.ItemCount });
            }
            return count;
        }
    }
}
