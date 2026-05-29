using System;
using System.Collections.Generic;
using System.Windows;
using Pokebar.Core.Models;
using Pokebar.DesktopPet.Entities;
using Serilog;

namespace Pokebar.DesktopPet.Combat;

public class CombatManager
{
    private readonly Random _random = new();
    private readonly double _collisionTolerance;
    private readonly double _spacing;
    private readonly double _roundDuration;
    private readonly int _rounds;
    private readonly double _retreatDistance;
    private readonly double _cooldownDuration;
    private readonly double _damageVariance;
    private readonly int _poisonDivisor;
    private readonly double _paralysisSkipChance;
    private readonly int _sleepDurationRounds;
    private readonly MoveDefinition[] _moves;
    private CombatSession? _active;
    private double _cooldownRemaining;

    public CombatManager(CombatConfig config)
    {
        _collisionTolerance = config.CollisionTolerance;
        _spacing = config.FacingSpacing;
        _roundDuration = config.RoundDurationSeconds;
        _rounds = Math.Max(1, config.RoundsPerFight);
        _retreatDistance = config.RetreatDistance;
        _cooldownDuration = config.CooldownSeconds;
        _damageVariance = config.DamageVariance;
        _poisonDivisor = Math.Max(1, config.PoisonDivisor);
        _paralysisSkipChance = config.ParalysisSkipChance;
        _sleepDurationRounds = Math.Max(1, config.SleepDurationRounds);
        _moves = config.Moves.Length > 0 ? config.Moves : MoveDefinition.DefaultMoves();
    }

    public bool IsActive => _active != null;

    /// <summary>Dispara quando uma batalha termina. bool = playerWon.</summary>
    public event Action<bool>? BattleEnded;

    /// <summary>Dispara a cada round com a mensagem do que aconteceu (ex: "Tackle!", "Poison Sting! ☠").</summary>
    public event Action<string>? RoundMessage;

    public void Update(double deltaTime, PlayerPet player, IEnumerable<EnemyPet> enemies)
    {
        if (_cooldownRemaining > 0)
        {
            _cooldownRemaining -= deltaTime;
            if (_cooldownRemaining < 0)
                _cooldownRemaining = 0;
        }

        if (_active == null)
        {
            if (_cooldownRemaining > 0)
                return;

            var enemy = FindCollision(player, enemies);
            if (enemy != null)
                StartCombat(player, enemy);
            return;
        }

        _active.Elapsed += deltaTime;

        // Disparar mensagens de round enquanto o timer avança
        FirePendingRoundMessages();

        if (_active.Elapsed >= _active.TotalDuration)
        {
            ResolveCombat();
        }
    }

    /// <summary>
    /// Dispara as mensagens de round que ainda não foram emitidas,
    /// sincronizando com o tempo decorrido da batalha.
    /// </summary>
    private void FirePendingRoundMessages()
    {
        if (_active == null || _active.RoundMessages.Count == 0) return;

        // Qual round o tempo atual representa (0-based)
        var currentRound = (int)(_active.Elapsed / _roundDuration);
        currentRound = Math.Clamp(currentRound, 0, _active.RoundMessages.Count - 1);

        while (_active.LastFiredMessageIndex < currentRound)
        {
            _active.LastFiredMessageIndex++;
            if (_active.LastFiredMessageIndex < _active.RoundMessages.Count)
                RoundMessage?.Invoke(_active.RoundMessages[_active.LastFiredMessageIndex]);
        }
    }

    private EnemyPet? FindCollision(PlayerPet player, IEnumerable<EnemyPet> enemies)
    {
        if (player.State != EntityState.Idle && player.State != EntityState.Walking)
            return null;

        var playerBox = player.GetHitbox();
        if (playerBox.IsEmpty)
            return null;

        var expanded = playerBox;
        expanded.Inflate(_collisionTolerance, 0);

        foreach (var enemy in enemies)
        {
            if (enemy.State != EntityState.Idle && enemy.State != EntityState.Walking)
                continue;

            var enemyBox = enemy.GetHitbox();
            if (enemyBox.IsEmpty)
                continue;

            if (expanded.IntersectsWith(enemyBox))
                return enemy;
        }

        return null;
    }

