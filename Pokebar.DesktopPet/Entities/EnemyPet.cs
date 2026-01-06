using System;

namespace Pokebar.DesktopPet.Entities;

public class EnemyPet : PokemonPet
{
    private readonly Random _random;
    private double _patrolTimer;
    private double _nextDirectionChange;

    public EnemyPet(int dex, int level = 1, int? maxHp = null, int? seed = null) : base(dex)
    {
        Level = Math.Max(1, level);
        MaxHp = maxHp ?? BuildStat(12, Level, dex);
        CurrentHp = MaxHp;
        Attack = BuildStat(6, Level, dex);
        Defense = BuildStat(6, Level, dex);
        PatrolSpeed = 30.0;
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
        _nextDirectionChange = NextChangeSeconds();
    }

    public int Level { get; }
    public int MaxHp { get; private set; }
    public int CurrentHp { get; private set; }
    public int Attack { get; }
    public int Defense { get; }
    public double PatrolSpeed { get; set; }
    public bool IsCaptureInProgress { get; private set; }

    public bool IsCapturable => State == EntityState.Fainted;

    public override void Update(double deltaTime)
    {
        UpdatePatrol(deltaTime);
        base.Update(deltaTime);
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || State == EntityState.Dead || State == EntityState.Captured)
            return;

        CurrentHp = Math.Max(0, CurrentHp - damage);
        if (CurrentHp == 0)
        {
            State = EntityState.Fainted;
            VelocityX = 0;
            StartIdle(false);
        }
    }

    public void MarkCaptured()
    {
        State = EntityState.Captured;
        VelocityX = 0;
        IsCaptureInProgress = false;
        StartIdle(false);
    }

    public void Despawn()
    {
        State = EntityState.Dead;
        VelocityX = 0;
    }

    public void BeginCapture()
    {
        if (State == EntityState.Fainted)
            IsCaptureInProgress = true;
    }

    private void UpdatePatrol(double deltaTime)
    {
        if (State != EntityState.Idle && State != EntityState.Walking)
            return;

        _patrolTimer += deltaTime;
        if (_patrolTimer < _nextDirectionChange)
            return;

        _patrolTimer = 0;
        _nextDirectionChange = NextChangeSeconds();

        if (VelocityX == 0)
        {
            VelocityX = _random.Next(0, 2) == 0 ? -PatrolSpeed : PatrolSpeed;
            StartWalking();
        }
        else
        {
            VelocityX = -VelocityX;
            StartWalking();
        }
    }

    private double NextChangeSeconds()
    {
        return 2 + (_random.NextDouble() * 3);
    }

    private static int BuildStat(int baseValue, int level, int dex)
    {
        return Math.Max(1, baseValue + level + (dex % 5));
    }
}
