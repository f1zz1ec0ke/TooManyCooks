using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppTMPro;
using MelonLoader;
using TooManyCooks;
using UnityEngine;

[assembly: MelonInfo(typeof(TooManyCooksMod), "TooManyCooks", "1.0.0", "dcdv9")]
[assembly: MelonGame("Biotech Gameworks", "Kebab Chefs! - Restaurant Simulator")]

namespace TooManyCooks
{
    /// <summary>
    /// Raises the lobby player cap from 4 to MaxPlayers.
    ///
    /// Four things bound the count independently:
    ///   - the Steam lobby size            (SteamMatchmaking.CreateLobbyAsync)
    ///   - the create-room dropdown        (prefab-authored options, CreateRoomHandler._playerCount)
    ///   - the lobby slot arrays           (LobbyUI.LobbySlots, LobbyUI.Characters)
    ///   - how many of those slots are activated
    /// </summary>
    public class TooManyCooksMod : MelonMod
    {
        internal static MelonLogger.Instance Log;
        internal static MelonPreferences_Entry<int> MaxPlayers;
        internal static MelonPreferences_Entry<bool> ForceMaxOnHost;

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;

            var cfg = MelonPreferences.CreateCategory("TooManyCooks");
            MaxPlayers = cfg.CreateEntry("MaxPlayers", 6,
                description: "Lobby size cap. Vanilla is 4.");
            ForceMaxOnHost = cfg.CreateEntry("ForceMaxOnHost", true,
                description: "Force the hosted lobby to MaxPlayers even if the create-room UI passed a smaller number. " +
                             "The Invite Friends path has no selector and always passes 4.");

            Log.Msg($"Ready. MaxPlayers={MaxPlayers.Value} ForceMaxOnHost={ForceMaxOnHost.Value}");
            Log.Msg("Press F9 in-game to dump live lobby slot state.");
        }