    private void StartCombat(PlayerPet player, EnemyPet enemy)
    {
        Log.Information("Combat started: Player (Dex {PlayerDex}, HP {PlayerHp}/{PlayerMaxHp}) vs Enemy (Dex {EnemyDex}, HP {EnemyHp}/{EnemyMaxHp})",
            player.Dex, player.CurrentHp, player.MaxHp, enemy.Dex, enemy.CurrentHp, enemy.MaxHp);

        _active = new CombatSession(player, enemy, _roundDuration, _rounds)
        {
            PlayerPrevVelocity = player.VelocityX,
            EnemyPrevVelocity = enemy.VelocityX,
            PlayerPrevState = player.State,
            EnemyPrevState = enemy.State
        };

        // Para os movimentos
        player.VelocityX = 0;
        enemy.VelocityX = 0;

        // Posiciona frente a frente com espaçamento, mantendo posições relativas
        var mid = (player.X + enemy.X) / 2;
        if (player.X <= enemy.X)
        {
            player.X = mid - (_spacing / 2);
            enemy.X = mid + (_spacing / 2);
        }
        else
        {
            player.X = mid + (_spacing / 2);
            enemy.X = mid - (_spacing / 2);
        }

        // Define direções para se encararem
        player.FacingRight = player.X < enemy.X;
        enemy.FacingRight = enemy.X < player.X;

        // Pré-simular rounds para gerar mensagens e resultado
        var result = SimulateRounds(player, enemy, _active.RoundMessages);
        _active.PrecomputedResult = result;

        // Agora inicia o combate
        player.StartFighting();
        enemy.StartFighting();
    }

    private void ResolveCombat()
    {
        if (_active == null)
            return;

        var player = _active.Player;
        var enemy = _active.Enemy;

        // Simulação de rodadas com moves, dano real e status effects
        var result = _active.PrecomputedResult;

        Log.Information("Combat resolved: {Winner} wins! Rounds={Rounds}, PlayerHP={PHp}/{PMax}, EnemyHP={EHp}/{EMax}",
            result.PlayerWins ? "Player" : "Enemy", _rounds,
            result.PlayerHpRemaining, player.MaxHp, result.EnemyHpRemaining, enemy.MaxHp);

        if (result.PlayerWins)
        {
            // Aplicar HP real e status no inimigo para afetar captura
            enemy.SetHp(result.EnemyHpRemaining);
            enemy.ActiveStatus = result.EnemyStatus;
            enemy.SleepTurnsLeft = 0;

            if (enemy.CurrentHp <= 0)
                enemy.TakeDamage(enemy.MaxHp); // triggers Fainted via 0 HP
            else
                enemy.Faint(); // fainted por derrota, com HP restante

            RestoreAfterCombat(player, _active.PlayerPrevState, _active.PlayerPrevVelocity);
            player.EndCombat();
        }
        else
        {
            player.X += player.FacingRight ? -_retreatDistance : _retreatDistance;
            RestoreAfterCombat(player, _active.PlayerPrevState, _active.PlayerPrevVelocity);
            RestoreAfterCombat(enemy, _active.EnemyPrevState, _active.EnemyPrevVelocity);
            player.EndCombat();
            enemy.ActiveStatus = StatusEffectType.None;
        }

        _active = null;
        _cooldownRemaining = _cooldownDuration;
        BattleEnded?.Invoke(result.PlayerWins);
    }

