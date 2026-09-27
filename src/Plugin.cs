using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using GameCore.HotUpdate.Battle.Logic;

namespace Trapline
{
    [BepInPlugin(PluginGuid, "Trapline", "1.0.1")]
    [BepInProcess("SurvivalLog.exe")]
    public sealed class Plugin : BasePlugin
    {
        public const string PluginGuid = "com.ivmakk.survivallog.trapline";

        internal static new ManualLogSource Log;
        internal static Harmony Harmony;
        internal static ConfigEntry<bool> Verbose;

        public override void Load()
        {
            Log = base.Log;
            Verbose = Config.Bind(
                "General", "Verbose", false,
                "Log route and page-script detail at Debug level. Keep off in normal play.");
            Harmony = new Harmony(PluginGuid);
            // Each patch target is attached on its own, so a target missing after a game update turns
            // off only its own feature.
            foreach (var type in new[]
                     {
                         typeof(TakeAllOnMessageFromJS),
                         typeof(TakeAllOnPickupTrapPrey),
                         typeof(GridOnAddTrap),
                         typeof(GridOnRemoveTrap),
                         typeof(GridOnRebuildFloorButtons),
                         typeof(GridOnFloorRefreshTags),
                         typeof(GridOnSwitchLanguage),
                         typeof(FramePatch),
                     })
            {
                try { Harmony.CreateClassProcessor(type).Patch(); }
                catch (Exception e) { Log.LogWarning($"patch {type.Name} failed, its target method is missing: {e.Message}"); }
            }

            Log.LogInfo("Trapline loaded.");
        }
    }

    // Runs the pending full-bag removal one frame after it was queued, then the push tick: it pushes the
    // floor groups and the button word at most once per frame no matter how many of their triggers fired
    // in it, or checks about once each real second that the page still has the page script. ActionManager.Update(float, float) is the per-frame tick of
    // the action system itself: NativeDisasm shows BattleLogicWorld.OnUpdateAction calls it
    // unconditionally once its ActionManager field is set, alongside every other gameplay manager's own
    // Update, so it runs every frame during play. A Postfix here is a plain per-frame hook with no extra
    // MonoBehaviour needed, and (unlike an injected MonoBehaviour) it takes no delegate-typed parameter,
    // so it does not hit the Il2CppInterop warning for that shape.
    [HarmonyPatch(typeof(ActionManager), "Update")]
    internal static class FramePatch
    {
        private static void Postfix()
        {
            TakeAllRoute.RunPendingRemoval();
            TrapGroups.Tick();
        }
    }
}
