# CutsceneVanilla feasibility (checked 2026-11-02)

Sources: installed Penumbra 1.7.2.1 `Penumbra.Api.xml` + `Penumbra.Api.dll` IPC label strings
(`~/.local/share/dev.goats.xivlauncher/installedPlugins/Penumbra/1.7.2.1/`), Dalamud `Dalamud.xml`.

## 1. Penumbra IPC  — VERIFIED (docs + labels)
- No cutscene switch exists. Penumbra tracks cutscene actors (object index 200+) with a *parent index*
  (`GetCutsceneParentIndex` / `SetCutsceneParentIndex.V5`), i.e. they resolve to the player's collection. Confirms the "copy of player" model.
- `Penumbra.SetTemporaryModSettings.V5` (Guid collection, dir, inherit, enabled, priority, options, source, key, name) sets
  settings *temporarily*; docs say "temporary", shown to the user with `source`. `key` < 0 = identification lock that does not block user edits;
  `Penumbra.RemoveAllTemporaryModSettings.V5(collection, key)` removes only ours. Omitted option groups reset to defaults, so we must
  copy the mod's current options (`Penumbra.GetCurrentModSettingsWithTemp`) when disabling.
- Temporary settings are not the saved settings (`TrySetMod.V5` is the saved path, which we avoid). On crash Penumbra's in-memory
  temporary state is gone -> mods return to saved (enabled) state. Persistence-on-disk of temp settings is from the API's documented
  semantics, NOT confirmed in Penumbra source (GitHub raw fetch failed). Check in game: mod settings file unchanged.
- Other calls: `Penumbra.GetModList` (dir->name), `Penumbra.GetCollectionForObject.V5(0)` (player collection), `Penumbra.Initialized`/`Disposed` events.

## 2. Dalamud condition flags — VERIFIED BY USAGE IN LOCAL PLUGINS (not by in-game test)
`Dalamud.xml` only gives generic text and does not separate cutscene from NPC talk. Local plugins do:
- Umbra (`plugins/umbra2/Umbra.Game/src/Player/Player.cs:338`): `IsInCutscene = OccupiedInCutSceneEvent || WatchingCutscene || WatchingCutscene78`.
- VoiceMaster (`plugins/VoiceMaster-linux-fix/Plugin.cs:238`): cutscene = `WatchingCutscene || OccupiedInCutSceneEvent`; its talk handler
  also treats `OccupiedInQuestEvent` as a separate quest-talk state.
- ECommons/Wrath treat `OccupiedInEvent` as a generic "busy" flag next to the cutscene ones (it covers NPC talk), so it is NOT in our default set.
Default watched set: WatchingCutscene, WatchingCutscene78, OccupiedInCutSceneEvent. The other two are optional checkboxes; the window shows which are set live.
Timing: disable is immediate on the rising edge; restore waits a configurable 1.5 s after the last flag (flags flicker between shots).

## 3. Animation cache — NOT VERIFIABLE WITHOUT THE GAME
Penumbra resolves paths at load time, so a toggle changes what *new* loads get; resources already resident keep old data.
Cutscene actors are newly created draw objects, so their animation loads should hit the resolver afresh, provided the toggle lands
before actors spawn (race with the flag edge). No redraw is done (a redraw at cutscene start would be visible and risky); add one only if testing shows cached animations stay.
Verdict: not shown impossible, reliability unknown -> built, in-game behaviour NOT TESTED.
