using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

namespace CutsceneVanilla.Plugin;

public sealed class ConfigWindow : Window
{
    private static readonly ConditionFlag[] SelectableFlags =
    {
        ConditionFlag.WatchingCutscene,
        ConditionFlag.WatchingCutscene78,
        ConditionFlag.OccupiedInCutSceneEvent,
        ConditionFlag.OccupiedInEvent,
        ConditionFlag.OccupiedInQuestEvent,
    };

    private readonly PluginConfig _config;
    private readonly PenumbraGateway _penumbra;
    private readonly ICondition _condition;
    private readonly Action _save;
    private IReadOnlyDictionary<string, string> _mods = new Dictionary<string, string>();
    private string _filter = string.Empty;

    public bool IsCutsceneActive { get; set; }

    public ConfigWindow(PluginConfig config, PenumbraGateway penumbra, ICondition condition, Action save)
        : base("Cutscene Vanilla###CutsceneVanillaConfig")
    {
        _config = config;
        _penumbra = penumbra;
        _condition = condition;
        _save = save;
        Size = new System.Numerics.Vector2(520, 560);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void OnOpen() => _mods = _penumbra.GetModList();

    public override void Draw()
    {
        var enabled = _config.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled))
        {
            _config.Enabled = enabled;
            _save();
        }
        ImGui.SameLine();
        ImGui.TextDisabled(IsCutsceneActive ? "(cutscene: selected mods are off)" : "(idle)");

        ImGui.TextWrapped("Checked mods are switched off (Penumbra temporary settings, nothing saved) while a cutscene plays, " +
                          "then switched back on.");
        ImGui.Separator();

        DrawModList();
        ImGui.Separator();
        DrawFlags();
    }

    private void DrawModList()
    {
        if (!_penumbra.IsAvailable)
            ImGui.TextColored(new System.Numerics.Vector4(1f, 0.6f, 0.3f, 1f), "Penumbra is not available.");

        if (ImGui.Button("Reload mod list")) _mods = _penumbra.GetModList();
        ImGui.SameLine();
        ImGui.SetNextItemWidth(220);
        ImGui.InputTextWithHint("##filter", "Filter mods", ref _filter, 128);

        ImGui.BeginChild("##mods", new System.Numerics.Vector2(0, 280), true);
        foreach (var (dir, name) in _mods.OrderBy(m => m.Value, StringComparer.OrdinalIgnoreCase))
        {
            if (_filter.Length > 0 && !name.Contains(_filter, StringComparison.OrdinalIgnoreCase)
                                    && !dir.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                continue;

            var on = _config.DisabledMods.Contains(dir);
            if (ImGui.Checkbox($"{name}##{dir}", ref on))
            {
                if (on) _config.DisabledMods.Add(dir); else _config.DisabledMods.Remove(dir);
                _save();
            }
        }
        ImGui.EndChild();
        ImGui.TextDisabled($"{_config.DisabledMods.Count} mod(s) selected");
    }

    private void DrawFlags()
    {
        ImGui.TextUnformatted("What counts as a cutscene");
        foreach (var flag in SelectableFlags)
        {
            var watched = _config.WatchedFlags.Contains(flag);
            if (ImGui.Checkbox($"{flag}##flag", ref watched))
            {
                if (watched) _config.WatchedFlags.Add(flag); else _config.WatchedFlags.Remove(flag);
                _save();
            }
            ImGui.SameLine();
            ImGui.TextDisabled(_condition[flag] ? "[set now]" : "");
        }
        ImGui.TextWrapped("OccupiedInEvent and OccupiedInQuestEvent also fire on ordinary NPC talk; leave them off unless a cutscene is missed.");

        var delay = _config.RestoreDelayMs;
        ImGui.SetNextItemWidth(120);
        if (ImGui.DragInt("Restore delay (ms)", ref delay, 10, 0, 10000))
        {
            _config.RestoreDelayMs = delay;
            _save();
        }
    }
}
