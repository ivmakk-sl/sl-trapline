using System;
using HarmonyLib;
using GameCore.HotUpdate.ReduxUI;

namespace Trapline
{
    // Reads the page messages of the mod: the click on the HUD's Take All button and the asks of the trap grid.
    // OnPageMessage's logic is inlined into OnMessageFromJS, so the Prefix goes on OnMessageFromJS, the one native entry
    // point for every raw page message, root page and iframes alike. A message is four fields joined by U+001E
    // (buildProtocol(3, sourcePageId, type, json) in Root.html): [0] a fixed type id, [1] the source page id, [2] the
    // message type, [3] the JSON data. Only an exact match of [2] is handled; every other message returns true and runs
    // through the game's normal handling untouched.
    [HarmonyPatch(typeof(WebUILayer), "OnMessageFromJS")]
    internal static class PageOnMessageFromJS
    {
        private static bool Prefix(Vuplex.WebView.EventArgs<string> eventArgs)
        {
            string text = eventArgs?.Value;
            if (string.IsNullOrEmpty(text) || text.IndexOf("TRAPLINE_", StringComparison.Ordinal) < 0) return true;

            string[] parts = text.Split('\x1E');
            if (parts.Length < 3) return true;
            string type = parts[2];
            string json = parts.Length > 3 ? parts[3] : "";
            try
            {
                switch (type)
                {
                    case "TRAPLINE_TAKE_ALL":
                        TakeAllRoute.Start();
                        return false;
                    case "TRAPLINE_CARD":
                        if (PageJson.MessageLong(json, "trap") is long trap) TrapCard.Send(trap);
                        return false;
                    case "TRAPLINE_REPLACE":
                        if (PageJson.MessageBool(json, "on") is bool on)
                        {
                            SaveKeys.SetGlobal(on);
                            TrapGroups.MarkDirty();
                            if (Plugin.Verbose.Value) Plugin.Log.LogDebug("Trapline replace: Auto-replace " + (on ? "on" : "off"));
                        }
                        return false;
                    case "TRAPLINE_SLOT_OPTION":
                        if (PageJson.MessageString(json, "slot") is string optionSlot && PageJson.MessageBool(json, "on") is bool optionOn)
                        {
                            SaveKeys.SetSlot(optionSlot, optionOn);
                            if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Trapline replace: slot {optionSlot} option {(optionOn ? "on" : "off")}");
                        }
                        return false;
                    case "TRAPLINE_PIN":
                        if (PageJson.MessageBool(json, "on") is bool pin)
                        {
                            Plugin.PinGrid.Value = pin;
                            TrapGroups.MarkDirty();
                            if (Plugin.Verbose.Value) Plugin.Log.LogDebug("Trapline pin: " + (pin ? "on" : "off"));
                        }
                        return false;
                    case "TRAPLINE_MENU":
                        if (PageJson.MessageString(json, "slot") is string menuSlot) SetTrapMenu.Send(menuSlot);
                        return false;
                    case "TRAPLINE_SET":
                        if (PageJson.MessageString(json, "slot") is string setSlot && PageJson.MessageLong(json, "trap") is long kind)
                            SetTrapMenu.Set(setSlot, (int)kind);
                        return false;
                    default:
                        return true;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Trapline: {type} handling failed: {e}");
                return false;
            }
        }
    }
}
