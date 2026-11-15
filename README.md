# CutsceneVanilla

Dalamud plugin: turns chosen Penumbra mods off while a cutscene plays, then back on, so cutscenes show the normal emotes.
Settings: `/cutscenevanilla` (or the plugin's settings button). Findings and sources: [FEASIBILITY.md](FEASIBILITY.md).

How it works: cutscene condition flag rises -> each checked mod gets a Penumbra *temporary* setting "disabled" (same priority and options,
key = fixed negative lock, source "Cutscene Vanilla"). 1.5 s after the flags clear, our temporary settings are removed.
Saved Penumbra settings are never written. Penumbra is reached only through its IPC.

Crash safety: temporary settings live in Penumbra's memory. A game crash drops them. If only this plugin reloads, it clears its own
leftovers (all collections, our key) on load and when Penumbra reports Initialized. Restore also runs on logout and plugin unload.

## Build / test
```bash
export DALAMUD_HOME=$HOME/.local/share/dev.goats.xivlauncher/dalamud/Hooks/dev
~/.dotnet/dotnet build src/CutsceneVanilla.Plugin # output: ../_shared/Build/CutsceneVanilla/
~/.dotnet/dotnet test tests/CutsceneVanilla.Tests
```
`lib/Penumbra.Api.dll` + `Luna.dll` are copied from Penumbra 1.7.2.1 (typed IPC subscribers, shipped with the plugin like Glamourer does).
Refresh them if Penumbra's IPC version changes.

## In-game test steps (ALL NOT TESTED — you run these)
1. Load the plugin (add as a dev plugin in the Wine path form `Z:\home\SloppyMods\Projects\ffxiv-tools\_shared\Build\CutsceneVanilla\CutsceneVanilla.dll`; if it fails to load, check `/xllog` for type-load errors). Open `/cutscenevanilla`, tick one dance mod you know replaces an emote. Leave Enabled on.
2. Outside cutscenes, do the emote: you should see the mod's dance. (Confirms the mod works and the plugin is idle.)
3. Enter a cutscene that plays that emote on your character. Expected: the vanilla emote. If the mod's dance still shows, note whether the actor spawned before the toggle (race) or the animation was cached.
4. During the cutscene, open Penumbra's mod tab: the mod should show a temporary-settings marker with source "Cutscene Vanilla". After the cutscene (about 2 s) the marker is gone and the dance works again.
5. Talk to an ordinary NPC (no cutscene): the mod must stay on, no flicker. If it flips, untick the flag that was `[set now]` in the settings window and report which one.
6. Restart the game, check the mod is still enabled in Penumbra (saved settings untouched).
7. Plugin reload mid-cutscene (disable/enable the plugin): mods come back at once.
8. Toggle Enabled off mid-cutscene: mods come back at once. Untick the mod mid-cutscene: only that mod comes back.
9. Log out during a cutscene-like state or unload the plugin: mods are enabled afterwards.
10. Another plugin that locks a mod's temporary settings (Glamourer-style): that mod is skipped, a line appears in `/xllog`, nothing breaks.
