using System;
using System.Collections.Generic;
using System.Linq;
using Pokebar.Core.Models;
using Serilog;

namespace Pokebar.DesktopPet.Services;

/// <summary>
/// Gerencia missões rápidas: geração, progresso, completude e recompensas.
/// </summary>
public class QuestService
{
    private readonly QuestConfig _config;
    private readonly Random _random = new();
    private readonly List<Quest> _questPool;
    private double _newQuestTimer;
    private double _walkTimeAccumulator;

    /// <summary>Missões ativas com progresso.</summary>
    public List<QuestProgress> ActiveQuests { get; private set; } = new();

    /// <summary>IDs de missões completadas (histórico).</summary>
    public List<string> CompletedQuests { get; private set; } = new();

    /// <summary>Disparado quando uma missão é completada.</summary>
    public event Action<Quest, QuestProgress>? QuestCompleted;

    public QuestService(QuestConfig config)
    {
        _config = config;
        _questPool = BuildQuestPool();
    }

    /// <summary>Restaura estado de quests do SaveData.</summary>
    public void RestoreFromSave(SaveData save)
    {
        ActiveQuests = new List<QuestProgress>(save.ActiveQuests);
        CompletedQuests = new List<string>(save.CompletedQuests);
    }

    /// <summary>
    /// Atualiza o timer de geração de novas missões.
    /// </summary>
    public void Update(double deltaTime)
    {
        if (!_config.Enabled) return;

        _newQuestTimer += deltaTime;
        var intervalSec = _config.NewQuestIntervalMinutes * 60.0;

        if (_newQuestTimer >= intervalSec && ActiveQuests.Count < _config.MaxActiveQuests)
        {
            _newQuestTimer = 0;
            TryGenerateNewQuest();
        }
    }

    /// <summary>Registra uma captura para progresso de missões.</summary>
    public void OnCapture(bool isShiny)
    {
        UpdateProgress(QuestObjective.CaptureAny, 1);
        if (isShiny)
            UpdateProgress(QuestObjective.CaptureShiny, 1);
    }

    /// <summary>Registra uma vitória de batalha.</summary>
    public void OnBattleWon()
    {
        UpdateProgress(QuestObjective.WinBattles, 1);
    }

    /// <summary>Registra uso de pokéballs.</summary>
    public void OnPokeballUsed()
    {
        UpdateProgress(QuestObjective.UsePokeballs, 1);
    }

    /// <summary>Registra uma carícia/clique.</summary>
    public void OnPet()
    {
        UpdateProgress(QuestObjective.PetPokemon, 1);
    }

    /// <summary>Registra tempo caminhando.</summary>
    public void OnWalkTime(double seconds)
    {
        _walkTimeAccumulator += seconds;
        var whole = (int)_walkTimeAccumulator;
        if (whole > 0)
        {
            _walkTimeAccumulator -= whole;
            UpdateProgress(QuestObjective.WalkTime, whole);
        }
    }

    /// <summary>
    /// Coleta a recompensa de uma missão completada.
    /// Retorna a Quest com detalhes da recompensa, ou null se não encontrada.
    /// </summary>
    public Quest? ClaimReward(string questId)
    {
        var idx = ActiveQuests.FindIndex(q => q.QuestId == questId && q.Completed && !q.RewardClaimed);
        if (idx < 0) return null;

        var progress = ActiveQuests[idx];
        ActiveQuests[idx] = progress with { RewardClaimed = true };

        var quest = _questPool.FirstOrDefault(q => q.Id == questId);
        if (quest != null)
        {
            CompletedQuests.Add(questId);
            ActiveQuests.RemoveAt(idx);
            Log.Information("Quest claimed: {QuestId}, Reward: {RewardType} x{Amount}",
                questId, quest.RewardType, quest.RewardAmount);
        }

        return quest;
    }

    /// <summary>Gera missões iniciais se não há nenhuma ativa.</summary>
    public void EnsureInitialQuests()
    {
        if (!_config.Enabled) return;
        while (ActiveQuests.Count < _config.MaxActiveQuests)
        {
            if (!TryGenerateNewQuest())
                break;
        }
    }

    /// <summary>Retorna a Quest completa por ID.</summary>
    public Quest? GetQuest(string questId) => _questPool.FirstOrDefault(q => q.Id == questId);

    /// <summary>Retorna todas as quests ativas com seus dados completos.</summary>
    public IEnumerable<(Quest Quest, QuestProgress Progress)> GetActiveQuestsWithDetails()
    {
        foreach (var progress in ActiveQuests)
        {
            var quest = _questPool.FirstOrDefault(q => q.Id == progress.QuestId);
            if (quest != null)
                yield return (quest, progress);
        }
    }

    private void UpdateProgress(QuestObjective objective, int amount)
    {
        if (!_config.Enabled) return;

        for (int i = 0; i < ActiveQuests.Count; i++)
        {
            var progress = ActiveQuests[i];
            if (progress.Completed) continue;

            var quest = _questPool.FirstOrDefault(q => q.Id == progress.QuestId);
            if (quest == null || quest.Objective != objective) continue;

            var newAmount = Math.Min(progress.CurrentAmount + amount, quest.TargetAmount);
            var completed = newAmount >= quest.TargetAmount;

            ActiveQuests[i] = progress with { CurrentAmount = newAmount, Completed = completed };

            if (completed)
            {
                Log.Information("Quest completed: {QuestId}", quest.Id);
                QuestCompleted?.Invoke(quest, ActiveQuests[i]);
            }
        }
    }

