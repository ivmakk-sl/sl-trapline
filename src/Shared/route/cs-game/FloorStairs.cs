// Library copy of shared/route 1.0.0. Do not edit: see src/Shared/route/VERSION.
using GameCore.HotUpdate.Battle.Logic;

namespace SlShared.Route
{
    // The floor cost of a route: the stairs between two floors, as the game's own Patrol counts them
    // (FloorRouteHelper.GetRoute, the same count as PlantChoreBatchManager.FloorCost).
    internal static class FloorStairs
    {
        // 0 on the same floor or a floor id below 1, 99 when the game gives no route.
        internal static int Count(int from, int to)
        {
            if (from == to || from <= 0 || to <= 0) return 0;
            var route = FloorRouteHelper.GetRoute(from, to);
            return route != null && route.Count > 0 ? route.Count : 99;
        }
    }
}
