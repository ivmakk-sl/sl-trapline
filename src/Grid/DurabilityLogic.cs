using System;

namespace Trapline
{
    // Game-free logic. This file must not use any game or BepInEx type, because the unit tests compile it alone.
    // The durability mark of a trap cell (the red danger cell): the pickups left of a trap and its durability percent.
    public static class DurabilityLogic
    {
        // A trap with this many pickups left or less shows the durability mark: the next pickup breaks it.
        public const int MarkPickups = 1;

        // The most ratio of the endless talent Buff/AE_EndlessTrapCost (TrapManager.GetCaptureDurabilityCost).
        private const float MaxRatio = 0.9f;

        // The durability that one pickup takes, as TrapManager.GetCaptureDurabilityCost computes it: Config_Trap.Capture_Cost
        // less the capture_cost_reduction of the trapping level, times one less the endless talent ratio (from 0 to 0.9),
        // rounded half to even, at least 0.
        public static int Wear(int captureCost, int reduction, float ratio)
        {
            if (ratio < 0f) ratio = 0f;
            else if (ratio > MaxRatio) ratio = MaxRatio;
            int wear = (int)Math.Round((captureCost - reduction) * (1f - ratio), MidpointRounding.ToEven);
            return wear < 0 ? 0 : wear;
        }

        // The pickups left: the current durability over the wear, rounded up. A wear of 0 or less counts as 1.
        public static int PickupsLeft(int current, int wear)
        {
            if (current <= 0) return 0;
            if (wear < 1) wear = 1;
            return (current + wear - 1) / wear;
        }

        // The most durability as the game reads it: PlacedTrap.MaxDurability, or Config_Trap.Durability_Max below 1.
        public static int MaxDurability(int max, int configMax) => max >= 1 ? max : configMax;

        // The durability percent, rounded down.
        public static int MarkPercent(int current, int max)
        {
            if (max < 1 || current <= 0) return 0;
            return (int)((long)current * 100 / max);
        }

        // The percent of the durability mark, or null for a trap with more than MarkPickups pickups left, or for a trap
        // that a pickup does not wear (a wear of 0 never breaks it).
        public static int? Mark(int current, int max, int configMax, int wear)
        {
            if (wear < 1 || PickupsLeft(current, wear) > MarkPickups) return null;
            return MarkPercent(current, MaxDurability(max, configMax));
        }
    }
}
