# TooManyCooks

Raises the 4-player lobby cap in **Kebab Chefs! - Restaurant Simulator** to 6.

> See [Known unknowns](#known-unknowns) before you rely on this.

## Install

1. Install **[MelonLoader](https://github.com/LavaGang/MelonLoader/releases) v0.7.3 or newer** into
   the game folder. Easiest is `MelonLoader.Installer.exe` just point it at
   `Kebab Chefs! - Restaurant Simulator.exe`.
2. Run the game once. MelonLoader generates its assemblies on first launch; this takes a minute and
   may look like it's hanging. Let it finish.
3. Drop [`TooManyCooks.dll`](dist/TooManyCooks.dll) into the `Mods` folder next to the game exe(build instructions below if you prefer).
4. Launch. You should see `[TooManyCooks] Ready. MaxPlayers=6` in the MelonLoader console.

**BepInEx will not work.** The game is Unity 6.3 / IL2CPP metadata v39, and BepInEx's bundled
Cpp2IL only supports metadata 23–31 ([BepInEx#1266](https://github.com/BepInEx/BepInEx/issues/1266)).
It fails before any mod loads. Use MelonLoader.

### Does everyone need it?

**Probably idk, install it on every machine.** The host opens the bigger lobby, but an unmodded
client only builds 4 lobby slots, so players 5 and 6 may be invisible to them. Untested.

## Config

`UserData/MelonPreferences.cfg`, created on first run:

```ini
[TooManyCooks]
# Lobby size cap. Vanilla is 4.
MaxPlayers = 6
# Force the hosted lobby to MaxPlayers even if the create-room UI passed a smaller number.
ForceMaxOnHost = true
```

`ForceMaxOnHost = true` means every lobby you host opens at `MaxPlayers`, ignoring the dropdown.
Set it to `false` if you want the **Create Room** dropdown to decide. Note the **Invite Friends**
path has no dropdown and hardcodes 4, so with `false` that path stays a 4-player lobby.

Higher than 6 is untested and increasingly likely to break things, but feel free to fork and do your thing.

## Known unknowns

- **Old Markets Bug.** The mod this replaces reported *"all markets are closed when player size is bigger
  than 4"*. That was against the old Photon build, the bug may be gone, or reappear differently. 
 **It has not been reproduced or ruled out here.** If you experience bugs or if your markets
  close with 5+ players, please open an issue with your `MelonLoader/Latest.log`.
- Whether 5+ players can actually connect and complete a day.
- Whether clients need the mod (see above).
- I'll update this section when I figure this stuff out myself through playing.

Press **F9** in a lobby to dump live slot/player state to `MelonLoader/Latest.log`. Attach that to
any issue.

## How it works

Three separate things cap the player count and all three needed changing:

| Cap | Fix |
|---|---|
| Steam lobby size | Patch `SteamMatchmaking.CreateLobbyAsync(maxMembers)` |
| Create-room dropdown | Rebuild the `TMP_Dropdown` options (prefab-authored `2/3/4`) and write `_playerCount` |
| Lobby slot UI | Clone `LobbyUI.LobbySlots` / `.Characters` entries and activate them |

The game has **two independent host paths**, which is the main trap:

- **Multiplayer → Create Room** → `CreateRoomHandler.CreateRoom()` → `CreateLobbyAsync`
- **Invite Friends (no room)** → `MainMenuLobbyManager.InviteFriendsAndCreateLobby()` →
  `MainMenuNetworkManager.StartHost()` → `CreateLobbyAsync`

Path 1 never touches `StartHost`. Patching either game-side method alone silently misses a path, 
so the size is forced at `CreateLobbyAsync` where both converge.

The good news is the game's data layer is already player-count agnostic: `GameLobbyManager` and
`IngameNetworkManager` key everything on `Dictionary`/`List`/`HashSet` by client id, and the game
sets no Netcode connection-approval cap. Only the UI hardcodes 4.

## Building

Requires the .NET SDK. You must have MelonLoader installed and have run the game once, so the
`Il2CppAssemblies` exist to compile against.

```sh
cd src
dotnet build -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Kebab Chefs! - Restaurant Simulator"
```

Output lands in `src/bin/Release/net6.0/TooManyCooks.dll`.

## Credits

Successor to [requizm/KebabMultiplayerLimit](https://github.com/requizm/KebabMultiplayerLimit),
which did this for the Photon-era build. None of its code survives as the game replaced its entire
networking stack with Unity Netcode for GameObjects + Facepunch Steamworks.

## License

MIT - see [LICENSE](LICENSE).
