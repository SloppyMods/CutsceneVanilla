using CutsceneVanilla.Core;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace CutsceneVanilla.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/cutscenevanilla";

    private readonly IDalamudPluginInterface _pi;
    private readonly IFramework _framework;
    private readonly ICondition _condition;
    private readonly IClientState _clientState;
    private readonly ICommandManager _commands;
    private readonly IPluginLog _log;
    private readonly PluginConfig _config;
    private readonly PenumbraGateway _penumbra;
    private readonly CutsceneController _controller;
    private readonly WindowSystem _windows = new("CutsceneVanilla");
    private readonly ConfigWindow _configWindow;

    public Plugin(
        IDalamudPluginInterface pi, IFramework framework, ICondition condition,
        IClientState clientState, ICommandManager commands, IPluginLog log)
    {
        _pi = pi;
        _framework = framework;
        _condition = condition;
        _clientState = clientState;
        _commands = commands;
        _log = log;

        _config = pi.GetPluginConfig() as PluginConfig ?? new PluginConfig();
        _penumbra = new PenumbraGateway(pi, log, OnPenumbraInitialized, OnPenumbraDisposed);
        _controller = new CutsceneController(_penumbra, m => _log.Information(m))
        {
            RestoreDelay = TimeSpan.FromMilliseconds(_config.RestoreDelayMs),
        };

        _configWindow = new ConfigWindow(_config, _penumbra, _condition, () => pi.SavePluginConfig(_config));
        _windows.AddWindow(_configWindow);

        // Leftovers from a previous run of this plugin (reload while Penumbra kept running).
        _framework.RunOnFrameworkThread(_controller.SweepStale);

        _framework.Update += OnUpdate;
        _clientState.Logout += OnLogout;
        pi.UiBuilder.Draw += _windows.Draw;
        pi.UiBuilder.OpenConfigUi += _configWindow.Toggle;
        _commands.AddHandler(Command, new CommandInfo((_, _) => _configWindow.Toggle())
        {
            HelpMessage = "Open the Cutscene Vanilla settings.",
        });
    }

    private void OnUpdate(IFramework framework)
    {
        var flag = _config.WatchedFlags.Any(f => _condition[f]);
        _controller.RestoreDelay = TimeSpan.FromMilliseconds(_config.RestoreDelayMs);
        _controller.Update(DateTime.UtcNow, flag, _config.Enabled, _config.DisabledMods);
        _configWindow.IsCutsceneActive = _controller.IsActive;
    }

    private void OnLogout(int type, int code) => _controller.Restore();

    private void OnPenumbraInitialized() => _framework.RunOnFrameworkThread(_controller.SweepStale);

    private void OnPenumbraDisposed() => _framework.RunOnFrameworkThread(_controller.OnPenumbraDisposed);

    public void Dispose()
    {
        _framework.Update -= OnUpdate;
        _clientState.Logout -= OnLogout;
        _pi.UiBuilder.Draw -= _windows.Draw;
        _pi.UiBuilder.OpenConfigUi -= _configWindow.Toggle;
        _commands.RemoveHandler(Command);
        _windows.RemoveAllWindows();

        _controller.Restore();
        _penumbra.Dispose();
    }
}
