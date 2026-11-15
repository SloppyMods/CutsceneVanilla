using Dalamud.Configuration;
using Dalamud.Game.ClientState.Conditions;
using Newtonsoft.Json;

namespace CutsceneVanilla.Plugin;

public sealed class PluginConfig : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;

    /// <summary>Penumbra mod directory names to switch off during cutscenes.</summary>
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public HashSet<string> DisabledMods { get; set; } = new();

    /// <summary>Condition flags that count as "a cutscene is playing". NPC talk flags are deliberately not in the default set.</summary>
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public HashSet<ConditionFlag> WatchedFlags { get; set; } = new()
    {
        ConditionFlag.WatchingCutscene,
        ConditionFlag.WatchingCutscene78,
        ConditionFlag.OccupiedInCutSceneEvent,
    };

    public int RestoreDelayMs { get; set; } = 1500;
}
