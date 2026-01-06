using System;
using System.Collections.Generic;
using System.Windows;
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
    private CombatSession? _active;
    private double _cooldownRemaining;

    public CombatManager(
        double collisionTolerance = 20,
        double spacing = 50,
        double roundDuration = 3,
        int rounds = 3,
        double retreatDistance = 200,
        double cooldownDuration = 1.5)
    {
        _collisionTolerance = collisionTolerance;
        _spacing = spacing;
        _roundDuration = roundDuration;
        _rounds = Math.Max(1, rounds);
        _retreatDistance = retreatDistance;
        _cooldownDuration = cooldownDuration;
    }

    public bool IsActive => _active != null;

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
        if (_active.Elapsed >= _active.TotalDuration)
        {
            ResolveCombat();
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

        // Posiciona frente a frente com espaÃ§amento
        var mid = (player.X + enemy.X) / 2;
        player.X = mid - (_spacing / 2);
        enemy.X = mid + (_spacing / 2);

        // Define direÃ§Ãµes para se encararem (ANTES de trocar estado/animaÃ§Ã£o)
        player.FacingRight = player.X < enemy.X;
        enemy.FacingRight = enemy.X < player.X;

        // Agora inicia o combate (troca estado e animaÃ§Ã£o)
        player.StartFighting();
        enemy.StartFighting();
    }

    private void ResolveCombat()
    {
        if (_active == null)
            return;

        var player = _active.Player;
        var enemy = _active.Enemy;

        var playerScore = ComputeScore(player.MaxHp, player.Attack, enemy.Defense);
        var enemyScore = ComputeScore(enemy.MaxHp, enemy.Attack, player.Defense);

        var playerWins = playerScore > enemyScore;        Log.Information("Combat resolved: {Winner} wins! Scores - Player: {PlayerScore}, Enemy: {EnemyScore}",
            playerWins ? "Player" : "Enemy", playerScore, enemyScore);        if (!playerWins && Math.Abs(playerScore - enemyScore) < 0.01)
        {
            playerWins = _random.NextDouble() >= 0.5;
        }

        if (playerWins)
        {
            enemy.TakeDamage(enemy.MaxHp);
            RestoreAfterCombat(player, _active.PlayerPrevState, _active.PlayerPrevVelocity);
        }
        else
        {
            player.X += player.FacingRight ? -_retreatDistance : _retreatDistance;
            RestoreAfterCombat(player, _active.PlayerPrevState, _active.PlayerPrevVelocity);
            RestoreAfterCombat(enemy, _active.EnemyPrevState, _active.EnemyPrevVelocity);
        }

        _active = null;
        _cooldownRemaining = _cooldownDuration;
    }

    private static void RestoreAfterCombat(PokemonPet pet, EntityState previousState, double previousVelocity)
    {
        pet.VelocityX = previousVelocity;

        if (previousState == EntityState.Idle)
        {
            pet.StartIdle();
        }
        else if (previousState == EntityState.Walking)
        {
            pet.StartWalking();
        }
        else
        {
            pet.StartWalking();
        }
    }

    private static double ComputeScore(int hp, int attack, int defense)
    {
        var safeDefense = Math.Max(1, defense);
        return (hp * attack) / (double)safeDefense;
    }

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
    }
}
