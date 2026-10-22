using System;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;

namespace Trapline
{
    // The trap card of a trap cell: the page asks for it (TRAPLINE_CARD) when the pointer comes onto a trap cell, and this
    // sends it in one call. It reads the trap as TrapManager.OnRequestTrapHoverData does for the game's trap card in the
    // world, with the texts of that card (WebUI_TrapHoverPanel.InitLocalText).
    internal static class TrapCard
    {
        // The text keys of the game's trap card (ConstantText.tsv).
        private const string DurabilityKey = "UI_BattleShop_20";
        private const string BaitSetKey = "WebUI_TrapPanel_8";
        private const string NoBaitKey = "WebUI_TrapPanel_9";
        private const string PreyReadyKey = "WebUI_TrapPanel_10";
        private const string NoPreyKey = "WebUI_TrapPanel_11";

        internal static void Send(long trapId)
        {
            var world = BaseSingleton<BattleLogicWorld>.Instance;
            var trap = world?._TrapManager?.GetTrapById(trapId);
            var config = ConfigManager.Instance;
            var trapConfig = trap != null ? config.Get_Config_Trap(trap.TrapConfigId) : null;
            if (trapConfig == null)
            {
                if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Trapline card: trap {trapId} not found");
                return;
            }

            // The bait, or the first bait in stock; the prey in the trap, or the first item of the storage box.
            int bait = trap.BaitItemConfigId;
            if (bait < 1 && trap.BaitStock != null && trap.BaitStock.Count > 0) bait = trap.BaitStock[0]?.ItemConfigId ?? 0;
            int prey = trap.HasPrey ? trap.PreyItemConfigId : 0;
            if (prey < 1)
            {
                var stored = world._ItemManager.GetTrapStoreItems(trapId);
                prey = stored != null && stored.Count > 0 ? stored[0]?.ItemConfigId ?? 0 : 0;
            }

            var card = CardLogic.Build(config.GetLocalTxt(trapConfig.Name), trap.CurrentDurability, trap.MaxDurability, trapConfig.Durability_Max,
                bait, Icon(config, bait), prey, Icon(config, prey));
            var texts = new CardLogic.Texts
            {
                Durability = ConstantTextTools.ToConstantText(DurabilityKey),
                BaitSet = ConstantTextTools.ToConstantText(BaitSetKey),
                NoBait = ConstantTextTools.ToConstantText(NoBaitKey),
                PreyReady = ConstantTextTools.ToConstantText(PreyReadyKey),
                NoPrey = ConstantTextTools.ToConstantText(NoPreyKey),
            };
            PageScript.Send(PageJson.SetCardCommand(PageJson.CardJson(trapId, card, texts)), "card");
            if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Trapline card: trap {trapId} sent");
        }

        private static string Icon(ConfigManager config, int itemId) => itemId > 0 ? config.Get_Config_Item(itemId)?.WebIcon ?? "" : "";
    }
}
