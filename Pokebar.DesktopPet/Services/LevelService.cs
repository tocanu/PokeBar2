using Pokebar.Core.Models;
using Serilog;

namespace Pokebar.DesktopPet.Services;

/// <summary>
/// Gerencia XP e sistema de níveis do jogador.
/// Calcula XP necessário por nível usando curva exponencial configurável.
/// </summary>
public class LevelService
{
    private readonly LevelConfig _config;
    private int _level;
    private int _xp;
    private double _walkXpAccumulator;

    /// <summary>Disparado quando o jogador sobe de nível. (newLevel)</summary>
    public event Action<int>? LevelUp;

    /// <summary>Nível atual do jogador.</summary>
    public int Level => _level;

    /// <summary>XP acumulado no nível atual.</summary>
    public int Xp => _xp;

    /// <summary>XP total necessário para o próximo nível.</summary>
    public int XpToNextLevel => GetXpForLevel(_level);

    /// <summary>Progresso percentual (0.0 a 1.0) para o próximo nível.</summary>
    public double Progress => XpToNextLevel > 0 ? Math.Clamp((double)_xp / XpToNextLevel, 0, 1) : 1.0;

    public LevelService(LevelConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Restaura o estado a partir do save.
    /// </summary>
    public void RestoreFromSave(SaveData save)
    {
        _level = Math.Max(1, save.Level);
        _xp = Math.Max(0, save.Xp);
        Log.Information("LevelService restored: Level={Level}, Xp={Xp}/{XpNeeded}",
            _level, _xp, GetXpForLevel(_level));
    }

    /// <summary>
    /// Retorna o XP necessário para subir do nível dado para o seguinte.
    /// Fórmula: BaseXp * level^GrowthFactor
    /// </summary>
    public int GetXpForLevel(int level)
    {
        if (level >= _config.MaxLevel) return 0; // nível máximo
        return (int)(_config.BaseXp * Math.Pow(level, _config.GrowthFactor));
    }

    /// <summary>
    /// Adiciona XP e processa level-ups. Retorna true se houve level-up.
    /// </summary>
    public bool AddXp(int amount, string source)
    {
        if (amount <= 0 || _level >= _config.MaxLevel) return false;

        _xp += amount;
        Log.Debug("XP +{Amount} ({Source}). Total: {Xp}/{XpNeeded} (Lv.{Level})",
            amount, source, _xp, GetXpForLevel(_level), _level);

        bool leveledUp = false;
        while (_level < _config.MaxLevel && _xp >= GetXpForLevel(_level))
        {
            _xp -= GetXpForLevel(_level);
            _level++;
            leveledUp = true;
            Log.Information("LEVEL UP! Now Lv.{Level} (remaining XP: {Xp})", _level, _xp);
            LevelUp?.Invoke(_level);
        }

        // Cap no nível máximo
        if (_level >= _config.MaxLevel)
            _xp = 0;

        return leveledUp;
    }

    /// <summary>XP por vencer batalha.</summary>
    public bool OnBattleWon() => AddXp(_config.XpPerBattleWin, "battle_won");

    /// <summary>XP por perder batalha (participação).</summary>
    public bool OnBattleLoss() => AddXp(_config.XpPerBattleLoss, "battle_loss");

    /// <summary>XP por capturar Pokémon.</summary>
    public bool OnCapture(bool isShiny)
    {
        var amount = isShiny
            ? (int)(_config.XpPerCapture * _config.ShinyXpMultiplier)
            : _config.XpPerCapture;
        return AddXp(amount, isShiny ? "capture_shiny" : "capture");
    }

    /// <summary>XP por completar quest.</summary>
    public bool OnQuestCompleted() => AddXp(_config.XpPerQuestComplete, "quest");

    /// <summary>XP por acariciar o pet.</summary>
    public bool OnPet() => AddXp(_config.XpPerPet, "pet");

    /// <summary>
    /// XP acumulado por andar. Usa acumulador fracionário como QuestService.
    /// </summary>
    public void OnWalkTime(double deltaSeconds)
    {
        if (_config.XpPerWalkSecond <= 0 || _level >= _config.MaxLevel) return;

        _walkXpAccumulator += _config.XpPerWalkSecond * deltaSeconds;
        if (_walkXpAccumulator >= 1.0)
        {
            int wholeXp = (int)_walkXpAccumulator;
            _walkXpAccumulator -= wholeXp;
            AddXp(wholeXp, "walk");
        }
    }

    /// <summary>
    /// Aplica o estado atual no SaveData.
    /// </summary>
    public SaveData ApplyToSave(SaveData save)
    {
        return save with { Level = _level, Xp = _xp };
    }
}
