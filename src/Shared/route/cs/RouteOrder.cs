// Library copy of shared/route 1.0.0. Do not edit: see src/Shared/route/VERSION.
using System;
using System.Collections.Generic;

namespace SlShared.Route
{
    // The floor and the place of the character when the walk starts.
    internal struct RouteStart
    {
        public int Floor;
        public float X, Y, Z;
    }

    // One stop of the walk: its floor and its place. The mod keeps its own data of the stop by the index of the stop
    // in its list.
    internal struct RouteStop
    {
        public int Floor;
        public float X, Y, Z;
    }

    // The order of the stops of a walk of the character over the floors of the home. The floor of the character
    // comes first, then each time the floor that the stairs reach first from the floor before (a tie goes to the
    // lower floor id). On a floor, the walk is the shortest order through all its stops up to ExactLimit stops; with
    // more, the nearest stop comes next, and 2-opt passes then reverse a part of the walk while that shortens it. The
    // distance is the walk length that `walk` gives (in the game the path of the navigation mesh, which goes around
    // walls), else the straight one on the floor plane (x, z). On an equal walk, the stop that comes first in the list
    // comes first. No game or BepInEx type here.
    internal static class RouteOrder
    {
        // The most stops of one floor for the exact shortest walk: its table has 2^n * n entries, about 50000 for 12
        // stops.
        public const int ExactLimit = 12;

        // Two walks closer than this are equal, so the sum order of the lengths does not decide a tie.
        private const double Tie = 1e-9;

        // The indexes of the stops in route order. `floorCost` gives the stairs between two floors; `stairs` gives the
        // place where the walk enters a floor, or null when the game gives none: the walk then starts from the place of
        // the last stop of the floor before. `walk` gives the walk length between two places on one floor, or null for
        // the straight distance.
        public static List<int> Order(RouteStart start, IList<RouteStop> stops, Func<int, int, int> floorCost,
            Func<int, (float X, float Z)?> stairs = null,
            Func<(float X, float Y, float Z), (float X, float Y, float Z), double> walk = null)
        {
            walk ??= Straight;
            var byFloor = new Dictionary<int, List<int>>();
            for (int i = 0; i < stops.Count; i++)
            {
                if (!byFloor.TryGetValue(stops[i].Floor, out var list)) byFloor[stops[i].Floor] = list = new List<int>();
                list.Add(i);
            }

            var route = new List<int>();
            int floor = start.Floor;
            var place = (start.X, start.Y, start.Z);
            bool first = true;
            while (byFloor.Count > 0)
            {
                int next = NextFloor(floor, byFloor.Keys, floorCost);
                if (!first || next != start.Floor)
                {
                    var entry = stairs?.Invoke(next);
                    if (entry.HasValue) place = (entry.Value.X, place.Item2, entry.Value.Z);
                }
                first = false;
                var path = Walk(place, byFloor[next], stops, walk);
                byFloor.Remove(next);
                route.AddRange(path);
                floor = next;
                place = Place(stops[path[path.Count - 1]]);
            }
            return route;
        }

        private static int NextFloor(int from, IEnumerable<int> floors, Func<int, int, int> floorCost)
        {
            int best = 0, bestCost = int.MaxValue;
            bool any = false;
            foreach (int floor in floors)
            {
                int cost = floor == from ? -1 : floorCost(from, floor);
                if (!any || cost < bestCost || (cost == bestCost && floor < best)) (best, bestCost, any) = (floor, cost, true);
            }
            return best;
        }

        // The walk on one floor from the entry place: the shortest order through all its stops (Held-Karp on the
        // open path) up to ExactLimit stops, else nearest neighbor, then 2-opt. `ids` are the indexes of the stops of
        // the floor, in list order.
        private static List<int> Walk((float X, float Y, float Z) entry, List<int> ids, IList<RouteStop> stops,
            Func<(float X, float Y, float Z), (float X, float Y, float Z), double> walk)
        {
            var places = ids.ConvertAll(i => Place(stops[i]));
            var order = places.Count <= ExactLimit ? Shortest(entry, places, walk) : NearestThen2Opt(entry, places, walk);
            return order.ConvertAll(k => ids[k]);
        }

