namespace Pokebar.Core.Models;

/// <summary>
/// Definição de uma conquista.
/// </summary>
public record Achievement
{
    public string Id { get; init; } = string.Empty;
    public string TitleKey { get; init; } = string.Empty;
    public string DescriptionKey { get; init; } = string.Empty;
    public string Icon { get; init; } = "⭐";
    public AchievementCondition Condition { get; init; } = new();
}

/// <summary>
/// Condição para desbloquear uma conquista.
/// </summary>
public record AchievementCondition
{
    /// <summary>Tipo: "captured", "battles_won", "playtime", "pokeballs_used", "party_size"</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Valor mínimo para desbloquear.</summary>
    public int Threshold { get; init; }
}
