using System;
using System.Collections.Generic;
using HarmonyLib;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;

namespace Trapline
{
    // Runs the trappanel bundle (the embedded resource "Trapline.trappanel.js") in the root page, which reaches the frame
    // of the trap window itself and adds the Auto-replace checkbox of its trap slot to the header; it retries for a short
    // time when the frame does not exist yet.
    internal static class TrapPanelScript
    {
        private static string script;
        private static readonly HashSet<string> logged = new HashSet<string>();

        private static string Script()
        {
            if (script != null) return script;
            using (var stream = typeof(TrapPanelScript).Assembly.GetManifestResourceStream("Trapline.trappanel.js"))
            using (var reader = new System.IO.StreamReader(stream))
                script = reader.ReadToEnd();
            return script;
        }

        internal static void SetState(string slot, bool on)
        {
            var webView = ReduxUISystem.Instance?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;
            if (webView == null) return;
            string json = PageJson.TrapPanelJson(slot, on, ModTexts.Current().ToDictionary());
            string js = Script() + ";window.__traplineTrap.setState(" + json + ");window.__traplineTrap.install();";
            webView.ExecuteJavaScript(js, (Il2CppSystem.Action<string>)(r =>
            {
                if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Trapline trap window script: {r}");
                // "no frame" is normal: the script retries until the frame loads.
                if (r != null && r != "installed" && r != "no frame" && logged.Add(r))
                    Plugin.Log.LogWarning($"Trapline trap window check: {r}");
            }));
        }
    }

    // The trap window opens (a click on a trap cell or on a trap in the world): Reducer_Web_TrapPanel.RA_Open gives the
    // new window state with the trap of the window, and the checkbox of its trap slot goes into the window.
    [HarmonyPatch(typeof(Reducer_Web_TrapPanel), "RA_Open")]
    internal static class ReplaceOnTrapPanelOpen
    {
        private static void Postfix(State_Web_TrapPanel __result)
        {
            try
            {
                if (__result == null) return;
                long trapId = __result.TrapInstanceId;
                var trap = GameCore.HotUpdate.BaseSingleton<BattleLogicWorld>.Instance?._TrapManager?.GetTrapById(trapId);
                if (trap == null) return;
                bool on = SaveKeys.SlotOn(trap.SlotNodeName);
                TrapPanelScript.SetState(trap.SlotNodeName, on);
                if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Trapline replace: window trap {trapId} slot {trap.SlotNodeName} option {(on ? "on" : "off")}");
            }
            catch (Exception e)
            {
                Plugin.WarnOnce($"Trapline: trap window checkbox failed: {e.Message}");
            }
        }
    }
}
