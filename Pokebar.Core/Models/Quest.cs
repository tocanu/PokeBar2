using System;
using System.Collections.Generic;

namespace Pokebar.Core.Models;

/// <summary>
/// Define uma missão rápida com objetivo e recompensa.
/// </summary>
public record Quest
{
    /// <summary>Identificador único da missão.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Chave de localização para o título.</summary>
    public string TitleKey { get; init; } = string.Empty;

    /// <summary>Chave de localização para a descrição.</summary>
    public string DescriptionKey { get; init; } = string.Empty;

    /// <summary>Tipo do objetivo.</summary>
    public QuestObjective Objective { get; init; }

    /// <summary>Quantidade necessária para completar.</summary>
    public int TargetAmount { get; init; } = 1;

    /// <summary>Tipo de recompensa.</summary>
    public QuestRewardType RewardType { get; init; }

    /// <summary>Quantidade da recompensa.</summary>
    public int RewardAmount { get; init; } = 1;
}

/// <summary>
/// Tipos de objetivos de missões.
/// </summary>
public enum QuestObjective
{
    /// <summary>Capturar N Pokémon.</summary>
    CaptureAny = 0,

    /// <summary>Vencer N batalhas.</summary>
    WinBattles = 1,

    /// <summary>Usar N Pokéballs.</summary>
    UsePokeballs = 2,

    /// <summary>Caminhar N segundos.</summary>
    WalkTime = 3,

    /// <summary>Capturar um Pokémon de tipo específico (dex range).</summary>
    CaptureSpecific = 4,

    /// <summary>Acariciar o Pokémon N vezes (cliques).</summary>
    PetPokemon = 5,

    /// <summary>Capturar um shiny.</summary>
    CaptureShiny = 6
}

/// <summary>
/// Tipos de recompensas de missões.
/// </summary>
public enum QuestRewardType
{
    /// <summary>Pokéballs extras.</summary>
    Pokeballs = 0,

    /// <summary>Spawn raro garantido.</summary>
    RareSpawn = 1,

    /// <summary>Boost de amizade.</summary>
    FriendshipBoost = 2,

    /// <summary>Pedra de evolução aleatória.</summary>
    EvolutionStone = 3
}

/// <summary>
/// Progresso de uma missão ativa.
/// </summary>
public record QuestProgress
{
    /// <summary>ID da missão.</summary>
    public string QuestId { get; init; } = string.Empty;

    /// <summary>Quantidade atual de progresso.</summary>
    public int CurrentAmount { get; init; }

    /// <summary>Se a missão foi completada.</summary>
    public bool Completed { get; init; }

    /// <summary>Se a recompensa foi coletada.</summary>
    public bool RewardClaimed { get; init; }

    /// <summary>Data de início da missão.</summary>
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
}
