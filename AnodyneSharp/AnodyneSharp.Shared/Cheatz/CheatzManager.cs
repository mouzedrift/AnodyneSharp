using AnodyneSharp.Registry;
using AnodyneSharp.Sounds;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AnodyneSharp.Cheatz;

public static class CheatzManager
{
    public const string DamageDealer = "Cheats menu";

    private static Dictionary<string, MethodInfo> cheatz;
    static CheatzManager()
    {
        cheatz = typeof(Cheatz).GetMethods().Where(m => m.IsDefined(typeof(CheatAttribute)))
            .ToDictionary(m => m.GetCustomAttribute<CheatAttribute>().cheatInput, m => m);
    }

    public static void DoCheat(string input)
    {
        if (cheatz.ContainsKey(input))
        {
            cheatz[input].Invoke(null, null);
        }
        else
        {
            Default();
        }
    }

    public static void Default()
    {
        GlobalState.CurrentHealth -= 2;
        GlobalState.DamageDealer = DamageDealer;
        SoundManager.PlaySoundEffect("sb_hurt");
    }

}
public static class Cheatz
{
    [Cheat("LRLR121212")]
    public static void ToggleFuckItMode()
    {
        GlobalState.FuckItModeOn = !GlobalState.FuckItModeOn;
    }

    [Cheat("UUDDLRLR21")]
    public static void KonamiCode()
    {
        SoundManager.PlaySoundEffect("unlock");

        GlobalState.MaxHealth = 16;
        GlobalState.CurrentHealth = GlobalState.MaxHealth;

        GlobalState.Inventory.HasBroom = true;
        GlobalState.Inventory.HasLengthen = true;
        GlobalState.Inventory.HasWiden = true;
        GlobalState.Inventory.HasTransformer = true;
        GlobalState.Inventory.CanJump = true;

        for (int i = 0; i < GlobalState.Inventory.CardStatus.Length; i++)
        {
            GlobalState.Inventory.CardStatus[i] = true;
        }

        for (int i = 0; i < 3; i++)
        {
            GlobalState.Inventory.BigKeyStatus[i] = true;
        }

        for (int i = 0; i < GlobalState.Inventory.SecretStatus.Length; i++)
        {
            GlobalState.Inventory.SecretStatus[i] = true;
        }

        GlobalState.Inventory.EquippedBroom = GlobalState.Inventory.EquippedBroom == Entities.BroomType.NONE ? Entities.BroomType.Normal : GlobalState.Inventory.EquippedBroom;
    }

    [Cheat("UDUD121212")]
    public static void UnlockNexusGates()
    {
        foreach(var map in (GameConstants.MapOrder[])Enum.GetValues(typeof(GameConstants.MapOrder)))
        {
            GlobalState.Events.ActivatedNexusPortals.Add(map.ToString());
        }

        SoundManager.PlaySoundEffect("menu_select");
        SoundManager.PlaySoundEffect("keyget");
    }

    [Cheat("URLDURLD11")]
    public static void ToggleAlwaysCellGraphics()
    {
        GlobalState.AlwaysCellGraphics = !GlobalState.AlwaysCellGraphics;
        GlobalState.ForceTextureReload = true;
    }

    [Cheat("12UULLRRDD")]
    public static void GiveKey()
    {
        GlobalState.Inventory.AddCurrentMapKey();
        SoundManager.PlaySoundEffect("keyget");
        GlobalState.RefreshKeyCount = true;
    }

    [Cheat("URDLURDLUR")]
    public static void ToggleChaosMode()
    {
        GlobalState.GameMode = GlobalState.GameMode == GameMode.Chaos ? GameMode.Normal : GameMode.Chaos;
        GlobalState.ForceTextureReload = true;

        if (GlobalState.GameMode == GameMode.Chaos)
        {
            SoundManager.PlaySoundEffect("shieldy-hit");
            SoundManager.PlaySoundEffect("small_wave");
            SoundManager.PlaySoundEffect("big_door_locked");
        }
    }

    [Cheat("ULDRULDRUL")]
    public static void ToggleExtremeChaosMode()
    {
        GlobalState.GameMode = GlobalState.GameMode == GameMode.EXTREME_CHAOS ? GameMode.Normal : GameMode.EXTREME_CHAOS;

        GlobalState.ForceTextureReload = true;

        if (GlobalState.GameMode == GameMode.EXTREME_CHAOS)
        {
            SoundManager.PlaySoundEffect("shieldy-hit");
            SoundManager.PlaySoundEffect("small_wave");
            SoundManager.PlaySoundEffect("big_door_locked");
            SoundManager.PlaySoundEffect("fall_in_hole");
        }
    }
}
