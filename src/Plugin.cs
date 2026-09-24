using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using UnityEngine;

namespace Trapline
{
    [BepInPlugin(PluginGuid, "Trapline", "1.0.0")]
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
                         typeof(CollectAllMessagePrefix),
                         typeof(InstallOnAddTrap),
                         typeof(InstallOnRemoveTrap),
                         typeof(FullBagStopPatch),
                         typeof(RemovalPollPatch),
                         typeof(GroupsOnRebuildFloorButtons),
                         typeof(GroupsOnFloorRefreshTags),
                         typeof(GroupsOnSwitchLanguage),
                     })
            {
                try { Harmony.CreateClassProcessor(type).Patch(); }
                catch (Exception e) { Log.LogWarning($"patch {type.Name} failed, its target method is missing: {e.Message}"); }
            }

            Log.LogInfo("Trapline loaded.");
        }
    }

    // Reads the click on the HUD's "Collect all" button and starts the pickup route. OnPageMessage's
    // logic is inlined into OnMessageFromJS, so the Prefix goes on OnMessageFromJS, the
    // one native entry point for every raw page message, root page and iframes alike. A message is four
    // fields joined by U+001E (buildProtocol(3, sourcePageId, type, json) in Root.html): [0] a fixed
    // type id, [1] the source page id, [2] the message type, [3] the JSON data. Only an exact match of
    // [2] is handled; a message whose type merely contains our string, and every other message, returns
    // true and runs through the game's normal handling untouched.
    [HarmonyPatch(typeof(WebUILayer), "OnMessageFromJS")]
    internal static class CollectAllMessagePrefix
    {
        private const string MessageType = "TRAPLINE_COLLECT_ALL";

        private static bool Prefix(Vuplex.WebView.EventArgs<string> eventArgs)
        {
            string text = eventArgs?.Value;
            if (string.IsNullOrEmpty(text)) return true;

            string[] parts = text.Split('\x1E');
            if (parts.Length < 3 || parts[2] != MessageType) return true;

            try { CollectAllRoute.Start(); }
            catch (Exception e) { Plugin.Log.LogWarning($"Trapline: {MessageType} handling failed: {e}"); }
            return false;
        }
    }

    // (Re)installs page.js's button feature into the CoreUI1 iframe whenever the trap list changes, the
    // same two reducer methods that fill the list itself. This both bootstraps the button the first time a trap gets prey and re-attaches
    // page.js's own MutationObserver if the web view ever gives CoreUI1 a fresh contentWindow.
    // page.js's install() is otherwise idempotent and cheap: window.__trapline is created once, and a
    // call that finds the observer already attached does no more than refresh the button from the
    // page's current state.
    [HarmonyPatch(typeof(Reducer_Web_CoreUI1), "RA_AddTrap")]
    internal static class InstallOnAddTrap
    {
        private static void Postfix()
        {
            try { PageScript.Install(); }
            catch (Exception e) { Plugin.Log.LogWarning($"Trapline: page install after RA_AddTrap failed: {e.Message}"); }
            // A placed trap can change which floor a trap slot counts against, so the floor groups are
            // due for a re-push too; TrapGroups.PushIfDirty coalesces this with every other trigger that
            // fires in the same frame.
            TrapGroups.MarkDirty();
        }
    }

    [HarmonyPatch(typeof(Reducer_Web_CoreUI1), "RA_RemoveTrap")]
    internal static class InstallOnRemoveTrap
    {
        private static void Postfix()
        {
            try { PageScript.Install(); }
            catch (Exception e) { Plugin.Log.LogWarning($"Trapline: page install after RA_RemoveTrap failed: {e.Message}"); }
            TrapGroups.MarkDirty();
        }
    }

    // Builds and starts one collect-all route: the traps with prey, ordered by the game-free RouteLogic,
    // dispatched through the game's own pickup action (TrapManager.OnStartPickupTrapPrey).
    internal static class CollectAllRoute
    {
        // The full-bag stop (FullBagStopPatch) reads and updates this on a failed pickup.
        internal static readonly RouteState State = new RouteState();

        // The remaining route trap ids to remove from the action queue after a full-bag stop, set by
        // FullBagStopPatch's Postfix and consumed by RemovalPollPatch on the next ActionManager.Update.
        // Null when nothing is pending. The failed trap's own pickup is still running inside
        // OnPickupTrapPrey when the Postfix fires, so the removal itself must wait a frame.
        private static List<int> pendingRemovalIds;

        internal static void QueuePendingRemoval(IReadOnlyList<int> remainingIds)
        {
            pendingRemovalIds = new List<int>(remainingIds);
        }

        // Removes, for each remaining route trap, the whole parent list that holds its queued pickup
        // (action id TrapManager.PickupPreyActionId, target id the trap id): one OnStartPickupTrapPrey
        // call queues one parent list holding both the walk and the pickup, so RemoveParentList also
        // stops the walk toward the next trap. Never removes a parent list whose pickup targets a trap
        // outside the given remaining set, so an action the player queued by hand is untouched.
        internal static void RunPendingRemoval()
        {
            var ids = pendingRemovalIds;
            if (ids == null) return;
            pendingRemovalIds = null;

            try
            {
                var world = BaseSingleton<BattleLogicWorld>.Instance;
                var actionManager = world._ActionManager;
                long agentId = world._AgentManager.GetLeadingRoleId();

                var source = actionManager.GetActionSource(agentId);
                var actionList = source?.ActionList;
                if (actionList == null) return;

                var parentIds = new List<long>();
                for (int i = 0; i < actionList.Count; i++)
                {
                    var action = actionList[i];
                    if (action.ActionId != TrapManager.PickupPreyActionId) continue;
                    if (!ids.Contains((int)action.TargetId)) continue;
                    if (!parentIds.Contains(action.ParentId)) parentIds.Add(action.ParentId);
                }

                foreach (long parentId in parentIds)
                    actionManager.RemoveParentList(agentId, parentId, false);

                if (Plugin.Verbose.Value)
                    Plugin.Log.LogDebug($"Trapline collect-all: full-bag stop removed {parentIds.Count} queued pickup(s)");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Trapline: full-bag pickup removal failed: {e.Message}");
            }
        }

        public static void Start()
        {
            var world = BaseSingleton<BattleLogicWorld>.Instance;
            var trapManager = world._TrapManager;
            var actionManager = world._ActionManager;
            var agentManager = world._AgentManager;
            var config = ConfigManager.Instance;

            long agentId = agentManager.GetLeadingRoleId();
            int characterFloor = agentManager.GetAgentFloor(agentId);
            var leadingRole = agentManager.GetLeadingRole();
            Vector3 characterPos = leadingRole?._transformComponent != null
                ? leadingRole._transformComponent.Position
                : default;

            var buildingNodes = config.customCache?.MapConfig?.BuildingNodeMap;

            // _placedTraps is an IL2CPP dictionary field; iterate it with GetEnumerator()/MoveNext()/
            // Current, not foreach (the interop enumerator lacks the foreach pattern).
            var traps = new List<RouteLogic.Trap>();
            var it = trapManager._placedTraps.GetEnumerator();
            while (it.MoveNext())
            {
                var trap = it.Current.Value;
                if (!trap.HasPrey) continue;
                if (actionManager.HasActionInQueue(agentId, TrapManager.PickupPreyActionId, trap.InstanceId)) continue;

                int floor = config.GetTrapSlotFloorByName(trap.SlotNodeName);
                // A slot with no node in BuildingNodeMap still gets a route entry, at the character's own
                // position, so it sorts early on its floor rather than being skipped.
                Vector3 pos = characterPos;
                if (buildingNodes != null) buildingNodes.TryGetValue(trap.SlotNodeName, out pos);

                traps.Add(new RouteLogic.Trap { Id = (int)trap.InstanceId, Floor = floor, X = pos.x, Z = pos.z });
            }

            if (traps.Count == 0)
            {
                if (Plugin.Verbose.Value) Plugin.Log.LogDebug("Trapline collect-all: no trap with prey to queue");
                return;
            }

            var floorCosts = new Dictionary<int, float>();
            foreach (var trap in traps)
            {
                if (trap.Floor == characterFloor || floorCosts.ContainsKey(trap.Floor)) continue;
                floorCosts[trap.Floor] = PlantChoreBatchManager.FloorCost(characterFloor, trap.Floor);
            }

            var route = RouteLogic.BuildRoute(traps, characterFloor, characterPos.x, characterPos.z, floorCosts);
            State.Start(route);

            if (Plugin.Verbose.Value)
                Plugin.Log.LogDebug($"Trapline collect-all: characterFloor={characterFloor} route=[{string.Join(",", route)}]");

            foreach (int trapId in route)
                trapManager.OnStartPickupTrapPrey(trapId);
        }
    }

    // Marks a route trap done or failed when its pickup resolves: on a full bag the game shows its own
    // notification, and the mod stops the route. The Prefix records whether the trap was part of the
    // active route before the game's own pickup logic runs; the Postfix then reads the trap's HasPrey
    // after that logic to tell a successful pickup (no prey left) from a failed one (the bag had no
    // room, so the prey stays in the trap). A failed pickup queues the remaining route ids for
    // RemovalPollPatch to remove on the next ActionManager.Update, because the failed trap's own pickup
    // action is still running here in the Postfix. A pickup the route did not queue (__state false, a
    // click by hand or a trap already resolved) changes nothing.
    [HarmonyPatch(typeof(TrapManager), "OnPickupTrapPrey")]
    internal static class FullBagStopPatch
    {
        private static void Prefix(long trapInstanceId, out bool __state)
        {
            __state = CollectAllRoute.State.Contains((int)trapInstanceId);
        }

        private static void Postfix(TrapManager __instance, long trapInstanceId, bool __state)
        {
            if (!__state) return;
            try
            {
                int trapId = (int)trapInstanceId;
                var trap = __instance.GetTrapById(trapInstanceId);
                if (trap != null && trap.HasPrey)
                {
                    var remaining = CollectAllRoute.State.Fail(trapId);
                    if (remaining.Count > 0) CollectAllRoute.QueuePendingRemoval(remaining);
                }
                else
                {
                    CollectAllRoute.State.MarkDone(trapId);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Trapline: OnPickupTrapPrey postfix failed: {e.Message}");
            }
        }
    }

    // Runs the pending full-bag removal one frame after it was queued, and, coalesced the same way,
    // pushes the floor groups and the button/label words at most once per frame no matter how many of
    // their triggers fired in it. ActionManager.Update(float, float) is the per-frame tick of
    // the action system itself: NativeDisasm shows BattleLogicWorld.OnUpdateAction calls it
    // unconditionally once its ActionManager field is set, alongside every other gameplay manager's own
    // Update, so it runs every frame during play. A Postfix here is a plain per-frame hook with no extra
    // MonoBehaviour needed, and (unlike an injected MonoBehaviour) it takes no delegate-typed parameter,
    // so it does not hit the Il2CppInterop warning for that shape.
    [HarmonyPatch(typeof(ActionManager), "Update")]
    internal static class RemovalPollPatch
    {
        private static void Postfix()
        {
            CollectAllRoute.RunPendingRemoval();
            TrapGroups.PushIfDirty();
        }
    }

    // Captures State_Web_CoreUI1.FloorButtonsJson each time the game rebuilds the floor switcher's own
    // button list, the only source for which floor ids exist and their display order (for example ids
    // 1, 101, 102 in button order 102, 1, 101). The floor NAME is deliberately not read from this JSON - see
    // TrapGroups.FloorLabel.
    [HarmonyPatch(typeof(Reducer_Web_CoreUI1), "RebuildFloorButtons")]
    internal static class GroupsOnRebuildFloorButtons
    {
        private static void Postfix(State_Web_CoreUI1 state)
        {
            try { TrapGroups.CaptureFloorButtonsJson(state?.FloorButtonsJson?.Value); }
            catch (Exception e) { Plugin.Log.LogWarning($"Trapline: capture FloorButtonsJson failed: {e.Message}"); }
        }
    }

    // A slot or floor unlock refreshes the floor tags that feed the usable-slot flags, so the usable
    // slots of a floor can change even with no trap placed or removed.
    [HarmonyPatch(typeof(Reducer_Web_CoreUI1), "RA_FloorRefreshTags")]
    internal static class GroupsOnFloorRefreshTags
    {
        private static void Postfix()
        {
            TrapGroups.MarkDirty();
        }
    }

    // A language switch changes the button word and the floor labels. ConfigManager.SwitchLanguage(Boolean isChinese) returns a UniTask, so the
    // Postfix fires once the method itself returns, not once the switch finishes; the mark only sets a
    // flag, and the actual push waits for the next ActionManager.Update frame (RemovalPollPatch), which
    // gives the switch a frame to complete before TrapGroups reads the words and the floor names again.
    [HarmonyPatch(typeof(ConfigManager), "SwitchLanguage")]
    internal static class GroupsOnSwitchLanguage
    {
        private static void Postfix()
        {
            TrapGroups.MarkDirty();
        }
    }

    // The collect-all button's word.
    internal static class CollectAllWord
    {
        private const string GameTextKey = "WebUI_ItemPickup_1";
        private const string FallbackEnglish = "Collect all";
        private const string FallbackChinese = "一键收取";

        // Reads the game's own word for the button fresh on every call, because the display language can
        // change while the game runs. Falls back to the mod's own word, by the current display language,
        // only when the game gives no text for the key - not expected at runtime (WebUI_ItemPickup_1
        // resolves to "Take All" / "全部拾取"), but kept as a safety net for a game
        // update that removes or renames the key.
        internal static string Current()
        {
            string text = ConstantTextTools.ToConstantTextOrEmpty(GameTextKey);
            if (!string.IsNullOrEmpty(text)) return text;
            bool chinese = ConfigManager.Instance?.customCache?.LanguageType == LanguageType.Chinese;
            return chinese ? FallbackChinese : FallbackEnglish;
        }
    }

    // Computes the floor groups and the button word, and pushes both to page.js in one call. Every trigger that can change
    // either one - a trap placed or removed, a floor-tag refresh, the floor switcher rebuilding its own
    // button list, or a language switch - only sets the dirty flag; RemovalPollPatch's per-frame Postfix
    // on ActionManager.Update drains it at most once per frame, so many triggers in the same frame (for
    // example one RA_AddTrap per trap at load) cost one push.
    internal static class TrapGroups
    {
        private static string lastFloorButtonsJson;
        private static bool dirty;

        internal static void MarkDirty() => dirty = true;

        internal static void CaptureFloorButtonsJson(string json)
        {
            if (!string.IsNullOrEmpty(json)) lastFloorButtonsJson = json;
            MarkDirty();
        }

        internal static void PushIfDirty()
        {
            if (!dirty) return;
            dirty = false;
            try { Push(); }
            catch (Exception e) { Plugin.Log.LogWarning($"Trapline: group/word push failed: {e.Message}"); }
        }

        // Only "id" is read out of the floor-button JSON; it and the array order are the one thing
        // nothing else gives. The JSON's own
        // "name" field is not used - see FloorLabel.
        private static readonly Regex FloorIdPattern = new Regex("\"id\":(-?\\d+)", RegexOptions.Compiled);

        private static List<int> ParseFloorButtonIds(string json)
        {
            var ids = new List<int>();
            if (string.IsNullOrEmpty(json)) return ids;
            foreach (Match m in FloorIdPattern.Matches(json))
                if (int.TryParse(m.Groups[1].Value, out int id) && !ids.Contains(id))
                    ids.Add(id);
            return ids;
        }

        // The floor name in the current display language. The floor-button JSON also carries
        // a name, but the floor switcher rebuilds it only on its own triggers (RA_FloorRefreshTags and
        // similar, confirmed by NativeDisasm to be what calls RebuildFloorButtons), not on every
        // SwitchLanguage, so a name read out of a JSON captured before the switch could stay in the old
        // language until the player happens to reopen the floor switcher. Reducer_Web_CoreUI1's own
        // RebuildFloorButtons resolves each button's name from Config_MapPoint.Name (a text key) through
        // ConstantTextTools.GetLocalText; this does the same lookup with ConfigManager.GetLocalTxt directly, every push, so the label
        // always matches ConfigManager's current language with no dependency on when the JSON was last
        // rebuilt. Falls back to the floor id itself when the map point or its name key is missing.
        private static string FloorLabel(ConfigManager config, int floorId)
        {
            try
            {
                var point = config.Get_Config_MapPoint(floorId);
                if (point != null && !string.IsNullOrEmpty(point.Name))
                {
                    string text = config.GetLocalTxt(point.Name);
                    if (!string.IsNullOrEmpty(text)) return text;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Trapline: floor label lookup for {floorId} failed: {e.Message}");
            }
            return floorId.ToString();
        }

        private static void Push()
        {
            var world = BaseSingleton<BattleLogicWorld>.Instance;
            var trapManager = world._TrapManager;
            var config = ConfigManager.Instance;
            var buildingNodes = config.customCache?.MapConfig?.BuildingNodeMap;

            // Config_TrapSlot is an IL2CPP dictionary field; iterate it with GetEnumerator()/MoveNext()/
            // Current, not foreach (the interop enumerator lacks the foreach pattern).
            var slots = new List<SlotLogic.Slot>();
            var slotIt = config._Config_TrapSlot_Dict.GetEnumerator();
            while (slotIt.MoveNext())
            {
                var slot = slotIt.Current.Value;
                slots.Add(new SlotLogic.Slot
                {
                    Id = slotIt.Current.Key,
                    NodeName = slot.Position,
                    Floor = config.GetTrapSlotFloorByName(slot.Position),
                    Unlocked = AreaUnlockConstants.IsSlotUnlocked(slot.Position),
                    BlockedByPreDisaster = FloorTagConstants.IsSlotBlockedByPreDisasterFloor(slot.Position),
                    InHomeMap = buildingNodes != null && buildingNodes.ContainsKey(slot.Position),
                });
            }

            var trapNodeNames = new HashSet<string>();
            var trapFloor = new Dictionary<long, int>();
            var trapNode = new Dictionary<long, string>();
            var trapIt = trapManager._placedTraps.GetEnumerator();
            while (trapIt.MoveNext())
            {
                var trap = trapIt.Current.Value;
                trapNodeNames.Add(trap.SlotNodeName);
                trapFloor[trap.InstanceId] = config.GetTrapSlotFloorByName(trap.SlotNodeName);
                trapNode[trap.InstanceId] = trap.SlotNodeName;
            }

            var floorOrder = ParseFloorButtonIds(lastFloorButtonsJson);
            var counts = SlotLogic.LayoutFloors(slots, trapNodeNames, floorOrder);

            // The cell of each trap on its floor line, in the fixed slot order. A trap whose node
            // has no cell is left out, and page.js puts it on a line after all floors.
            var trapCell = new Dictionary<long, int>();
            foreach (var kv in trapNode)
            {
                foreach (var layout in counts)
                {
                    if (layout.Cells.TryGetValue(kv.Value, out int cell)) { trapCell[kv.Key] = cell; break; }
                }
            }

            var groupsJson = new StringBuilder();
            groupsJson.Append("{\"floors\":[");
            for (int i = 0; i < counts.Count; i++)
            {
                if (i > 0) groupsJson.Append(',');
                var c = counts[i];
                groupsJson.Append("{\"id\":").Append(c.Floor)
                    .Append(",\"label\":\"").Append(JsonEscape(FloorLabel(config, c.Floor))).Append('"')
                    .Append(",\"traps\":").Append(c.Traps)
                    .Append(",\"slots\":").Append(c.Slots)
                    .Append('}');
            }
            groupsJson.Append("],\"trapFloor\":{");
            bool firstTrap = true;
            foreach (var kv in trapFloor)
            {
                if (!firstTrap) groupsJson.Append(',');
                firstTrap = false;
                groupsJson.Append('"').Append(kv.Key).Append("\":").Append(kv.Value);
            }
            groupsJson.Append("},\"trapCell\":{");
            firstTrap = true;
            foreach (var kv in trapCell)
            {
                if (!firstTrap) groupsJson.Append(',');
                firstTrap = false;
                groupsJson.Append('"').Append(kv.Key).Append("\":").Append(kv.Value);
            }
            groupsJson.Append("}}");

            string collectAll = CollectAllWord.Current();
            string textJson = "{\"collectAll\":\"" + JsonEscape(collectAll) + "\"}";

            PageScript.SetTextAndGroups(textJson, groupsJson.ToString());

            if (Plugin.Verbose.Value)
                Plugin.Log.LogDebug($"Trapline groups: floors={counts.Count} traps={trapFloor.Count} cells={trapCell.Count} collectAll='{collectAll}'");
        }

        // Escapes a string for embedding as a JSON string literal in the hand-built payload above; there
        // is no JSON library available to the mod for this (netstandard2.1 has no System.Text.Json, and
        // the game's own Newtonsoft.Json is not usable from mod code).
        private static string JsonEscape(string s)
        {
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }

    // Installs page.js (an embedded resource) into the root page, which reaches the CoreUI1 iframe
    // itself.
    internal static class PageScript
    {
        private static string script;
        private static readonly HashSet<string> loggedMissing = new HashSet<string>();
        private static readonly HashSet<string> loggedOther = new HashSet<string>();

        private static string Script()
        {
            if (script != null) return script;
            var assembly = typeof(PageScript).Assembly;
            string name = Array.Find(assembly.GetManifestResourceNames(), n => n.EndsWith("page.js", StringComparison.Ordinal));
            using (var stream = assembly.GetManifestResourceStream(name))
            using (var reader = new System.IO.StreamReader(stream))
                script = reader.ReadToEnd();
            return script;
        }

        public static void Install()
        {
            var webView = ReduxUISystem.Instance?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;
            if (webView == null)
            {
                Plugin.Log.LogWarning("Trapline: web view not found");
                return;
            }
            string js = Script() + ";window.__trapline.install();";
            webView.ExecuteJavaScript(js, (Il2CppSystem.Action<string>)(r =>
            {
                LogPageCheck(r);
                if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Trapline page script: {r}");
            }));
        }

        // Pushes the button word and the floor groups together (TrapGroups.Push): install() first (the
        // button and the groups both need page.js's window.__trapline in place, and install() is
        // idempotent and cheap when it already is), then setText, then setGroups, so one push always
        // leaves both features current even if page.js was never explicitly installed yet.
        public static void SetTextAndGroups(string textJson, string groupsJson)
        {
            var webView = ReduxUISystem.Instance?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;
            if (webView == null)
            {
                Plugin.Log.LogWarning("Trapline: web view not found");
                return;
            }
            string js = Script() + ";window.__trapline.install();window.__trapline.setText(" + textJson
                        + ");window.__trapline.setGroups(" + groupsJson + ");";
            webView.ExecuteJavaScript(js, (Il2CppSystem.Action<string>)(r =>
            {
                LogPageCheck(r);
                if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Trapline page script: {r}");
            }));
        }

        // page.js's result is "installed", "installed; missing: <parts>", "no CoreUI1 frame", or
        // "error: ...". Only a missing part or an error logs a warning, and each distinct message logs once, so a
        // game update is not silent but a normal call does not spam the log.
        private static void LogPageCheck(string r)
        {
            // No CoreUI1 frame is normal while the main menu or a save load is on screen.
            if (r == null || r == "installed" || r == "no CoreUI1 frame") return;
            const string missingMarker = "; missing: ";
            int missingAt = r.IndexOf(missingMarker, StringComparison.Ordinal);
            if (missingAt >= 0)
            {
                string missing = r.Substring(missingAt + missingMarker.Length);
                if (loggedMissing.Add(missing)) Plugin.Log.LogWarning($"Trapline page check: missing {missing}");
                return;
            }
            if (loggedOther.Add(r)) Plugin.Log.LogWarning($"Trapline page check: {r}");
        }
    }
}
