namespace Pokebar.Core.Models;

/// <summary>
/// Método de evolução de um Pokémon.
/// </summary>
public enum EvolutionMethod
{
    /// <summary>Evolui ao atingir um nível mínimo.</summary>
    Level = 0,

    /// <summary>Evolui ao atingir amizade mínima (e level-up).</summary>
    Friendship = 1,

    /// <summary>Evolui ao usar uma pedra de evolução.</summary>
    Stone = 2
}

/// <summary>
/// Tipos de pedra de evolução.
/// </summary>
public enum EvolutionStone
{
    None = 0,
    Fire = 1,
    Water = 2,
    Thunder = 3,
    Leaf = 4,
    Moon = 5,
    Sun = 6,
    Dusk = 7,
    Dawn = 8,
    Ice = 9,
    Shiny = 10
}

/// <summary>
/// Entrada na tabela de evolução: de qual dex para qual dex, por qual método.
/// </summary>
public record EvolutionEntry
{
    /// <summary>Dex de origem.</summary>
    public int FromDex { get; init; }

    /// <summary>Dex de destino após evolução.</summary>
    public int ToDex { get; init; }

    /// <summary>Método necessário para evoluir.</summary>
    public EvolutionMethod Method { get; init; }

    /// <summary>Nível mínimo (para Method=Level).</summary>
    public int MinLevel { get; init; }

    /// <summary>Amizade mínima (para Method=Friendship).</summary>
    public int MinFriendship { get; init; }

    /// <summary>Pedra necessária (para Method=Stone).</summary>
    public EvolutionStone Stone { get; init; }
}
