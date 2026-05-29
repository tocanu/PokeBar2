namespace Pokebar.Core.Models;

/// <summary>
/// Tabela completa de efetividade de tipos (geração 6+, 18 tipos).
/// Retorna o multiplicador de dano para um ataque de tipo <paramref name="attacker"/>
/// contra um defensor do tipo <paramref name="defender"/>.
/// Valores possíveis: 0 (imune), 0.5 (pouco efetivo), 1 (neutro), 2 (super efetivo).
/// </summary>
public static class TypeChart
{
    // Linhas = tipo do ataque, Colunas = tipo do defensor.
    // Índices seguem a ordem do enum PokemonType (0=Normal … 17=Fairy).
    //
    //          Norm  Fire  Wter  Elec  Grss  Ice   Fght  Psn   Grnd  Fly   Psy   Bug   Rock  Ghst  Drag  Dark  Stl   Fair
    private static readonly double[,] _chart =
    {
        { 1,    1,    1,    1,    1,    1,    1,    1,    1,    1,    1,    1,   .5,    0,    1,    1,   .5,    1 }, // Normal
        { 1,   .5,   .5,    1,    2,    2,    1,    1,    1,    1,    1,    2,   .5,    1,   .5,    1,    2,    1 }, // Fire
        { 1,    2,   .5,    1,   .5,    1,    1,    1,    2,    1,    1,    1,    2,    1,   .5,    1,    1,    1 }, // Water
        { 1,    1,    2,   .5,   .5,    1,    1,    1,    0,    2,    1,    1,    1,    1,   .5,    1,    1,    1 }, // Electric
        { 1,   .5,    2,    1,   .5,    1,    1,   .5,    2,   .5,    1,   .5,    2,    1,   .5,    1,   .5,    1 }, // Grass
        { 1,   .5,   .5,    1,    2,   .5,    1,    1,    2,    2,    1,    1,    1,    1,    2,    1,   .5,    1 }, // Ice
        { 2,    1,    1,    1,    1,    2,    1,   .5,    1,   .5,   .5,   .5,    2,    0,    1,    2,    2,   .5 }, // Fighting
        { 1,    1,    1,    1,    2,    1,    1,   .5,   .5,    1,    1,    1,   .5,   .5,    1,    1,    0,    2 }, // Poison
        { 1,    2,    1,    2,   .5,    1,    1,    2,    1,    0,    1,   .5,    2,    1,    1,    1,    2,    1 }, // Ground
        { 1,    1,    1,   .5,    2,    1,    2,    1,    1,    1,    1,    2,   .5,    1,    1,    1,   .5,    1 }, // Flying
        { 1,    1,    1,    1,    1,    1,    2,    2,    1,    1,   .5,    1,    1,    0,    1,    0,   .5,    1 }, // Psychic
        { 1,   .5,    1,    1,    2,    1,   .5,   .5,    1,   .5,    2,    1,    1,   .5,    1,    2,   .5,   .5 }, // Bug
        { 1,    2,    1,    1,    1,    2,   .5,    1,   .5,    2,    1,    2,    1,    1,    1,    1,   .5,    1 }, // Rock
        { 0,    1,    1,    1,    1,    1,    1,    1,    1,    1,    2,    1,    1,    2,    1,   .5,    1,    1 }, // Ghost
        { 1,    1,    1,    1,    1,    1,    1,    1,    1,    1,    1,    1,    1,    1,    2,    1,   .5,    0 }, // Dragon
        { 1,    1,    1,    1,    1,    1,   .5,    1,    1,    1,    2,    1,    1,    2,    1,   .5,    1,   .5 }, // Dark
        { 1,   .5,   .5,   .5,    1,    2,    1,    1,    1,    1,    1,    1,    2,    1,    1,    1,   .5,    2 }, // Steel
        { 1,   .5,    1,    1,    1,    1,    2,   .5,    1,    1,    1,    1,    1,    1,    2,    2,   .5,    1 }, // Fairy
    };

    /// <summary>
    /// Retorna o multiplicador de dano de <paramref name="attacker"/> contra <paramref name="defender"/>.
    /// 0 = imune, 0.5 = pouco efetivo, 1 = normal, 2 = super efetivo.
    /// </summary>
    public static double GetMultiplier(PokemonType attacker, PokemonType defender)
    {
        var a = (int)attacker;
        var d = (int)defender;
        if ((uint)a >= 18 || (uint)d >= 18)
            return 1.0;
        return _chart[a, d];
    }

    /// <summary>
    /// Retorna emoji representativo de um tipo para uso em mensagens de combate.
    /// </summary>
    public static string TypeEmoji(PokemonType type) => type switch
    {
        PokemonType.Normal   => "⬜",
        PokemonType.Fire     => "🔥",
        PokemonType.Water    => "💧",
        PokemonType.Electric => "⚡",
        PokemonType.Grass    => "🍃",
        PokemonType.Ice      => "❄️",
        PokemonType.Fighting => "🥊",
        PokemonType.Poison   => "☠️",
        PokemonType.Ground   => "🌍",
        PokemonType.Flying   => "🌪️",
        PokemonType.Psychic  => "🔮",
        PokemonType.Bug      => "🐛",
        PokemonType.Rock     => "🪨",
        PokemonType.Ghost    => "👻",
        PokemonType.Dragon   => "🐉",
        PokemonType.Dark     => "🌑",
        PokemonType.Steel    => "⚙️",
        PokemonType.Fairy    => "✨",
        _                    => "❓",
    };
}
