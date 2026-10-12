// Library copy of shared/route 1.0.0. Do not edit: see src/Shared/route/VERSION.
using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using UnityEngine;
using UnityEngine.AI;

namespace SlShared.Route
{
    // The walk length between two places on one floor for a route: the length of the path of the navigation mesh, on
    // the walkable areas of the game (GameKey.WalkableAreaMask, as AE_IsInRange measures a walk), so a wall between two
    // near places makes the walk long. Each place is first moved onto the mesh within the snap radius (a pot or a trap
    // stands on no walkable area). When the mesh gives no full path, the length is the straight one. One instance lives
    // for one route and keeps each length it found. A mod needs a reference to UnityEngine.AIModule.
    internal sealed class WalkLength
    {
        private readonly float snapRadius;
        private readonly Action<string> warn;
        private readonly int mask = GameKey.WalkableAreaMask;
        private readonly NavMeshPath path = new NavMeshPath();
        private readonly Dictionary<(float, float, float, float, float, float), double> lengths = new Dictionary<(float, float, float, float, float, float), double>();

        // `warn` gets the text of a failed mesh call (the mod writes it to its log once); `snapRadius` is how far a
        // place can be from the mesh: a pot is an obstacle, so the walkable floor is next to it.
        internal WalkLength(Action<string> warn, float snapRadius = 2f)
        {
            this.warn = warn;
            this.snapRadius = snapRadius;
        }

        internal int Paths { get; private set; }
        internal int AllAreaPaths { get; private set; }
        internal int PartialPaths { get; private set; }
        internal int Fallbacks { get; private set; }

        internal double Of((float X, float Y, float Z) a, (float X, float Y, float Z) b)
        {
            var key = a.X < b.X || (a.X == b.X && a.Z <= b.Z) ? (a.X, a.Y, a.Z, b.X, b.Y, b.Z) : (b.X, b.Y, b.Z, a.X, a.Y, a.Z);
            if (lengths.TryGetValue(key, out double known)) return known;
            double length = Measure(new Vector3(a.X, a.Y, a.Z), new Vector3(b.X, b.Y, b.Z));
            lengths[key] = length;
            return length;
        }

        // The length of the mesh path: the walkable areas first, then all areas (a partial path on the walkable
        // areas can mean an area that the walk still crosses), then a partial path with the straight rest from its
        // end; the straight distance only when no place is near the mesh.
        private double Measure(Vector3 a, Vector3 b)
        {
            double straight = Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z));
            try
            {
                if (NavMesh.SamplePosition(a, out var hitA, snapRadius, mask) && NavMesh.SamplePosition(b, out var hitB, snapRadius, mask))
                {
                    if (Complete(hitA.position, hitB.position, mask)) { Paths++; return Length(hitB.position, false); }
                    // The all-areas query below reuses `path`, so the partial walk of the walkable areas is kept first.
                    double partial = path.status == NavMeshPathStatus.PathPartial ? Length(hitB.position, true) : -1;
                    if (Complete(hitA.position, hitB.position, NavMesh.AllAreas)) { AllAreaPaths++; return Length(hitB.position, false); }
                    if (path.status == NavMeshPathStatus.PathPartial) { PartialPaths++; return Length(hitB.position, true); }
                    if (partial >= 0) { PartialPaths++; return partial; }
                }
            }
            catch (Exception e) { warn?.Invoke($"a walk length failed, the route uses the straight distance: {e.Message}"); }
            Fallbacks++;
            return straight;
        }

        private bool Complete(Vector3 a, Vector3 b, int areas) =>
            NavMesh.CalculatePath(a, b, areas, path) && path.status == NavMeshPathStatus.PathComplete;

        // The length of the last path, with the straight rest from its end to the target for a partial path.
        private double Length(Vector3 target, bool rest)
        {
            var corners = path.corners;
            double length = 0;
            for (int i = 1; corners != null && i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            if (rest && corners != null && corners.Length > 0) length += Vector3.Distance(corners[corners.Length - 1], target);
            return length;
        }
    }
}