    /// <summary>
    /// Simula N rodadas de combate com moves, dano real, crit e status effects.
    /// Preenche <paramref name="messages"/> com uma string legível por round (para o typewriter).
    /// </summary>
    private CombatResult SimulateRounds(PlayerPet player, EnemyPet enemy, List<string> messages)
    {
        // Primeira mensagem: tipo do inimigo (exibida imediatamente no round 0)
        var enemyTypeEmoji = Pokebar.Core.Models.TypeChart.TypeEmoji(enemy.PrimaryType);
        messages.Add($"⚔ {enemyTypeEmoji} {enemy.PrimaryType}!");

        var playerMoves = new MoveSet(_moves, _random);
        var enemyMoves = new MoveSet(_moves, _random);

        int pHp = player.MaxHp;
        int eHp = enemy.MaxHp;
        var pStatus = StatusEffectType.None;
        var eStatus = StatusEffectType.None;
        int pSleepLeft = 0;
        int eSleepLeft = 0;

        for (int round = 0; round < _rounds; round++)
        {
            // Aplicar veneno no início do turno
            if (pStatus == StatusEffectType.Poison)
                pHp -= Math.Max(1, player.MaxHp / _poisonDivisor);
            if (eStatus == StatusEffectType.Poison)
                eHp -= Math.Max(1, enemy.MaxHp / _poisonDivisor);

            if (pHp <= 0 || eHp <= 0) break;

            // Turno do jogador
            MoveDefinition? playerMove = null;
            int playerDmg = 0;
            bool playerSkipped = IsSkippedByStatus(pStatus, ref pSleepLeft);

            // BUG FIX: IsSkippedByStatus decrementa sleepLeft mas não limpa o status
            // quando o sono expira (sleepLeft chega a 0 e retorna false). Sem este
            // guard, pStatus permanece Sleep até o fim, propagando bônus indevido
            // de captura via CombatResult.EnemyStatus → enemy.ActiveStatus.
            if (pStatus == StatusEffectType.Sleep && pSleepLeft == 0)
                pStatus = StatusEffectType.None;

            if (!playerSkipped)
            {
                playerMove = playerMoves.PickMove();
                playerMoves.UseMove(playerMove);
                playerDmg = CalcDamage(playerMove, player.Attack, enemy.Defense, enemy.PrimaryType);
                eHp -= playerDmg;

                if (playerMove.StatusEffect != StatusEffectType.None && eStatus == StatusEffectType.None)
                {
                    if (_random.NextDouble() < playerMove.StatusChance)
                    {
                        eStatus = playerMove.StatusEffect;
                        if (eStatus == StatusEffectType.Sleep)
                            eSleepLeft = _sleepDurationRounds;
                        Log.Debug("Round {R}: Player inflicted {Status} on enemy", round + 1, eStatus);
                    }
                }
            }

            if (eHp <= 0)
            {
                messages.Add(BuildRoundText(playerSkipped, playerMove, playerDmg, pStatus,
                                            true, null, 0, eStatus));
                break;
            }

            // Turno do inimigo
            MoveDefinition? enemyMove = null;
            int enemyDmg = 0;
            bool enemySkipped = IsSkippedByStatus(eStatus, ref eSleepLeft);

            // BUG FIX: idem para o inimigo — limpar eStatus quando eSleepLeft chega a 0.
            if (eStatus == StatusEffectType.Sleep && eSleepLeft == 0)
                eStatus = StatusEffectType.None;

            if (!enemySkipped)
            {
                enemyMove = enemyMoves.PickMove();
                enemyMoves.UseMove(enemyMove);
                enemyDmg = CalcDamage(enemyMove, enemy.Attack, player.Defense, player.PrimaryType);
                pHp -= enemyDmg;

                if (enemyMove.StatusEffect != StatusEffectType.None && pStatus == StatusEffectType.None)
                {
                    if (_random.NextDouble() < enemyMove.StatusChance)
                    {
                        pStatus = enemyMove.StatusEffect;
                        if (pStatus == StatusEffectType.Sleep)
                            pSleepLeft = _sleepDurationRounds;
                        Log.Debug("Round {R}: Enemy inflicted {Status} on player", round + 1, pStatus);
                    }
                }
            }

            playerMoves.TickCooldowns();
            enemyMoves.TickCooldowns();

            // Gerar texto do round para o typewriter
            messages.Add(BuildRoundText(playerSkipped, playerMove, playerDmg, pStatus,
                                        enemySkipped, enemyMove, enemyDmg, eStatus));

            if (pHp <= 0) break;
        }

        pHp = Math.Max(0, pHp);
        eHp = Math.Max(0, eHp);

        // Determinar vencedor: quem tem mais HP% restante
        var pRatio = player.MaxHp > 0 ? (double)pHp / player.MaxHp : 0;
        var eRatio = enemy.MaxHp > 0 ? (double)eHp / enemy.MaxHp : 0;
        var playerWins = pRatio >= eRatio;

        // Empate: coin flip
        if (Math.Abs(pRatio - eRatio) < 0.01)
            playerWins = _random.NextDouble() >= 0.5;

        return new CombatResult(playerWins, pHp, eHp, pStatus, eStatus);
    }

    /// <summary>
    /// Calcula dano de um move: BaseDamage × (atk/def) × efetividade de tipo × variância × crit.
    /// </summary>
    private int CalcDamage(MoveDefinition move, int attack, int defense, Pokebar.Core.Models.PokemonType defenderType)
    {
        if (move.BaseDamage <= 0) return 0;

        // Efetividade de tipo (0 = imune, 0.5 = pouco efetivo, 1 = normal, 2 = super efetivo)
        var typeMultiplier = Pokebar.Core.Models.TypeChart.GetMultiplier(move.Type, defenderType);
        if (typeMultiplier == 0.0) return 0;

        var safeDef = Math.Max(1, defense);
        var raw = move.BaseDamage * ((double)attack / safeDef);
        raw *= typeMultiplier;

        // Variância: ±damageVariance
        var variance = 1.0 + ((_random.NextDouble() * 2 - 1) * _damageVariance);
        raw *= variance;

        // Crit
        if (_random.NextDouble() < move.CritChance)
            raw *= 2.0;

        return Math.Max(1, (int)Math.Round(raw));
    }