        // rest[set, at]: the shortest walk that starts at stop `at` and goes through each other stop of `set` (a bit
        // for each stop, `at` in it). The table runs from the end of the walk, so the choice of the first stop and of
        // each next stop can take the earliest stop of the list on a tie.
        private static List<int> Shortest((float X, float Y, float Z) entry, List<(float X, float Y, float Z)> places,
            Func<(float X, float Y, float Z), (float X, float Y, float Z), double> walk)
        {
            int n = places.Count;
            if (n == 0) return new List<int>();
            var d = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    d[i, j] = i == j ? 0 : walk(places[i], places[j]);
            int full = (1 << n) - 1;
            var rest = new double[1 << n, n];
            var next = new int[1 << n, n];
            for (int set = 1; set <= full; set++)
                for (int at = 0; at < n; at++)
                {
                    if ((set & (1 << at)) == 0) continue;
                    int others = set & ~(1 << at);
                    next[set, at] = -1;
                    if (others == 0) continue;
                    double best = double.MaxValue;
                    for (int j = 0; j < n; j++)
                    {
                        if ((others & (1 << j)) == 0) continue;
                        double length = d[at, j] + rest[others, j];
                        // The first stop always counts, so a walk length of no number (infinite, NaN) drops no stop.
                        if (next[set, at] < 0 || length < best - Tie) (best, next[set, at]) = (length, j);
                    }
                    rest[set, at] = best;
                }
            int start = -1;
            double shortest = double.MaxValue;
            for (int at = 0; at < n; at++)
            {
                double length = walk(entry, places[at]) + rest[full, at];
                if (start < 0 || length < shortest - Tie) (shortest, start) = (length, at);
            }
            var path = new List<int>();
            for (int set = full, at = start; at >= 0;)
            {
                path.Add(at);
                int j = next[set, at];
                set &= ~(1 << at);
                at = j;
            }
            return path;
        }

        private static List<int> NearestThen2Opt((float X, float Y, float Z) entry, List<(float X, float Y, float Z)> places,
            Func<(float X, float Y, float Z), (float X, float Y, float Z), double> walk)
        {
            var left = new List<int>();
            for (int i = 0; i < places.Count; i++) left.Add(i);
            var path = new List<int>();
            var here = entry;
            while (left.Count > 0)
            {
                int nearest = 0;
                double best = walk(here, places[left[0]]);
                for (int i = 1; i < left.Count; i++)
                {
                    double d = walk(here, places[left[i]]);
                    if (d < best) (nearest, best) = (i, d);
                }
                int stop = left[nearest];
                left.RemoveAt(nearest);
                path.Add(stop);
                here = places[stop];
            }

            // A home has a few dozen stops at most, so the passes cost nothing.
            bool shorter = true;
            while (shorter)
            {
                shorter = false;
                for (int i = 0; i < path.Count; i++)
                    for (int k = i + 1; k < path.Count; k++)
                    {
                        var before = i == 0 ? entry : places[path[i - 1]];
                        double delta = walk(before, places[path[k]]) - walk(before, places[path[i]]);
                        if (k + 1 < path.Count) delta += walk(places[path[i]], places[path[k + 1]]) - walk(places[path[k]], places[path[k + 1]]);
                        if (delta >= -1e-4) continue;
                        path.Reverse(i, k - i + 1);
                        shorter = true;
                    }
            }
            return path;
        }

        private static (float X, float Y, float Z) Place(RouteStop stop) => (stop.X, stop.Y, stop.Z);

        private static double Straight((float X, float Y, float Z) a, (float X, float Y, float Z) b) =>
            Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
    }
}
