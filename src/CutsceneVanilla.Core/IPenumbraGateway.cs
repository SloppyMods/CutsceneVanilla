namespace CutsceneVanilla.Core;

/// <summary>A mod's current Penumbra settings (temporary ones included), copied so a disable can keep them.</summary>
public sealed record ModSettings(
    bool Enabled,
    int Priority,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Options);

public enum GatewayResult
{
    Success,
    /// <summary>Another plugin holds a lock on this mod's temporary settings, or Penumbra refused.</summary>
    Refused,
    Failed,
}

/// <summary>
/// Everything the controller needs from Penumbra. The plugin implements it over Penumbra IPC;
/// tests use a fake. Only temporary settings are ever written, never saved ones.
/// </summary>
public interface IPenumbraGateway
{
    bool IsAvailable { get; }
    Guid? GetPlayerCollection();
    IReadOnlyList<Guid> GetAllCollections();
    ModSettings? GetCurrentSettings(Guid collection, string modDirectory);
    GatewayResult DisableTemporarily(Guid collection, string modDirectory, ModSettings current);
    /// <summary>Removes every temporary setting this plugin set in the collection (our key only).</summary>
    GatewayResult RemoveOurTemporarySettings(Guid collection);
}
