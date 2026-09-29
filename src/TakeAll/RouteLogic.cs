using System.Collections.Generic;

namespace Trapline
{
    // Game-free logic. This file must not use any game or BepInEx type, because the unit tests compile it alone.
    // Builds the Take All route: the order in which the character visits traps that have prey.
    public static class RouteLogic
    {
        // One trap with prey, read from the game. Floor and position come from the caller.
        public sealed class Trap
        {
            public int Id;
            public int Floor;
            public float X;
            public float Z;
        }

        // A trap goes into the route when prey is in the trap, or when the storage box of an Auto Trap
        // Cage holds prey (storeCount is the count of items in the storage box).
        public static bool HasPrey(bool preyInTrap, int storeCount) => preyInTrap || storeCount > 0;

        // The route sends a trap through the game's pickup of the storage box (the pickup of the
        // game's "Collect All" button) when its storage box holds prey, else through the plain pickup.
        public static bool UseStorePath(int storeCount) => storeCount > 0;

        // A pickup of the route is done only when no prey is left in the trap or in its storage box.
        // A part take (the bag became full) leaves prey behind and is a failed pickup.
        public static bool PickupDone(bool preyInTrapAfter, int storeCountAfter) => !HasPrey(preyInTrapAfter, storeCountAfter);

        // Orders the traps floor by floor, nearest neighbour within a floor. floorCosts is the cost
        // from the character floor to each other floor.
        public static List<int> BuildRoute(
            IReadOnlyList<Trap> traps,
            int characterFloor,
            float characterX,
            float characterZ,
            IReadOnlyDictionary<int, float> floorCosts)
        {
            var route = new List<int>();
            float x = characterX, z = characterZ;

            foreach (var floorGroup in OrderFloors(traps, characterFloor, floorCosts))
            {
                var remaining = new List<Trap>(floorGroup);
                while (remaining.Count > 0)
                {
                    var nearest = Nearest(remaining, x, z);
                    route.Add(nearest.Id);
                    x = nearest.X;
                    z = nearest.Z;
                    remaining.Remove(nearest);
                }
            }

            return route;
        }

        // Groups the traps by floor, then orders the floors: the character floor first when it has
        // traps, then by floor cost from the character floor (a floor missing from the table sorts
        // after the floors that have a cost), then by floor id on equal cost.
        private static IEnumerable<List<Trap>> OrderFloors(
            IReadOnlyList<Trap> traps,
            int characterFloor,
            IReadOnlyDictionary<int, float> floorCosts)
        {
            var byFloor = new Dictionary<int, List<Trap>>();
            foreach (var trap in traps)
            {
                if (!byFloor.TryGetValue(trap.Floor, out var list))
                {
                    list = new List<Trap>();
                    byFloor[trap.Floor] = list;
                }
                list.Add(trap);
            }

            var floors = new List<int>(byFloor.Keys);
            floors.Remove(characterFloor);
            floors.Sort((a, b) =>
            {
                bool hasA = floorCosts.TryGetValue(a, out var costA);
                bool hasB = floorCosts.TryGetValue(b, out var costB);
                if (hasA != hasB) return hasA ? -1 : 1;
                if (hasA && costA != costB) return costA.CompareTo(costB);
                return a.CompareTo(b);
            });
            if (byFloor.ContainsKey(characterFloor)) floors.Insert(0, characterFloor);

            foreach (var floor in floors) yield return byFloor[floor];
        }

        private static Trap Nearest(List<Trap> traps, float x, float z)
        {
            Trap nearest = null;
            float bestDistSq = 0;
            foreach (var trap in traps)
            {
                float dx = trap.X - x, dz = trap.Z - z;
                float distSq = dx * dx + dz * dz;
                if (nearest == null || distSq < bestDistSq || (distSq == bestDistSq && trap.Id < nearest.Id))
                {
                    nearest = trap;
                    bestDistSq = distSq;
                }
            }
            return nearest;
        }
    }

    // The in-memory state of one Take All route, for the full-bag stop.
    // Holds the trap ids that were queued and are not done yet, in order, and the ids that the route
    // sent through the storage box path.
    public sealed class RouteState
    {
        private readonly List<int> _remaining = new List<int>();
        private readonly HashSet<int> _storePath = new HashSet<int>();

        public bool IsActive { get; private set; }

        public void Start(IEnumerable<int> orderedIds)
        {
            _remaining.Clear();
            _remaining.AddRange(orderedIds);
            _storePath.Clear();
            IsActive = true;
        }

        // Records that the route sent this trap through the storage box path. Each such pickup leaves
        // a take request in the game's queue of that trap, which the full-bag stop must take out again.
        public void MarkStorePath(int trapId) => _storePath.Add(trapId);

        // The ids of the given list that the current route sent through the storage box path.
        public IReadOnlyList<int> StorePathIds(IEnumerable<int> ids)
        {
            var result = new List<int>();
            foreach (int id in ids)
                if (_storePath.Contains(id)) result.Add(id);
            return result;
        }

        public bool Contains(int trapId) => IsActive && _remaining.Contains(trapId);

        public void MarkDone(int trapId)
        {
            if (!Contains(trapId)) return;
            _remaining.Remove(trapId);
            if (_remaining.Count == 0) IsActive = false;
        }

        // Ends the route and returns the ids that are not done yet, not including trapId itself (its
        // pickup already ran, even though it failed). A trap that is not in the route changes nothing.
        public IReadOnlyList<int> Fail(int trapId)
        {
            if (!Contains(trapId)) return new List<int>();
            _remaining.Remove(trapId);
            var remaining = new List<int>(_remaining);
            _remaining.Clear();
            IsActive = false;
            return remaining;
        }
    }
}
