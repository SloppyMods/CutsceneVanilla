namespace CutsceneVanilla.Core;

/// <summary>
/// Turns chosen Penumbra mods off while a cutscene plays and back on afterwards.
///
/// Disabling is immediate on the rising edge (the toggle must beat the cutscene actors' animation loads).
/// Restoring waits <see cref="RestoreDelay"/> after the last cutscene flag, because flags flicker between shots.
/// Only Penumbra temporary settings under our key are used, so nothing is written to saved settings.
/// </summary>
public sealed class CutsceneController
{
    private readonly IPenumbraGateway _penumbra;
    private readonly Action<string> _log;
    private readonly HashSet<(Guid Collection, string Mod)> _applied = new();
    private DateTime _lastFlagSeen;
    private bool _active;
    /// <summary>The last Apply could not start (no Penumbra / no player collection yet); retry while the cutscene lasts.</summary>
    private bool _pending;
    private IReadOnlySet<string> _lastDesired = new HashSet<string>();

    public CutsceneController(IPenumbraGateway penumbra, Action<string>? log = null)
    {
        _penumbra = penumbra;
        _log = log ?? (_ => { });
    }

    public TimeSpan RestoreDelay { get; set; } = TimeSpan.FromMilliseconds(1500);

    /// <summary>True from the cutscene's first flag until the restore has run.</summary>
    public bool IsActive => _active;

    public IReadOnlyCollection<(Guid Collection, string Mod)> Applied => _applied;

    /// <summary>Call every frame. <paramref name="cutsceneFlag"/> is true while any watched condition flag is set.</summary>
    public void Update(DateTime now, bool cutsceneFlag, bool enabled, IReadOnlySet<string> selectedMods)
    {
        if (!enabled)
        {
            Restore();
            return;
        }

        if (cutsceneFlag)
        {
            _lastFlagSeen = now;
            if (!_active)
            {
                _active = true;
                Apply(selectedMods);
            }
            else if (!selectedMods.SetEquals(_lastDesired))
            {
                Reconcile(selectedMods);
            }
            else if (_pending)
            {
                Apply(selectedMods);
            }
        }
        else if (_active && now - _lastFlagSeen >= RestoreDelay)
        {
            Restore();
        }
    }

    /// <summary>Puts everything back immediately. Safe to call at any time, any number of times.</summary>
    public void Restore()
    {
        _active = false;
        _pending = false;
        _lastDesired = new HashSet<string>();
        if (_applied.Count == 0) return;

        foreach (var collection in _applied.Select(a => a.Collection).Distinct().ToList())
        {
            var result = _penumbra.RemoveOurTemporarySettings(collection);
            if (result != GatewayResult.Success)
                _log($"Restore in collection {collection} returned {result}.");
        }
        _applied.Clear();
    }

    /// <summary>Penumbra went away: its temporary settings are gone with it, so only forget our state.</summary>
    public void OnPenumbraDisposed()
    {
        _applied.Clear();
        _active = false;
        _pending = false;
        _lastDesired = new HashSet<string>();
    }

    /// <summary>
    /// Clears leftovers of a previous run of this plugin (e.g. it was reloaded mid-cutscene while Penumbra kept running).
    /// Call on plugin load and when Penumbra reports it is initialized.
    /// </summary>
    public void SweepStale()
    {
        if (!_penumbra.IsAvailable) return;
        foreach (var collection in _penumbra.GetAllCollections())
            _penumbra.RemoveOurTemporarySettings(collection);
    }

    private void Apply(IReadOnlySet<string> desired)
    {
        _lastDesired = new HashSet<string>(desired);
        _pending = false;
        if (desired.Count == 0) return;

        var collection = _penumbra.IsAvailable ? _penumbra.GetPlayerCollection() : null;
        if (collection is null)
        {
            _pending = true; // e.g. zone-in: try again next frame
            return;
        }

        foreach (var mod in desired)
            DisableOne(collection.Value, mod);
    }

    private void DisableOne(Guid collection, string mod)
    {
        if (_applied.Contains((collection, mod))) return;

        var current = _penumbra.GetCurrentSettings(collection, mod);
        if (current is null)
        {
            _log($"Mod '{mod}' not found or has no settings; skipped.");
            return;
        }
        if (!current.Enabled) return; // already off, leave it alone

        var result = _penumbra.DisableTemporarily(collection, mod, current);
        if (result == GatewayResult.Success)
            _applied.Add((collection, mod));
        else
            _log($"Could not disable '{mod}': {result}.");
    }

    /// <summary>The selection changed mid-cutscene: undo ours, then re-apply the new selection.</summary>
    private void Reconcile(IReadOnlySet<string> desired)
    {
        var collections = _applied.Select(a => a.Collection).Distinct().ToList();
        foreach (var collection in collections)
            _penumbra.RemoveOurTemporarySettings(collection);
        _applied.Clear();
        Apply(desired);
    }
}
