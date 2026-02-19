using System;
using System.Collections.Generic;
using Pokebar.Core.Models;
using Pokebar.DesktopPet.Animation;

namespace Pokebar.DesktopPet.Entities;

public class PlayerPet : PokemonPet
{
    private readonly List<int> _party = new();
    private int _friendship = 70;

    public PlayerPet(int dex, int pokeballs = 0) : base(dex)
    {
        Level = 5;
        InitializeStats();
        if (pokeballs > 0)
            Pokeballs = pokeballs;
        _party.Add(dex);
    }

    public int Level { get; set; }
    public int MaxHp { get; private set; }
    public int CurrentHp { get; private set; }
    public int Attack { get; private set; }
    public int Defense { get; private set; }
    public int Pokeballs { get; private set; }
    public IReadOnlyList<int> Party => _party;

    // FASE 7: Amizade e Humor
    /// <summary>Valor de amizade (0-255). Afeta humor e comportamentos.</summary>
    public int Friendship
    {
        get => _friendship;
        private set => _friendship = Math.Clamp(value, 0, 255);
    }

    /// <summary>Humor atual derivado da amizade e estado.</summary>
    public MoodType Mood { get; set; } = MoodType.Neutral;

    /// <summary>Aumenta amizade em N pontos.</summary>
    public void AddFriendship(int amount)
    {
        if (amount > 0)
            Friendship += amount;
    }

    /// <summary>Reduz amizade em N pontos.</summary>
    public void LoseFriendship(int amount)
    {
        if (amount > 0)
            Friendship -= amount;
    }

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

    /// <summary>
    /// Troca o Pokémon usando cache de animações (mais eficiente).
    /// </summary>
    public bool ChangePokemon(int newDex, SpriteCache cache, Pokebar.Core.Models.GameplayConfig config)
    {
        if (Dex == newDex)
            return false;

        cache.Unpin(UniqueId);
        Dex = newDex;
        FormId = "0000";
        UniqueId = new Pokebar.Core.Models.PokemonVariant(newDex, FormId).UniqueId;
        if (!_party.Contains(newDex))
            _party.Add(newDex);

        InitializeStats();
        var anims = cache.GetAnimations(newDex, FormId, config);
        ApplyAnimations(anims);
        cache.Pin(UniqueId);
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

    /// <summary>
    /// Restaura o estado do jogador a partir de SaveData.
    /// Chamado na inicialização quando um save existe.
    /// </summary>
    public void RestoreFromSave(SaveData save)
    {
        Dex = save.ActiveDex;
        FormId = save.ActiveFormId;
        UniqueId = new PokemonVariant(save.ActiveDex, save.ActiveFormId).UniqueId;
        Level = save.Level;
        Pokeballs = save.Pokeballs;
        Friendship = save.Friendship;

        _party.Clear();
        if (save.Party.Count > 0)
        {
            foreach (var dex in save.Party)
                _party.Add(dex);
        }
        else
        {
            _party.Add(Dex);
        }

        InitializeStats();
    }

    /// <summary>
    /// Gera um SaveData a partir do estado atual do jogador.
    /// </summary>
    public SaveData ToSaveData(PlayerStats? existingStats = null)
    {
        return new SaveData
        {
            ActiveDex = Dex,
            ActiveFormId = FormId,
            Level = Level,
            Pokeballs = Pokeballs,
            Party = new List<int>(_party),
            Friendship = Friendship,
            Stats = existingStats ?? new PlayerStats()
        };
    }
}