    /// <summary>
    /// Verifica se o turno é pulado por efeito de status.
    /// Sleep: sempre pula, decrementa turnos restantes, limpa ao expirar.
    /// Paralysis: chance de pular.
    /// </summary>
    private bool IsSkippedByStatus(StatusEffectType status, ref int sleepLeft)
    {
        switch (status)
        {
            case StatusEffectType.Sleep:
                if (sleepLeft > 0)
                {
                    sleepLeft--;
                    return true;
                }
                return false; // sono expirou

            case StatusEffectType.Paralysis:
                return _random.NextDouble() < _paralysisSkipChance;

            default:
                return false;
        }
    }

    /// <summary>
    /// Monta a string de texto de um round para exibição via typewriter.
    /// Formato compacto estilo GBA: "Tackle! / Growl~ ☠"
    /// </summary>
    private static string BuildRoundText(
        bool playerSkipped, MoveDefinition? playerMove, int playerDmg, StatusEffectType newEnemyStatus,
        bool enemySkipped, MoveDefinition? enemyMove, int enemyDmg, StatusEffectType newPlayerStatus)
    {
        var parts = new System.Text.StringBuilder();

        // Ação do player
        if (playerSkipped)
            parts.Append("...");
        else if (playerMove != null)
        {
            parts.Append(playerMove.Name);
            if (playerDmg > 0) parts.Append('!');
            if (newEnemyStatus == StatusEffectType.Poison) parts.Append(" ☠");
            else if (newEnemyStatus == StatusEffectType.Sleep) parts.Append(" 💤");
            else if (newEnemyStatus == StatusEffectType.Paralysis) parts.Append(" ⚡");
        }

        parts.Append(" / ");

        // Ação do inimigo
        if (enemySkipped)
            parts.Append("...");
        else if (enemyMove != null)
        {
            parts.Append(enemyMove.Name);
            if (enemyDmg > 0) parts.Append('!');
            if (newPlayerStatus == StatusEffectType.Poison) parts.Append(" ☠");
            else if (newPlayerStatus == StatusEffectType.Sleep) parts.Append(" 💤");
            else if (newPlayerStatus == StatusEffectType.Paralysis) parts.Append(" ⚡");
        }
        else
        {
            parts.Append("---");
        }

        return parts.ToString();
    }

    private static void RestoreAfterCombat(PokemonPet pet, EntityState previousState, double previousVelocity)
    {
        pet.VelocityX = previousVelocity;

        if (previousState == EntityState.Walking)
            pet.StartWalking();
        else
            pet.StartIdle(); // Idle é o estado seguro padrão pós-combate
    }

    private readonly record struct CombatResult(
        bool PlayerWins,
        int PlayerHpRemaining,
        int EnemyHpRemaining,
        StatusEffectType PlayerStatus,
        StatusEffectType EnemyStatus);

    private sealed class CombatSession
    {
        public CombatSession(PlayerPet player, EnemyPet enemy, double roundDuration, int rounds)
        {
            Player = player;
            Enemy = enemy;
            RoundDuration = roundDuration;
            Rounds = rounds;
        }

        public PlayerPet Player { get; }
        public EnemyPet Enemy { get; }
        public double RoundDuration { get; }
        public int Rounds { get; }
        public double Elapsed { get; set; }
        public double TotalDuration => RoundDuration * Rounds;
        public double PlayerPrevVelocity { get; set; }
        public double EnemyPrevVelocity { get; set; }
        public EntityState PlayerPrevState { get; set; }
        public EntityState EnemyPrevState { get; set; }

        /// <summary>Mensagens geradas por round para o typewriter.</summary>
        public List<string> RoundMessages { get; } = new();

        /// <summary>Índice da última mensagem já disparada via RoundMessage event.</summary>
        public int LastFiredMessageIndex { get; set; } = -1;

        /// <summary>Resultado pré-calculado pela simulação (evita re-simular no Resolve).</summary>
        public CombatResult PrecomputedResult { get; set; }
    }
}
