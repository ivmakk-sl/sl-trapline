using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Show;
using HarmonyLib;
using UnityEngine;

namespace Trapline
{
    // The Set Trap menu of a free trap slot: the page asks for its lines when the player clicks the cell (TRAPLINE_MENU),
    // and a click on a line sets a trap of that kind (TRAPLINE_SET). The game's own Set Trap goes to the end of the queue.
    internal static class SetTrapMenu
    {
        // The game's text for no trap in its trap select window (ConstantText.tsv), shown as the game's floating message.
        private const string NoTrapsKey = "WebUI_TrapSelectPanel_7";

        internal static void Send(string slot)
        {
            var config = ConfigManager.Instance;
            var lines = new List<PageJson.MenuLine>();
            foreach (var line in MenuLogic.Lines(TrapPool.Read()))
            {
                var item = config.Get_Config_Item(line.ItemConfigId);
                lines.Add(new PageJson.MenuLine
                {
                    Kind = line.Kind,
                    Icon = item?.WebIcon ?? "",
                    Name = item != null ? config.GetLocalTxt(item.ItemName) : line.ItemConfigId.ToString(),
                    Count = line.Count,
                });
            }
            // With no trap, the game's floating message in place of a menu; the empty menu closes an open one.
            if (lines.Count == 0) EventDispatcherTools.NotifyGlobal<string>(EnumNotify_Show.PopText, ConstantTextTools.ToConstantText(NoTrapsKey));
            PageScript.Send(PageJson.SetMenuCommand(PageJson.MenuJson(slot, lines)), "menu");
            open = lines.Count > 0;
            leave.Reset();
            if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Trapline menu: slot {slot} lines {lines.Count}");
        }

        // True from a menu with lines until the mod closes it. The page can close it first (a line, a click in the HUD);
        // a later close is then an empty menu that changes nothing.
        private static bool open;
        private static readonly MenuLogic.Leave leave = new MenuLogic.Leave();

        // FramePatch, each frame: with a menu open, the game's own check of the pointer over a HUD area
        // (MouseRaycastService.IsPointerOverUI, cached by the game for the frame). The page cannot see a fast move onto
        // the world.
        internal static void Tick()
        {
            if (!open) return;
            try
            {
                if (leave.Step(MouseRaycastService.IsPointerOverUI(), Time.unscaledTime)) Close("pointer over the world");
            }
            catch (Exception e)
            {
                open = false;
                Plugin.WarnOnce($"Trapline: Set Trap menu pointer check failed: {e.Message}");
            }
        }

        // Closes an open menu with an empty menu, which the page draws as no menu.
        internal static void Close(string reason)
        {
            if (!open) return;
            open = false;
            PageScript.Send(PageJson.SetMenuCommand(PageJson.MenuJson("", new List<PageJson.MenuLine>())), "menu");
            if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Trapline menu: closed ({reason})");
        }

        internal static void Set(string slot, int kind)
        {
            open = false;
            var pick = MenuLogic.PickItem(TrapPool.Read(), kind);
            if (pick == null)
            {
                if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Trapline set: slot {slot} kind {kind} skipped (no item)");
                return;
            }
            bool queued = TrapInstall.Send(slot, pick.Item, pick.ItemConfigId, kind, false, out _);
            if (Plugin.Verbose.Value)
                Plugin.Log.LogDebug($"Trapline set: slot {slot} kind {kind} item {pick.Item} owner {pick.Owner}" + (queued ? "" : " (refused by the game)"));
        }
    }

    // A click in the game world (the game's CameraTouch image behind the HUD reports it) closes an open Set Trap menu
    // at once: that click never reaches the HUD page.
    [HarmonyPatch(typeof(CameraManager), "OnSelectModel")]
    internal static class MenuOnWorldClick
    {
        private static void Postfix()
        {
            try { SetTrapMenu.Close("click in the world"); }
            catch (Exception e) { Plugin.WarnOnce($"Trapline: Set Trap menu close failed: {e.Message}"); }
        }
    }
}
