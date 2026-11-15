using CutsceneVanilla.Core;
using Xunit;

namespace CutsceneVanilla.Tests;

internal sealed class FakePenumbra : IPenumbraGateway
{
    public bool IsAvailable { get; set; } = true;
    public Guid Player { get; set; } = Guid.NewGuid();
    public List<Guid> Collections { get; } = new();
    public Dictionary<string, ModSettings> Mods { get; } = new();
    public HashSet<string> Locked { get; } = new();
    /// <summary>Mods currently disabled by our temporary settings, keyed (collection, mod).</summary>
    public HashSet<(Guid, string)> TempDisabled { get; } = new();
    public List<string> Calls { get; } = new();

    public bool NoCollection { get; set; }
    public Guid? GetPlayerCollection() => NoCollection ? null : Player;
    public IReadOnlyList<Guid> GetAllCollections() => Collections;
    public ModSettings? GetCurrentSettings(Guid c, string mod) => Mods.GetValueOrDefault(mod);

    public GatewayResult DisableTemporarily(Guid c, string mod, ModSettings current)
    {
        Calls.Add($"disable:{mod}");
        if (Locked.Contains(mod)) return GatewayResult.Refused;
        TempDisabled.Add((c, mod));
        return GatewayResult.Success;
    }

    public GatewayResult RemoveOurTemporarySettings(Guid c)
    {
        Calls.Add("removeAll");
        TempDisabled.RemoveWhere(t => t.Item1 == c);
        return GatewayResult.Success;
    }

    public static ModSettings On() => new(true, 0, new Dictionary<string, IReadOnlyList<string>>());
    public static ModSettings Off() => new(false, 0, new Dictionary<string, IReadOnlyList<string>>());
}

