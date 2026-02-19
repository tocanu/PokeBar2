using System;
using System.Collections.Generic;

namespace Pokebar.Core.Models;

/// <summary>
/// Dados de progresso do jogador salvos em JSON.
/// Separado de GameplayConfig (que são configurações estáticas de balanceamento).
/// </summary>
public record SaveData
{
    /// <summary>Versão do formato de save para migrações futuras.</summary>
    public int Version { get; init; } = 1;

    /// <summary>ID do perfil ativo quando o save foi feito.</summary>
    public string ActiveProfileId { get; init; } = "default";

    /// <summary>Dex do Pokémon atualmente ativo.</summary>
    public int ActiveDex { get; init; } = 25;

    /// <summary>FormId do Pokémon ativo (ex: "0000" para forma base).</summary>
    public string ActiveFormId { get; init; } = "0000";

    /// <summary>Nível do jogador.</summary>
    public int Level { get; init; } = 1;

    /// <summary>XP acumulado no nível atual.</summary>
    public int Xp { get; init; }

    /// <summary>Pokéballs restantes.</summary>
    public int Pokeballs { get; init; } = 10;

    /// <summary>Lista de dex numbers dos Pokémon capturados (party/box).</summary>
    public List<int> Party { get; init; } = new() { 25 };

    /// <summary>Estatísticas acumuladas da sessão.</summary>
    public PlayerStats Stats { get; init; } = new();

    /// <summary>Se o onboarding de primeiro uso foi concluído.</summary>
    public bool OnboardingCompleted { get; init; }

    /// <summary>IDs de conquistas desbloqueadas.</summary>
    public List<string> Achievements { get; init; } = new();

    /// <summary>Valor de amizade com o Pokémon ativo (0-255).</summary>
    public int Friendship { get; init; } = 70;

    /// <summary>Dex numbers de Pokémon shiny capturados.</summary>
    public List<int> ShinyCaptured { get; init; } = new();

    /// <summary>Progresso de missões ativas.</summary>
    public List<QuestProgress> ActiveQuests { get; init; } = new();

    /// <summary>IDs de missões já completadas (histórico).</summary>
    public List<string> CompletedQuests { get; init; } = new();

    /// <summary>Total de cliques/carícias no pet.</summary>
    public int TotalPets { get; init; }

    /// <summary>IDs de mods habilitados.</summary>
    public List<string> EnabledMods { get; init; } = new();

    /// <summary>Silenciar notificações toast (persiste entre sessões).</summary>
    public bool SilenceNotifications { get; init; }

    /// <summary>Bloquear spawns e combate (persiste entre sessões).</summary>
    public bool BlockSpawns { get; init; }

    /// <summary>Inventário de pedras de evolução (stone → quantidade).</summary>
    public Dictionary<EvolutionStone, int> Stones { get; init; } = new();

    /// <summary>Se o jogador cancelou a evolução pendente (para não perguntar a cada level-up).</summary>
    public bool EvolutionCancelled { get; init; }

    /// <summary>Data/hora do último save.</summary>
    public DateTime LastSaved { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Estatísticas do jogador acumuladas ao longo do tempo.
/// </summary>
public record PlayerStats
{
    /// <summary>Total de Pokémon capturados (inclui tentativas bem-sucedidas).</summary>
    public int TotalCaptured { get; init; }

    /// <summary>Total de capturas que falharam.</summary>
    public int TotalCaptureFailed { get; init; }

    /// <summary>Total de batalhas iniciadas.</summary>
    public int TotalBattles { get; init; }

    /// <summary>Total de batalhas vencidas.</summary>
    public int TotalBattlesWon { get; init; }

    /// <summary>Total de Pokéballs usadas.</summary>
    public int TotalPokeballsUsed { get; init; }

    /// <summary>Tempo total em jogo (segundos).</summary>
    public double TotalPlayTimeSeconds { get; init; }
}
