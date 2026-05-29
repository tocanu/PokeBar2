using System;

namespace Pokebar.Core.Models;

/// <summary>
/// Tipos de efeito de status que podem ser aplicados durante combate.
/// </summary>
public enum StatusEffectType
{
    /// <summary>Sem efeito.</summary>
    None = 0,

    /// <summary>Sono — perde o turno.</summary>
    Sleep = 1,

    /// <summary>Veneno — recebe dano a cada turno (1/8 do HP máximo).</summary>
    Poison = 2,

    /// <summary>Paralisia — 25% de chance de perder o turno + velocidade reduzida.</summary>
    Paralysis = 3
}

/// <summary>
/// Tier de raridade do Pokémon, usado para balancear captura e spawn.
/// </summary>
public enum RarityTier
{
    Common = 0,
    Uncommon = 1,
    Rare = 2,
    Epic = 3,
    Legendary = 4
}

/// <summary>
/// Define um movimento disponível em combate.
/// Todos os moves usam a mesma animação; a diferença é lógica (dano/crit/efeito/cooldown).
/// </summary>
public record MoveDefinition
{
    /// <summary>Nome do move (para logs/overlay).</summary>
    public string Name { get; init; } = "Tackle";

    /// <summary>Dano base do move.</summary>
    public int BaseDamage { get; init; } = 10;

    /// <summary>Chance de acerto crítico (0.0–1.0). Crit = 2x dano.</summary>
    public double CritChance { get; init; } = 0.05;

    /// <summary>Efeito de status que o move pode aplicar.</summary>
    public StatusEffectType StatusEffect { get; init; } = StatusEffectType.None;

    /// <summary>Chance de aplicar o efeito de status (0.0–1.0).</summary>
    public double StatusChance { get; init; }

    /// <summary>Cooldown em rodadas antes de poder usar novamente (0 = sem cooldown).</summary>
    public int CooldownRounds { get; init; }

    /// <summary>Tipo do movimento (determina efetividade via TypeChart).</summary>
    public PokemonType Type { get; init; } = PokemonType.Normal;

    /// <summary>Moves padrão disponíveis para todos os Pokémon.</summary>
    public static MoveDefinition[] DefaultMoves() => new[]
    {
        new MoveDefinition
        {
            Name = "Tackle",
            BaseDamage = 10,
            CritChance = 0.06,
            CooldownRounds = 0,
            Type = PokemonType.Normal
        },
        new MoveDefinition
        {
            Name = "Headbutt",
            BaseDamage = 18,
            CritChance = 0.10,
            CooldownRounds = 1,
            Type = PokemonType.Normal
        },
        new MoveDefinition
        {
            Name = "Poison Sting",
            BaseDamage = 8,
            CritChance = 0.05,
            StatusEffect = StatusEffectType.Poison,
            StatusChance = 0.30,
            CooldownRounds = 2,
            Type = PokemonType.Poison
        },
        new MoveDefinition
        {
            Name = "Body Slam",
            BaseDamage = 22,
            CritChance = 0.08,
            StatusEffect = StatusEffectType.Paralysis,
            StatusChance = 0.25,
            CooldownRounds = 3,
            Type = PokemonType.Normal
        },
        new MoveDefinition
        {
            Name = "Hypnosis",
            BaseDamage = 0,
            CritChance = 0,
            StatusEffect = StatusEffectType.Sleep,
            StatusChance = 0.60,
            CooldownRounds = 4,
            Type = PokemonType.Psychic
        }
    };
}