public class CutsceneControllerTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0);
    private static IReadOnlySet<string> Set(params string[] m) => new HashSet<string>(m);

    private static (CutsceneController, FakePenumbra, List<string>) Make(params string[] mods)
    {
        var fake = new FakePenumbra();
        foreach (var m in mods) fake.Mods[m] = FakePenumbra.On();
        var log = new List<string>();
        return (new CutsceneController(fake, log.Add), fake, log);
    }

    [Fact]
    public void Disables_selected_mods_immediately_when_cutscene_starts()
    {
        var (c, fake, _) = Make("dance", "other");
        c.Update(T0, true, true, Set("dance"));
        Assert.Contains((fake.Player, "dance"), fake.TempDisabled);
        Assert.DoesNotContain((fake.Player, "other"), fake.TempDisabled);
        Assert.True(c.IsActive);
    }

    [Fact]
    public void Does_nothing_when_switch_is_off()
    {
        var (c, fake, _) = Make("dance");
        c.Update(T0, true, false, Set("dance"));
        Assert.Empty(fake.TempDisabled);
        Assert.False(c.IsActive);
    }

    [Fact]
    public void Keeps_mods_off_during_short_flag_flicker()
    {
        var (c, fake, _) = Make("dance");
        c.Update(T0, true, true, Set("dance"));
        c.Update(T0.AddMilliseconds(500), false, true, Set("dance"));
        Assert.Contains((fake.Player, "dance"), fake.TempDisabled);
        c.Update(T0.AddMilliseconds(700), true, true, Set("dance"));
        c.Update(T0.AddMilliseconds(1400), false, true, Set("dance"));
        Assert.Contains((fake.Player, "dance"), fake.TempDisabled);
    }

    [Fact]
    public void Restores_after_delay_once_flags_stay_clear()
    {
        var (c, fake, _) = Make("dance");
        c.Update(T0, true, true, Set("dance"));
        c.Update(T0.AddMilliseconds(1499), false, true, Set("dance"));
        Assert.NotEmpty(fake.TempDisabled);
        c.Update(T0.AddMilliseconds(1500), false, true, Set("dance"));
        Assert.Empty(fake.TempDisabled);
        Assert.False(c.IsActive);
        Assert.Empty(c.Applied);
    }

    [Fact]
    public void Skips_mods_that_are_already_disabled()
    {
        var (c, fake, _) = Make("dance");
        fake.Mods["dance"] = FakePenumbra.Off();
        c.Update(T0, true, true, Set("dance"));
        Assert.DoesNotContain("disable:dance", fake.Calls);
        Assert.Empty(c.Applied);
    }

    [Fact]
    public void Skips_and_logs_mods_locked_by_another_plugin()
    {
        var (c, fake, log) = Make("dance", "other");
        fake.Locked.Add("dance");
        c.Update(T0, true, true, Set("dance", "other"));
        Assert.DoesNotContain((fake.Player, "dance"), fake.TempDisabled);
        Assert.Contains((fake.Player, "other"), fake.TempDisabled);
        Assert.Contains(log, l => l.Contains("dance"));
    }

    [Fact]
    public void Skips_missing_mods()
    {
        var (c, fake, log) = Make();
        c.Update(T0, true, true, Set("ghost"));
        Assert.Empty(fake.TempDisabled);
        Assert.Contains(log, l => l.Contains("ghost"));
    }

    [Fact]
    public void Turning_switch_off_mid_cutscene_restores_at_once()
    {
        var (c, fake, _) = Make("dance");
        c.Update(T0, true, true, Set("dance"));
        c.Update(T0.AddMilliseconds(100), true, false, Set("dance"));
        Assert.Empty(fake.TempDisabled);
        Assert.False(c.IsActive);
    }

    [Fact]
    public void Unchecking_a_mod_mid_cutscene_restores_only_that_mod()
    {
        var (c, fake, _) = Make("a", "b");
        c.Update(T0, true, true, Set("a", "b"));
        c.Update(T0.AddMilliseconds(100), true, true, Set("a"));
        Assert.Contains((fake.Player, "a"), fake.TempDisabled);
        Assert.DoesNotContain((fake.Player, "b"), fake.TempDisabled);
    }

    [Fact]
    public void Checking_a_mod_mid_cutscene_disables_it()
    {
        var (c, fake, _) = Make("a", "b");
        c.Update(T0, true, true, Set("a"));
        c.Update(T0.AddMilliseconds(100), true, true, Set("a", "b"));
        Assert.Contains((fake.Player, "b"), fake.TempDisabled);
    }

    [Fact]
    public void Restore_is_idempotent_and_logout_unload_use_it()
    {
        var (c, fake, _) = Make("dance");
        c.Update(T0, true, true, Set("dance"));
        c.Restore();
        var calls = fake.Calls.Count;
        c.Restore();
        c.Restore();
        Assert.Empty(fake.TempDisabled);
        Assert.Equal(calls, fake.Calls.Count); // nothing left to undo, no extra IPC
    }

    [Fact]
    public void Restores_the_collection_it_touched_even_if_player_collection_changed()
    {
        var (c, fake, _) = Make("dance");
        var original = fake.Player;
        c.Update(T0, true, true, Set("dance"));
        fake.Player = Guid.NewGuid(); // zone change swapped the active collection
        c.Update(T0.AddSeconds(5), false, true, Set("dance"));
        Assert.DoesNotContain((original, "dance"), fake.TempDisabled);
    }

    [Fact]
    public void Penumbra_disposed_clears_state_without_calling_it()
    {
        var (c, fake, _) = Make("dance");
        c.Update(T0, true, true, Set("dance"));
        var calls = fake.Calls.Count;
        c.OnPenumbraDisposed();
        Assert.Empty(c.Applied);
        Assert.False(c.IsActive);
        Assert.Equal(calls, fake.Calls.Count);
    }

    [Fact]
    public void Does_nothing_while_penumbra_is_unavailable()
    {
        var (c, fake, _) = Make("dance");
        fake.IsAvailable = false;
        c.Update(T0, true, true, Set("dance"));
        Assert.Empty(fake.Calls);
    }

    [Fact]
    public void SweepStale_clears_our_settings_in_every_collection()
    {
        var (c, fake, _) = Make();
        fake.Collections.AddRange(new[] { Guid.NewGuid(), Guid.NewGuid() });
        c.SweepStale();
        Assert.Equal(2, fake.Calls.Count(x => x == "removeAll"));
    }

    [Fact]
    public void Empty_selection_still_tracks_cutscene_but_touches_nothing()
    {
        var (c, fake, _) = Make();
        c.Update(T0, true, true, Set());
        Assert.True(c.IsActive);
        Assert.Empty(fake.Calls);
    }

    [Fact]
    public void Retries_each_frame_until_player_collection_exists()
    {
        var (c, fake, _) = Make("dance");
        fake.NoCollection = true;
        c.Update(T0, true, true, Set("dance"));
        Assert.Empty(fake.TempDisabled);
        fake.NoCollection = false;
        c.Update(T0.AddMilliseconds(16), true, true, Set("dance"));
        Assert.Contains((fake.Player, "dance"), fake.TempDisabled);
    }

    [Fact]
    public void Retries_when_penumbra_becomes_available_mid_cutscene()
    {
        var (c, fake, _) = Make("dance");
        fake.IsAvailable = false;
        c.Update(T0, true, true, Set("dance"));
        fake.IsAvailable = true;
        c.Update(T0.AddMilliseconds(16), true, true, Set("dance"));
        Assert.Contains((fake.Player, "dance"), fake.TempDisabled);
    }

    [Fact]
    public void Does_not_retry_refused_mods_every_frame()
    {
        var (c, fake, _) = Make("dance");
        fake.Locked.Add("dance");
        for (var i = 0; i < 5; i++) c.Update(T0.AddMilliseconds(16 * i), true, true, Set("dance"));
        Assert.Equal(1, fake.Calls.Count(x => x == "disable:dance"));
    }
}
