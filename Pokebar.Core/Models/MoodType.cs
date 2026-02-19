namespace Pokebar.Core.Models;

/// <summary>
/// Humor atual do Pokémon do jogador.
/// Afeta comportamentos idle, velocidade de animação e interações.
/// </summary>
public enum MoodType
{
    /// <summary>Feliz — animações mais rápidas, mais hop/dance.</summary>
    Happy = 0,

    /// <summary>Neutro — comportamento padrão.</summary>
    Neutral = 1,

    /// <summary>Triste — animações mais lentas, mais idle/sit.</summary>
    Sad = 2,

    /// <summary>Sonolento — boceja, dorme com mais frequência.</summary>
    Sleepy = 3
}
