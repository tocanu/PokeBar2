using System.Collections.Generic;
using Pokebar.DesktopPet.Animation;

namespace Pokebar.DesktopPet.Entities;

public class PlayerPet : PokemonPet
{
    private readonly List<int> _party = new();

    public PlayerPet(int dex, int pokeballs = 0) : base(dex)
    {
        Level = 5;
        InitializeStats();
        if (pokeballs > 0)
            Pokeballs = pokeballs;
        _party.Add(dex);
    }

    public int Level { get; private set; }
    public int MaxHp { get; private set; }
    public int CurrentHp { get; private set; }
    public int Attack { get; private set; }
    public int Defense { get; private set; }
    public int Pokeballs { get; private set; }
    public IReadOnlyList<int> Party => _party;

    public void AddPokeballs(int amount)
    {
        if (amount > 0)
            Pokeballs += amount;
    }

    public bool TryConsumePokeball()
    {
        if (Pokeballs <= 0)
            return false;

        Pokeballs--;
        return true;
    }

    public void AddToParty(int dex)
    {
        if (!_party.Contains(dex))
            _party.Add(dex);
    }

    public bool ChangePokemon(int newDex, SpriteLoader loader, Pokebar.Core.Models.GameplayConfig config)
    {
        if (Dex == newDex)
            return false;

        Dex = newDex;
        if (!_party.Contains(newDex))
            _party.Add(newDex);

        InitializeStats();
        LoadAnimations(loader, config);
        StartWalking();
        return true;
    }

    private void InitializeStats()
    {
        MaxHp = BuildStat(20, Level, Dex);
        CurrentHp = MaxHp;
        Attack = BuildStat(8, Level, Dex);
        Defense = BuildStat(8, Level, Dex);
    }

    private static int BuildStat(int baseValue, int level, int dex)
    {
        return Math.Max(1, baseValue + level + (dex % 5));
    }
}
