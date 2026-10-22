using System;

namespace Trapline
{
    // Game-free logic. This file must not use any game or BepInEx type, because the unit tests compile it alone.
    // The trap card of a trap cell: what the game's trap card in the world (TrapUI/TrapFloatPopup.html) shows.
    public static class CardLogic
    {
        // The game's texts of the card, in the display language.
        public sealed class Texts
        {
            public string Durability;
            public string BaitSet;
            public string NoBait;
            public string PreyReady;
            public string NoPrey;
        }

        // The content of the card: the trap name, the durability with the fill of the bar (a percent) and its color band
        // ("" green, "medium" amber, "low" red, as the game's durabilityClass), and the bait and the prey with their icons.
        public sealed class Card
        {
            public string Name;
            public int Current;
            public int Max;
            public float Fill;
            public string Band;
            public bool HasBait;
            public string BaitIcon;
            public bool HasPrey;
            public string PreyIcon;
        }

        // The game's bands of the durability bar (TrapFloatPopup.html): amber at a ratio of 0.5 or less, red at 0.2 or less.
        private const double MediumRatio = 0.5;
        private const double LowRatio = 0.2;

        // The card from the values that TrapManager.OnRequestTrapHoverData reads: the current and the most durability
        // (Config_Trap.Durability_Max below 1), the bait item (the bait or the first bait in stock, 0 for none) and the
        // prey item (the prey in the trap or the first item of the storage box, 0 for none), with their WebIcon.
        public static Card Build(string name, int current, int max, int configMax, int baitItemId, string baitIcon, int preyItemId, string preyIcon)
        {
            int most = max >= 1 ? max : configMax;
            double ratio = most >= 1 ? Math.Max(0.0, Math.Min(1.0, (double)current / most)) : 0.0;
            return new Card
            {
                Name = name ?? "",
                Current = current,
                Max = most,
                Fill = (float)(ratio * 100),
                Band = ratio <= LowRatio ? "low" : ratio <= MediumRatio ? "medium" : "",
                HasBait = baitItemId > 0,
                BaitIcon = baitIcon ?? "",
                HasPrey = preyItemId > 0,
                PreyIcon = preyIcon ?? "",
            };
        }
    }
}
