using System;
using System.Collections.Generic;
using SlShared.Route;

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
            public float Y;
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

        // The trap ids in the order of the Take All route, from the route library (shared/route): the floor of the
        // character first, then each next floor by `floorCost` from the floor before, and the shortest walk on each
        // floor by `walk` (null: the straight distance). The traps go in by id, so an equal walk goes to the lower trap
        // id. Internal, as the library types are.
        internal static List<int> Order(IList<Trap> traps, int characterFloor, float characterX, float characterY, float characterZ,
            Func<int, int, int> floorCost,
            Func<(float X, float Y, float Z), (float X, float Y, float Z), double> walk = null)
        {
            var byId = new List<Trap>(traps);
            byId.Sort((a, b) => a.Id.CompareTo(b.Id));
            var stops = new List<RouteStop>(byId.Count);
            foreach (var trap in byId) stops.Add(new RouteStop { Floor = trap.Floor, X = trap.X, Y = trap.Y, Z = trap.Z });
            var start = new RouteStart { Floor = characterFloor, X = characterX, Y = characterY, Z = characterZ };
            var route = new List<int>(byId.Count);
            foreach (int i in RouteOrder.Order(start, stops, floorCost, null, walk)) route.Add(byId[i].Id);
            return route;
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
