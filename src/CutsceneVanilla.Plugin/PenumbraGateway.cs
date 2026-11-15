using CutsceneVanilla.Core;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Penumbra.Api.Enums;
using Penumbra.Api.IpcSubscribers;

namespace CutsceneVanilla.Plugin;

/// <summary>Penumbra access over IPC only. Writes temporary mod settings under a fixed negative key and nothing else.</summary>
public sealed class PenumbraGateway : IPenumbraGateway, IDisposable
{
    /// <summary>Negative = identification lock only: the user can still edit, and we can remove just our own settings.</summary>
    private const int Key = -0x43564E4C; // "CVNL"
    private const string Source = "Cutscene Vanilla";

    private readonly IPluginLog _log;
    private readonly GetModList _getModList;
    private readonly GetCollectionForObject _getCollectionForObject;
    private readonly GetCollections _getCollections;
    private readonly GetCurrentModSettingsWithTemp _getSettings;
    private readonly SetTemporaryModSettings _setTemp;
    private readonly RemoveAllTemporaryModSettings _removeAll;
    private readonly GetEnabledState _getEnabled;
    private readonly IDisposable _initialized;
    private readonly IDisposable _disposed;

    public PenumbraGateway(IDalamudPluginInterface pi, IPluginLog log, Action onInitialized, Action onDisposed)
    {
        _log = log;
        _getModList = new GetModList(pi);
        _getCollectionForObject = new GetCollectionForObject(pi);
        _getCollections = new GetCollections(pi);
        _getSettings = new GetCurrentModSettingsWithTemp(pi);
        _setTemp = new SetTemporaryModSettings(pi);
        _removeAll = new RemoveAllTemporaryModSettings(pi);
        _getEnabled = new GetEnabledState(pi);
        _initialized = Initialized.Subscriber(pi, onInitialized);
        _disposed = Disposed.Subscriber(pi, onDisposed);
    }

    public bool IsAvailable
    {
        get
        {
            try { return _getEnabled.Invoke(); }
            catch { return false; }
        }
    }

    /// <summary>Directory name -> display name of every installed mod; empty if Penumbra is not there.</summary>
    public IReadOnlyDictionary<string, string> GetModList()
    {
        try { return _getModList.Invoke(); }
        catch { return new Dictionary<string, string>(); }
    }

    public Guid? GetPlayerCollection()
    {
        try
        {
            var (valid, _, effective) = _getCollectionForObject.Invoke(0);
            return valid ? effective.Id : null;
        }
        catch (Exception e) { _log.Warning(e, "GetCollectionForObject failed"); return null; }
    }

    public IReadOnlyList<Guid> GetAllCollections()
    {
        try { return _getCollections.Invoke().Keys.ToList(); }
        catch (Exception e) { _log.Warning(e, "GetCollections failed"); return Array.Empty<Guid>(); }
    }

    public ModSettings? GetCurrentSettings(Guid collection, string modDirectory)
    {
        try
        {
            var (ec, settings) = _getSettings.Invoke(collection, modDirectory, string.Empty, false, false, Key);
            if (ec != PenumbraApiEc.Success || settings is null) return null;
            var (enabled, priority, groups, _, _) = settings.Value;
            var options = groups.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value.ToList());
            return new ModSettings(enabled, priority, options);
        }
        catch (Exception e) { _log.Warning(e, "GetCurrentModSettings failed for {Mod}", modDirectory); return null; }
    }

    public GatewayResult DisableTemporarily(Guid collection, string modDirectory, ModSettings current)
    {
        try
        {
            var ec = _setTemp.Invoke(collection, modDirectory, false, false, current.Priority, current.Options,
                Source, Key, string.Empty);
            return ec switch
            {
                PenumbraApiEc.Success => GatewayResult.Success,
                PenumbraApiEc.TemporarySettingDisallowed => GatewayResult.Refused,
                _ => GatewayResult.Failed,
            };
        }
        catch (Exception e) { _log.Warning(e, "SetTemporaryModSettings failed for {Mod}", modDirectory); return GatewayResult.Failed; }
    }

    public GatewayResult RemoveOurTemporarySettings(Guid collection)
    {
        try
        {
            var ec = _removeAll.Invoke(collection, Key);
            return ec is PenumbraApiEc.Success or PenumbraApiEc.NothingChanged
                ? GatewayResult.Success
                : GatewayResult.Failed;
        }
        catch (Exception e) { _log.Warning(e, "RemoveAllTemporaryModSettings failed"); return GatewayResult.Failed; }
    }

    public void Dispose()
    {
        _initialized.Dispose();
        _disposed.Dispose();
    }
}