    private bool TryGenerateNewQuest()
    {
        var activeIds = new HashSet<string>(ActiveQuests.Select(q => q.QuestId));
        var completedIds = new HashSet<string>(CompletedQuests);
        var available = _questPool.Where(q => !activeIds.Contains(q.Id) && !completedIds.Contains(q.Id)).ToList();

        if (available.Count == 0)
        {
            // Reciclar missões: usar missões já completadas
            available = _questPool.Where(q => !activeIds.Contains(q.Id)).ToList();
        }

        if (available.Count == 0) return false;

        var quest = available[_random.Next(available.Count)];
        ActiveQuests.Add(new QuestProgress
        {
            QuestId = quest.Id,
            CurrentAmount = 0,
            Completed = false,
            RewardClaimed = false,
            StartedAt = DateTime.UtcNow
        });

        Log.Information("Quest generated: {QuestId} ({Objective} x{Target})",
            quest.Id, quest.Objective, quest.TargetAmount);
        return true;
    }

    private static List<Quest> BuildQuestPool()
    {
        return new List<Quest>
        {
            new Quest
            {
                Id = "capture_3",
                TitleKey = "quest.capture_3.title",
                DescriptionKey = "quest.capture_3.desc",
                Objective = QuestObjective.CaptureAny,
                TargetAmount = 3,
                RewardType = QuestRewardType.Pokeballs,
                RewardAmount = 5
            },
            new Quest
            {
                Id = "capture_10",
                TitleKey = "quest.capture_10.title",
                DescriptionKey = "quest.capture_10.desc",
                Objective = QuestObjective.CaptureAny,
                TargetAmount = 10,
                RewardType = QuestRewardType.Pokeballs,
                RewardAmount = 15
            },
            new Quest
            {
                Id = "win_3",
                TitleKey = "quest.win_3.title",
                DescriptionKey = "quest.win_3.desc",
                Objective = QuestObjective.WinBattles,
                TargetAmount = 3,
                RewardType = QuestRewardType.Pokeballs,
                RewardAmount = 3
            },
            new Quest
            {
                Id = "win_10",
                TitleKey = "quest.win_10.title",
                DescriptionKey = "quest.win_10.desc",
                Objective = QuestObjective.WinBattles,
                TargetAmount = 10,
                RewardType = QuestRewardType.RareSpawn,
                RewardAmount = 1
            },
            new Quest
            {
                Id = "pet_5",
                TitleKey = "quest.pet_5.title",
                DescriptionKey = "quest.pet_5.desc",
                Objective = QuestObjective.PetPokemon,
                TargetAmount = 5,
                RewardType = QuestRewardType.FriendshipBoost,
                RewardAmount = 20
            },
            new Quest
            {
                Id = "pet_20",
                TitleKey = "quest.pet_20.title",
                DescriptionKey = "quest.pet_20.desc",
                Objective = QuestObjective.PetPokemon,
                TargetAmount = 20,
                RewardType = QuestRewardType.Pokeballs,
                RewardAmount = 10
            },
            new Quest
            {
                Id = "pokeball_10",
                TitleKey = "quest.pokeball_10.title",
                DescriptionKey = "quest.pokeball_10.desc",
                Objective = QuestObjective.UsePokeballs,
                TargetAmount = 10,
                RewardType = QuestRewardType.Pokeballs,
                RewardAmount = 8
            },
            new Quest
            {
                Id = "walk_300",
                TitleKey = "quest.walk_300.title",
                DescriptionKey = "quest.walk_300.desc",
                Objective = QuestObjective.WalkTime,
                TargetAmount = 300,
                RewardType = QuestRewardType.FriendshipBoost,
                RewardAmount = 15
            },
            new Quest
            {
                Id = "capture_shiny",
                TitleKey = "quest.capture_shiny.title",
                DescriptionKey = "quest.capture_shiny.desc",
                Objective = QuestObjective.CaptureShiny,
                TargetAmount = 1,
                RewardType = QuestRewardType.Pokeballs,
                RewardAmount = 25
            },
            new Quest
            {
                Id = "capture_5_quick",
                TitleKey = "quest.capture_5_quick.title",
                DescriptionKey = "quest.capture_5_quick.desc",
                Objective = QuestObjective.CaptureAny,
                TargetAmount = 5,
                RewardType = QuestRewardType.RareSpawn,
                RewardAmount = 1
            },
            new Quest
            {
                Id = "win_5_stone",
                TitleKey = "quest.win_5_stone.title",
                DescriptionKey = "quest.win_5_stone.desc",
                Objective = QuestObjective.WinBattles,
                TargetAmount = 5,
                RewardType = QuestRewardType.EvolutionStone,
                RewardAmount = 1
            },
            new Quest
            {
                Id = "capture_7_stone",
                TitleKey = "quest.capture_7_stone.title",
                DescriptionKey = "quest.capture_7_stone.desc",
                Objective = QuestObjective.CaptureAny,
                TargetAmount = 7,
                RewardType = QuestRewardType.EvolutionStone,
                RewardAmount = 1
            }
        };
    }
}
