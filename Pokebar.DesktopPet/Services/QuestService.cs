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
    private readonly List<Quest> _dailyQuestPool;
    private double _newQuestTimer;
    private double _walkTimeAccumulator;

    // BUG FIX: detectar virada de dia com app aberto.
    // CheckDailyReset era chamado apenas no boot (EnsureInitialQuests).
    // Se o app ficasse aberto além da meia-noite as dailies nunca eram resetadas.
    private const double MIDNIGHT_CHECK_INTERVAL_SECONDS = 30.0;
    private double _midnightCheckTimer;
    private DateTime _lastCheckedDate = DateTime.UtcNow.Date;

    /// <summary>Missões ativas com progresso.</summary>
    public List<QuestProgress> ActiveQuests { get; private set; } = new();

    /// <summary>IDs de missões completadas (histórico).</summary>
    public List<string> CompletedQuests { get; private set; } = new();

    /// <summary>IDs das daily quests ativas hoje.</summary>
    public List<string> DailyQuestIds { get; private set; } = new();

    /// <summary>Streak de dias consecutivos com daily quests completadas.</summary>
    public int DailyStreak { get; private set; }

    /// <summary>Data da última daily quest completada (para streak).</summary>
    public DateTime? LastDailyDate { get; private set; }

    /// <summary>
    /// Data em que as daily quests foram geradas pela última vez.
    /// Separado de LastDailyDate para que reiniciar o app no mesmo dia
    /// não regenere as quests enquanto o jogador ainda não as completou.
    /// </summary>
    public DateTime? LastDailyGeneratedDate { get; private set; }

    /// <summary>Disparado quando uma missão é completada.</summary>
    public event Action<Quest, QuestProgress>? QuestCompleted;

    public QuestService(QuestConfig config)
    {
        _config = config;
        _questPool = BuildQuestPool();
        _dailyQuestPool = BuildDailyQuestPool();
    }

    /// <summary>Restaura estado de quests do SaveData.</summary>
    public void RestoreFromSave(SaveData save)
    {
        ActiveQuests = new List<QuestProgress>(save.ActiveQuests);
        CompletedQuests = new List<string>(save.CompletedQuests);
        DailyQuestIds = new List<string>(save.DailyQuestIds);
        DailyStreak = save.DailyStreak;
        LastDailyDate = save.LastDailyDate;
        LastDailyGeneratedDate = save.LastDailyGeneratedDate;

        // Sincronizar _lastCheckedDate com a data atual após restaurar o save,
        // para que a verificação de virada de dia não dispare imediatamente no boot
        // (CheckDailyReset já é chamado via EnsureInitialQuests logo em seguida).
        _lastCheckedDate = DateTime.UtcNow.Date;
    }

    /// <summary>
    /// Atualiza o timer de geração de novas missões e verifica virada de dia.
    /// </summary>
    public void Update(double deltaTime)
    {
        if (!_config.Enabled) return;

        // BUG FIX: verificar virada de meia-noite durante runtime.
        // Checagem a cada 30 s (não a cada frame) para minimizar alocações de DateTime.
        _midnightCheckTimer += deltaTime;
        if (_midnightCheckTimer >= MIDNIGHT_CHECK_INTERVAL_SECONDS)
        {
            _midnightCheckTimer = 0;
            var today = DateTime.UtcNow.Date;
            if (today != _lastCheckedDate)
            {
                _lastCheckedDate = today;
                Log.Information("QuestService: virada de dia detectada — reiniciando daily quests");
                CheckDailyReset();
            }
        }

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

        var quest = _questPool.FirstOrDefault(q => q.Id == questId) 
                   ?? _dailyQuestPool.FirstOrDefault(q => q.Id == questId);
        if (quest != null)
        {
            CompletedQuests.Add(questId);
            ActiveQuests.RemoveAt(idx);
            Log.Information("Quest claimed: {QuestId}, Reward: {RewardType} x{Amount}",
                questId, quest.RewardType, quest.RewardAmount);

            // Se era daily quest, atualizar streak
            if (quest.IsDaily && DailyQuestIds.Contains(questId))
            {
                var today = DateTime.UtcNow.Date;
                if (LastDailyDate?.Date != today)
                {
                    DailyStreak++;
                    LastDailyDate = DateTime.UtcNow;
                    Log.Information("Daily streak updated: {Streak} days", DailyStreak);
                }
            }
        }

        return quest;
    }

    /// <summary>Gera missões iniciais se não há nenhuma ativa.</summary>
    public void EnsureInitialQuests()
    {
        if (!_config.Enabled) return;

        // Verificar daily quests
        CheckDailyReset();

        while (ActiveQuests.Count < _config.MaxActiveQuests)
        {
            if (!TryGenerateNewQuest())
                break;
        }
    }

    /// <summary>Verifica se precisa resetar as daily quests (novo dia).</summary>
    public void CheckDailyReset()
    {
        if (!_config.DailyQuestsEnabled) return;

        var today = DateTime.UtcNow.Date;

        // BUG FIX: usar LastDailyGeneratedDate (data de GERAÇÃO) como guard,
        // não LastDailyDate (data de CLAIM).
        // Antes: se o jogador não completava as dailies, LastDailyDate ficava com
        // a data do dia anterior e CheckDailyReset regenerava as quests a cada
        // reinicialização do app no mesmo dia — duplicando ActiveQuests.
        // Agora: LastDailyGeneratedDate é atualizado em GenerateDailyQuests e
        // garante que a geração ocorre no máximo uma vez por dia calendário.
        if (LastDailyGeneratedDate?.Date == today)
            return; // dailies já geradas hoje

        // Verificar streak baseado na data de CLAIM (LastDailyDate) — correto
        if (LastDailyDate.HasValue)
        {
            var lastClaimedDate = LastDailyDate.Value.Date;
            var daysSinceLast = (today - lastClaimedDate).Days;
            if (daysSinceLast == 1)
            {
                // Dia consecutivo — manter streak
            }
            else if (daysSinceLast > 1)
            {
                // Streak quebrado
                DailyStreak = 0;
                Log.Information("Daily streak reset (gap of {Days} days)", daysSinceLast);
            }
        }

        // Remover daily quests anteriores dos ativos
        var oldDailyIds = new HashSet<string>(DailyQuestIds);
        ActiveQuests.RemoveAll(q => oldDailyIds.Contains(q.QuestId) && !q.Completed);
        DailyQuestIds.Clear();

        // Gerar novas daily quests
        GenerateDailyQuests();
    }

    /// <summary>Retorna a Quest completa por ID.</summary>
    public Quest? GetQuest(string questId) => 
        _questPool.FirstOrDefault(q => q.Id == questId) 
        ?? _dailyQuestPool.FirstOrDefault(q => q.Id == questId);

    /// <summary>Retorna todas as quests ativas com seus dados completos.</summary>
    public IEnumerable<(Quest Quest, QuestProgress Progress)> GetActiveQuestsWithDetails()
    {
        foreach (var progress in ActiveQuests)
        {
            var quest = _questPool.FirstOrDefault(q => q.Id == progress.QuestId)
                        ?? _dailyQuestPool.FirstOrDefault(q => q.Id == progress.QuestId);
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

            var quest = _questPool.FirstOrDefault(q => q.Id == progress.QuestId)
                        ?? _dailyQuestPool.FirstOrDefault(q => q.Id == progress.QuestId);
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

    private void GenerateDailyQuests()
    {
        if (!_config.DailyQuestsEnabled || _dailyQuestPool.Count == 0) return;

        // BUG FIX: marcar data de geração ANTES de gerar as quests.
        // Isso garante que, mesmo se o processo for encerrado durante a geração,
        // uma nova reinicialização no mesmo dia não tente regenerar novamente.
        LastDailyGeneratedDate = DateTime.UtcNow;

        var count = Math.Min(_config.DailyQuestCount, _dailyQuestPool.Count);
        var shuffled = _dailyQuestPool.OrderBy(_ => _random.Next()).Take(count).ToList();

        foreach (var quest in shuffled)
        {
            DailyQuestIds.Add(quest.Id);
            ActiveQuests.Add(new QuestProgress
            {
                QuestId = quest.Id,
                CurrentAmount = 0,
                Completed = false,
                RewardClaimed = false,
                StartedAt = DateTime.UtcNow
            });
            Log.Information("Daily quest generated: {QuestId} ({Objective} x{Target})",
                quest.Id, quest.Objective, quest.TargetAmount);
        }
    }

    private static List<Quest> BuildDailyQuestPool()
    {
        return new List<Quest>
        {
            new Quest
            {
                Id = "daily_capture_2",
                TitleKey = "quest.daily_capture_2.title",
                DescriptionKey = "quest.daily_capture_2.desc",
                Objective = QuestObjective.CaptureAny,
                TargetAmount = 2,
                RewardType = QuestRewardType.Pokeballs,
                RewardAmount = 3,
                IsDaily = true
            },
            new Quest
            {
                Id = "daily_win_2",
                TitleKey = "quest.daily_win_2.title",
                DescriptionKey = "quest.daily_win_2.desc",
                Objective = QuestObjective.WinBattles,
                TargetAmount = 2,
                RewardType = QuestRewardType.Pokeballs,
                RewardAmount = 3,
                IsDaily = true
            },
            new Quest
            {
                Id = "daily_pet_3",
                TitleKey = "quest.daily_pet_3.title",
                DescriptionKey = "quest.daily_pet_3.desc",
                Objective = QuestObjective.PetPokemon,
                TargetAmount = 3,
                RewardType = QuestRewardType.FriendshipBoost,
                RewardAmount = 10,
                IsDaily = true
            },
            new Quest
            {
                Id = "daily_walk_120",
                TitleKey = "quest.daily_walk_120.title",
                DescriptionKey = "quest.daily_walk_120.desc",
                Objective = QuestObjective.WalkTime,
                TargetAmount = 120,
                RewardType = QuestRewardType.FriendshipBoost,
                RewardAmount = 10,
                IsDaily = true
            },
            new Quest
            {
                Id = "daily_pokeball_5",
                TitleKey = "quest.daily_pokeball_5.title",
                DescriptionKey = "quest.daily_pokeball_5.desc",
                Objective = QuestObjective.UsePokeballs,
                TargetAmount = 5,
                RewardType = QuestRewardType.Pokeballs,
                RewardAmount = 4,
                IsDaily = true
            },
            new Quest
            {
                Id = "daily_capture_win",
                TitleKey = "quest.daily_capture_win.title",
                DescriptionKey = "quest.daily_capture_win.desc",
                Objective = QuestObjective.CaptureAny,
                TargetAmount = 1,
                RewardType = QuestRewardType.EvolutionStone,
                RewardAmount = 1,
                IsDaily = true
            }
        };
    }
}
