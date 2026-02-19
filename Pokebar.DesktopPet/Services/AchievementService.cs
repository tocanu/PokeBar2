using System;
using System.Collections.Generic;
using System.Linq;
using Pokebar.Core.Models;
using Serilog;

namespace Pokebar.DesktopPet.Services;

/// <summary>
/// Gerencia conquistas, verificando condições e desbloqueando automaticamente.
/// </summary>
public class AchievementService
{
    private readonly List<Achievement> _definitions;
    private readonly HashSet<string> _unlocked;

    /// <summary>Disparado quando uma conquista é desbloqueada. Parâmetro: Achievement.</summary>
    public event Action<Achievement>? AchievementUnlocked;

    public IReadOnlyList<Achievement> AllAchievements => _definitions;

    public AchievementService(List<string>? unlockedIds = null)
    {
        _definitions = BuildDefinitions();
        _unlocked = new HashSet<string>(unlockedIds ?? new List<string>());
    }

    /// <summary>
    /// Verifica se alguma conquista nova foi desbloqueada com base nas stats atuais.
    /// Retorna lista de conquistas recém-desbloqueadas.
    /// </summary>
    public List<Achievement> CheckAndUnlock(PlayerStats stats, int partySize, int pokeballs)
    {
        var newlyUnlocked = new List<Achievement>();

        foreach (var a in _definitions)
        {
            if (_unlocked.Contains(a.Id))
                continue;

            var met = a.Condition.Type switch
            {
                "captured" => stats.TotalCaptured >= a.Condition.Threshold,
                "battles_won" => stats.TotalBattlesWon >= a.Condition.Threshold,
                "playtime" => stats.TotalPlayTimeSeconds >= a.Condition.Threshold,
                "pokeballs_used" => stats.TotalPokeballsUsed >= a.Condition.Threshold,
                "party_size" => partySize >= a.Condition.Threshold,
                "battles" => stats.TotalBattles >= a.Condition.Threshold,
                _ => false
            };

            if (met)
            {
                _unlocked.Add(a.Id);
                newlyUnlocked.Add(a);
                AchievementUnlocked?.Invoke(a);
                Log.Information("Achievement unlocked: {Id} — {Title}", a.Id, a.TitleKey);
            }
        }

        return newlyUnlocked;
    }

    public List<string> GetUnlockedIds() => _unlocked.ToList();

    private static List<Achievement> BuildDefinitions()
    {
        return new List<Achievement>
        {
            new() { Id = "first_capture", TitleKey = "achievement.first_capture.title",
                     DescriptionKey = "achievement.first_capture.desc", Icon = "🔴",
                     Condition = new() { Type = "captured", Threshold = 1 } },

            new() { Id = "capture_5", TitleKey = "achievement.capture_5.title",
                     DescriptionKey = "achievement.capture_5.desc", Icon = "⚾",
                     Condition = new() { Type = "captured", Threshold = 5 } },

            new() { Id = "capture_25", TitleKey = "achievement.capture_25.title",
                     DescriptionKey = "achievement.capture_25.desc", Icon = "🏆",
                     Condition = new() { Type = "captured", Threshold = 25 } },

            new() { Id = "battle_1", TitleKey = "achievement.battle_1.title",
                     DescriptionKey = "achievement.battle_1.desc", Icon = "⚔",
                     Condition = new() { Type = "battles_won", Threshold = 1 } },

            new() { Id = "battle_10", TitleKey = "achievement.battle_10.title",
                     DescriptionKey = "achievement.battle_10.desc", Icon = "🥊",
                     Condition = new() { Type = "battles_won", Threshold = 10 } },

            new() { Id = "battle_50", TitleKey = "achievement.battle_50.title",
                     DescriptionKey = "achievement.battle_50.desc", Icon = "🏅",
                     Condition = new() { Type = "battles_won", Threshold = 50 } },

            new() { Id = "party_3", TitleKey = "achievement.party_3.title",
                     DescriptionKey = "achievement.party_3.desc", Icon = "📦",
                     Condition = new() { Type = "party_size", Threshold = 3 } },

            new() { Id = "party_6", TitleKey = "achievement.party_6.title",
                     DescriptionKey = "achievement.party_6.desc", Icon = "🎒",
                     Condition = new() { Type = "party_size", Threshold = 6 } },

            new() { Id = "playtime_1h", TitleKey = "achievement.playtime_1h.title",
                     DescriptionKey = "achievement.playtime_1h.desc", Icon = "⏰",
                     Condition = new() { Type = "playtime", Threshold = 3600 } },

            new() { Id = "playtime_10h", TitleKey = "achievement.playtime_10h.title",
                     DescriptionKey = "achievement.playtime_10h.desc", Icon = "🕐",
                     Condition = new() { Type = "playtime", Threshold = 36000 } },
        };
    }
}