        private float _nextSlotCheck;

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F9))
                LobbyDump.Dump();

            if (Time.unscaledTime < _nextSlotCheck) return;
            _nextSlotCheck = Time.unscaledTime + 0.5f;
            SlotActivator.Tick();
        }
    }

    /// <summary>
    /// Activates lobby slots past the fourth. The game enables indices 0-3 only; vanilla
    /// LobbySlots holds 5 entries, so index 4 is inactive before any cloning.
    /// </summary>
    internal static class SlotActivator
    {
        internal static void Tick()
        {
            var glm = GameLobbyManager.Instance;
            if (glm == null) return;
            Ensure(glm._roomLobbyUI, "roomLobbyUI");
            Ensure(glm._mainMenuLobbyUI, "mainMenuLobbyUI");
        }

        private static void Ensure(GameLobbyManager.LobbyUI ui, string label)
        {
            if (ui == null) return;
            var slots = ui.LobbySlots;
            if (slots == null || slots.Length == 0) return;

            // Slot 0 is active only while this UI is on screen; used here as the "is visible" test.
            var first = slots[0];
            if (first == null || !first.gameObject.activeInHierarchy) return;

            int want = System.Math.Min(TooManyCooksMod.MaxPlayers.Value, slots.Length);
            for (int i = 1; i < want; i++)
            {
                var s = slots[i];
                if (s == null) continue;
                if (s.gameObject.activeSelf) continue;

                s.gameObject.SetActive(true);
                s.ResetSlot();
                TooManyCooksMod.Log.Msg($"[Slots] {label}: activated slot [{i}] '{s.gameObject.name}'");
            }
        }
    }

    /// <summary>
    /// F9 diagnostic. Dumps slot arrays, character arrays and lobby state to the MelonLoader log.
    /// </summary>
    internal static class LobbyDump
    {
        internal static void Dump()
        {
            var log = TooManyCooksMod.Log;
            log.Msg("================ F9 LOBBY DUMP ================");

            var glm = GameLobbyManager.Instance;
            if (glm == null)
            {
                log.Msg("GameLobbyManager.Instance == null (not in a lobby?)");
            }
            else
            {
                DumpUI(glm._mainMenuLobbyUI, "_mainMenuLobbyUI");
                DumpUI(glm._roomLobbyUI, "_roomLobbyUI");
                log.Msg($"_playerInfos={CountDict(glm)}");
            }

            var mmn = MainMenuNetworkManager.Instance;
            if (mmn != null)
            {
                log.Msg($"MainMenuNetworkManager: _lastPlayerCount='{mmn._lastPlayerCount}' _lastRoomName='{mmn._lastRoomName}'");

                // CurrentLobby is a Nullable<Lobby> struct and marshals unreliably through
                // Il2CppInterop; OnLobbyCreated's values are the ones to trust.
                var lob = mmn.CurrentLobby;
                if (lob.HasValue)
                    log.Msg($"CurrentLobby (unreliable): MaxMembers={lob.Value.MaxMembers} MemberCount={lob.Value.MemberCount} id={lob.Value.Id}");
                else
                    log.Msg("CurrentLobby: none");
            }
            log.Msg("===============================================");
        }

        private static string CountDict(GameLobbyManager glm)
        {
            try { return glm._playerInfos != null ? glm._playerInfos.Count.ToString() : "null"; }
            catch { return "?"; }
        }

        private static void DumpUI(GameLobbyManager.LobbyUI ui, string label)
        {
            var log = TooManyCooksMod.Log;
            if (ui == null) { log.Msg($"{label}: null"); return; }

            var slots = ui.LobbySlots;
            log.Msg($"{label}.LobbySlots: length={(slots == null ? -1 : slots.Length)}");
            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    var s = slots[i];
                    if (s == null) { log.Msg($"   [{i}] <null>"); continue; }
                    var go = s.gameObject;
                    log.Msg($"   [{i}] '{go.name}' activeSelf={go.activeSelf} activeInHierarchy={go.activeInHierarchy} " +
                            $"IsSet={s.IsSet} user='{s.CurrentUsername}' pos={go.transform.position} parent='{go.transform.parent?.name}'");
                }
            }

            var chars = ui.Characters;
            log.Msg($"{label}.Characters: length={(chars == null ? -1 : chars.Length)}");
            if (chars != null)
            {
                for (int i = 0; i < chars.Length; i++)
                {
                    var c = chars[i];
                    if (c == null) { log.Msg($"   [{i}] <null>"); continue; }
                    log.Msg($"   [{i}] '{c.gameObject.name}' activeSelf={c.gameObject.activeSelf} pos={c.transform.position}");
                }
            }
        }
    }

    /// <summary>
    /// Rebuilds the "Max Players" dropdown to offer 2..MaxPlayers, and raises _maxPlayerCount.
    /// The dropdown's options are authored in the prefab rather than derived from
    /// _maxPlayerCount, and it is not a field on CreateRoomHandler; it lives under _createRoomMenu.
    /// </summary>
    [HarmonyPatch(typeof(CreateRoomHandler), nameof(CreateRoomHandler.OpenCreateRoomMenu))]
    internal static class CreateRoomHandler_OpenCreateRoomMenu
    {
        private static void Postfix(CreateRoomHandler __instance)
        {
            int want = TooManyCooksMod.MaxPlayers.Value;
            int before = __instance._maxPlayerCount;
            __instance._maxPlayerCount = want;

            TooManyCooksMod.Log.Msg(
                $"[UI] _maxPlayerCount {before} -> {__instance._maxPlayerCount} (_playerCount={__instance._playerCount})");

            var menu = __instance._createRoomMenu;
            if (menu == null)
            {
                TooManyCooksMod.Log.Warning("[UI] _createRoomMenu was null; cannot reach the dropdown.");
                return;
            }

            // Include inactive: the menu may not be active when this runs.
            var dd = menu.GetComponentInChildren<TMP_Dropdown>(true);
            if (dd == null)
            {
                TooManyCooksMod.Log.Warning("[UI] No TMP_Dropdown under _createRoomMenu. Hierarchy follows:");
                DumpHierarchy(menu.transform, 0);
                return;
            }

            var existing = new List<string>();
            for (int i = 0; i < dd.options.Count; i++)
                existing.Add(dd.options[i].text);
            TooManyCooksMod.Log.Msg($"[UI] dropdown '{dd.gameObject.name}' options before: [{string.Join(", ", existing)}] value={dd.value}");

            // Vanilla options start at 2.
            var opts = new Il2CppSystem.Collections.Generic.List<string>();
            for (int i = 2; i <= want; i++)
                opts.Add(i.ToString());

            dd.ClearOptions();
            dd.AddOptions(opts);
            dd.value = opts.Count - 1;
            dd.RefreshShownValue();

            var after = new List<string>();
            for (int i = 0; i < dd.options.Count; i++)
                after.Add(dd.options[i].text);
            TooManyCooksMod.Log.Msg($"[UI] dropdown options after: [{string.Join(", ", after)}] value={dd.value} caption='{dd.captionText?.text}'");
        }

        private static void DumpHierarchy(Transform t, int depth)
        {
            var comps = t.GetComponents<Component>();
            var names = new List<string>();
            foreach (var c in comps)
                if (c != null) names.Add(c.GetIl2CppType().Name);

            TooManyCooksMod.Log.Msg($"[UI]   {new string(' ', depth * 2)}{t.name} :: {string.Join(", ", names)}");

            for (int i = 0; i < t.childCount; i++)
                DumpHierarchy(t.GetChild(i), depth + 1);
        }
    }

    /// <summary>
    /// Sets the Steam lobby size. Both host paths reach this:
    ///   CreateRoomHandler.CreateRoom -> CreateLobbyAsync
    ///   MainMenuLobbyManager.InviteFriendsAndCreateLobby -> MainMenuNetworkManager.StartHost -> CreateLobbyAsync
    /// </summary>
    [HarmonyPatch(typeof(Il2CppSteamworks.SteamMatchmaking), nameof(Il2CppSteamworks.SteamMatchmaking.CreateLobbyAsync))]
    internal static class SteamMatchmaking_CreateLobbyAsync
    {
        private static void Prefix(ref int maxMembers)
        {
            int incoming = maxMembers;
            int cap = TooManyCooksMod.MaxPlayers.Value;

            if (TooManyCooksMod.ForceMaxOnHost.Value)
                maxMembers = cap;
            else if (maxMembers > cap)
                maxMembers = cap;

            TooManyCooksMod.Log.Msg($"[Steam] CreateLobbyAsync: maxMembers {incoming} -> {maxMembers}");
        }
    }

    /// <summary>Raises the member limit if it is set below MaxPlayers.</summary>
    [HarmonyPatch(typeof(Il2CppSteamworks.Data.Lobby), nameof(Il2CppSteamworks.Data.Lobby.MaxMembers), MethodType.Setter)]
    internal static class Lobby_set_MaxMembers
    {
        private static void Prefix(ref int value)
        {
            int incoming = value;
            if (value < TooManyCooksMod.MaxPlayers.Value)
                value = TooManyCooksMod.MaxPlayers.Value;
            TooManyCooksMod.Log.Msg($"[Steam] Lobby.MaxMembers set: {incoming} -> {value}");
        }
    }

    /// <summary>
    /// Writes _playerCount from the dropdown's current option. Assigning TMP_Dropdown.value does
    /// not propagate to _playerCount.
    /// </summary>
    [HarmonyPatch(typeof(CreateRoomHandler), nameof(CreateRoomHandler.CreateRoom))]
    internal static class CreateRoomHandler_CreateRoom
    {
        private static void Prefix(CreateRoomHandler __instance)
        {
            int before = __instance._playerCount;
            int selected = ReadDropdownSelection(__instance);

            if (selected > 0 && selected != before)
            {
                __instance._playerCount = selected;
                TooManyCooksMod.Log.Msg($"[Flow] CreateRoom(): _playerCount {before} -> {selected} (from dropdown)");
            }
            else
            {
                TooManyCooksMod.Log.Msg($"[Flow] CreateRoom(): _playerCount={before} (dropdown read gave {selected})");
            }
        }

        private static int ReadDropdownSelection(CreateRoomHandler h)
        {
            try
            {
                var menu = h._createRoomMenu;
                if (menu == null) return -1;
                var dd = menu.GetComponentInChildren<TMP_Dropdown>(true);
                if (dd == null) return -1;
                if (dd.value < 0 || dd.value >= dd.options.Count) return -1;
                return int.TryParse(dd.options[dd.value].text, out int n) ? n : -1;
            }
            catch (System.Exception e)
            {
                TooManyCooksMod.Log.Warning($"[Flow] dropdown read failed: {e.Message}");
                return -1;
            }
        }
    }

    /// <summary>Raises _maxMembers on the Invite Friends host path, which always passes 4.</summary>
    [HarmonyPatch(typeof(MainMenuNetworkManager), nameof(MainMenuNetworkManager.StartHost))]
    internal static class MainMenuNetworkManager_StartHost
    {
        private static void Prefix(ref int _maxMembers, string roomName, bool isInviteOnly)
        {
            int incoming = _maxMembers;
            if (TooManyCooksMod.ForceMaxOnHost.Value)
                _maxMembers = TooManyCooksMod.MaxPlayers.Value;
            TooManyCooksMod.Log.Msg(
                $"[Flow] StartHost: maxMembers incoming={incoming} using={_maxMembers} room='{roomName}' inviteOnly={isInviteOnly}");
        }
    }

    /// <summary>Logs the lobby size Steam actually created.</summary>
    [HarmonyPatch(typeof(MainMenuNetworkManager), nameof(MainMenuNetworkManager.SteamMatchmaking_OnLobbyCreated))]
    internal static class MainMenuNetworkManager_OnLobbyCreated
    {
        private static void Postfix(Il2CppSteamworks.Data.Lobby _lobby)
            => TooManyCooksMod.Log.Msg($"[Flow] OnLobbyCreated: lobby.MaxMembers={_lobby.MaxMembers} id={_lobby.Id}");
    }

    /// <summary>Logs entry to the Invite Friends host path.</summary>
    [HarmonyPatch(typeof(MainMenuLobbyManager), nameof(MainMenuLobbyManager.InviteFriendsAndCreateLobby))]
    internal static class MainMenuLobbyManager_InviteFriendsAndCreateLobby
    {
        private static void Prefix() => TooManyCooksMod.Log.Msg("[Flow] InviteFriendsAndCreateLobby()");
    }

    /// <summary>
    /// Grows LobbyUI.LobbySlots and LobbyUI.Characters to MaxPlayers by cloning the last entry.
    /// These are the only size-bound structures in the lobby; GameLobbyManager's own state is
    /// keyed by client id.
    /// </summary>
    [HarmonyPatch(typeof(GameLobbyManager), nameof(GameLobbyManager.Awake))]
    internal static class GameLobbyManager_Awake
    {
        private static void Postfix(GameLobbyManager __instance)
        {
            Expand(__instance._mainMenuLobbyUI, "mainMenuLobbyUI");
            Expand(__instance._roomLobbyUI, "roomLobbyUI");
        }

        private static void Expand(GameLobbyManager.LobbyUI ui, string label)
        {
            if (ui == null)
            {
                TooManyCooksMod.Log.Msg($"[Slots] {label}: null, skipping");
                return;
            }

            int want = TooManyCooksMod.MaxPlayers.Value;
            ExpandSlots(ui, label, want);
            ExpandCharacters(ui, label, want);
        }

        private static void ExpandSlots(GameLobbyManager.LobbyUI ui, string label, int want)
        {
            var slots = ui.LobbySlots;
            if (slots == null || slots.Length == 0)
            {
                TooManyCooksMod.Log.Warning($"[Slots] {label}: LobbySlots null/empty, cannot expand");
                return;
            }
            if (slots.Length >= want)
            {
                TooManyCooksMod.Log.Msg($"[Slots] {label}: LobbySlots already {slots.Length}, no change");
                return;
            }

            int before = slots.Length;
            var template = slots[before - 1];
            var kept = new List<LobbySlot>();
            for (int i = 0; i < before; i++) kept.Add(slots[i]);

            try
            {
                for (int i = before; i < want; i++)
                {
                    var clone = Object.Instantiate(template.gameObject, template.transform.parent);
                    clone.name = $"LobbySlot_TMC_{i}";
                    var ls = clone.GetComponent<LobbySlot>();
                    if (ls == null)
                    {
                        TooManyCooksMod.Log.Warning($"[Slots] {label}: clone {i} has no LobbySlot component");
                        Object.Destroy(clone);
                        break;
                    }
                    ls.ResetSlot();
                    kept.Add(ls);
                }

                var arr = new Il2CppReferenceArray<LobbySlot>(kept.Count);
                for (int i = 0; i < kept.Count; i++) arr[i] = kept[i];
                ui.LobbySlots = arr;

                TooManyCooksMod.Log.Msg($"[Slots] {label}: LobbySlots {before} -> {ui.LobbySlots.Length}");
            }
            catch (System.Exception e)
            {
                TooManyCooksMod.Log.Error($"[Slots] {label}: LobbySlots expand failed: {e}");
            }
        }

        private static void ExpandCharacters(GameLobbyManager.LobbyUI ui, string label, int want)
        {
            var chars = ui.Characters;
            if (chars == null || chars.Length == 0)
            {
                TooManyCooksMod.Log.Msg($"[Slots] {label}: Characters null/empty, skipping");
                return;
            }
            if (chars.Length >= want)
            {
                TooManyCooksMod.Log.Msg($"[Slots] {label}: Characters already {chars.Length}, no change");
                return;
            }

            int before = chars.Length;
            var kept = new List<CharacterAppearanceGroup>();
            for (int i = 0; i < before; i++) kept.Add(chars[i]);

            // Clones are offset by the existing spacing so they line up with the row.
            Vector3 step = Vector3.zero;
            if (before >= 2)
                step = chars[before - 1].transform.position - chars[before - 2].transform.position;

            try
            {
                var template = chars[before - 1];
                for (int i = before; i < want; i++)
                {
                    var clone = Object.Instantiate(template.gameObject, template.transform.parent);
                    clone.name = $"LobbyCharacter_TMC_{i}";
                    clone.transform.position = template.transform.position + step * (i - before + 1);
                    var cg = clone.GetComponent<CharacterAppearanceGroup>();
                    if (cg == null)
                    {
                        TooManyCooksMod.Log.Warning($"[Slots] {label}: char clone {i} missing CharacterAppearanceGroup");
                        Object.Destroy(clone);
                        break;
                    }
                    kept.Add(cg);
                }

                var arr = new Il2CppReferenceArray<CharacterAppearanceGroup>(kept.Count);
                for (int i = 0; i < kept.Count; i++) arr[i] = kept[i];
                ui.Characters = arr;

                TooManyCooksMod.Log.Msg($"[Slots] {label}: Characters {before} -> {ui.Characters.Length} (step={step})");
            }
            catch (System.Exception e)
            {
                TooManyCooksMod.Log.Error($"[Slots] {label}: Characters expand failed: {e}");
            }
        }
    }
}
