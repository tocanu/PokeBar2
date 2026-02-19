using System;
using Pokebar.Core.Models;
using Pokebar.DesktopPet.Entities;
using Serilog;

namespace Pokebar.DesktopPet.Services;

/// <summary>
/// Gerencia comportamentos idle do pet: bocejar, deitar, olhar ao redor, dormir.
/// Ativados após tempo de ociosidade, influenciados pelo humor.
/// </summary>
public class IdleBehaviorService
{
    private readonly Random _random = new();
    private double _idleTimer;
    private double _behaviorTimer;
    private double _behaviorDuration;
    private bool _inBehavior;
    private readonly double _minIdleBeforeBehavior;
    private readonly double _maxIdleBeforeBehavior;

    /// <summary>Tempo mínimo idle antes de iniciar um comportamento (s).</summary>
    private const double MIN_IDLE_TIME = 8.0;
    /// <summary>Tempo máximo idle antes de forçar um comportamento (s).</summary>
    private const double MAX_IDLE_TIME = 20.0;
    /// <summary>Duração de comportamentos loop (sit, lay, sleep) antes de voltar (s).</summary>
    private const double MIN_BEHAVIOR_DURATION = 5.0;
    private const double MAX_BEHAVIOR_DURATION = 15.0;
    /// <summary>Duração mínima de sleep (muito mais longo).</summary>
    private const double MIN_SLEEP_DURATION = 20.0;
    private const double MAX_SLEEP_DURATION = 60.0;

    public IdleBehaviorService()
    {
        _minIdleBeforeBehavior = MIN_IDLE_TIME;
        _maxIdleBeforeBehavior = MAX_IDLE_TIME;
        ResetIdleTimer();
    }

    /// <summary>Se está atualmente executando um comportamento idle especial.</summary>
    public bool IsInBehavior => _inBehavior;

    /// <summary>
    /// Atualiza o sistema de idle behaviors.
    /// Deve ser chamado a cada frame do game loop.
    /// </summary>
    public void Update(PlayerPet player, double deltaTime, MoodType mood)
    {
        // Se o player está em combate, captura, etc — resetar tudo
        if (player.State == EntityState.Fighting || player.State == EntityState.Fainted ||
            player.State == EntityState.Captured || player.State == EntityState.Dead)
        {
            Cancel(player);
            return;
        }

        // Se está num comportamento ativo, contar duração
        if (_inBehavior)
        {
            _behaviorTimer += deltaTime;
            
            // Se a animação parou (non-loop terminou) ou tempo acabou, voltar ao normal
            if (_behaviorTimer >= _behaviorDuration || 
                (!player.AnimationPlayer.IsPlaying && player.State == EntityState.SpecialIdle))
            {
                EndBehavior(player);
            }
            return;
        }

        // Se está andando, resetar timer
        if (player.State == EntityState.Walking)
        {
            _idleTimer = 0;
            return;
        }

        // Contar tempo idle
        if (player.State == EntityState.Idle)
        {
            _idleTimer += deltaTime;

            // Ajustar threshold baseado no humor
            var threshold = GetIdleThreshold(mood);
            if (_idleTimer >= threshold)
            {
                TryStartBehavior(player, mood);
            }
        }
    }

    /// <summary>Cancela qualquer comportamento ativo e volta ao idle/walking.</summary>
    public void Cancel(PlayerPet player)
    {
        if (_inBehavior)
        {
            _inBehavior = false;
            _behaviorTimer = 0;
            if (player.State == EntityState.Sleeping || player.State == EntityState.SpecialIdle)
            {
                player.StartIdle();
            }
        }
        _idleTimer = 0;
    }

    /// <summary>Interrompe o comportamento atual e faz o pet voltar a andar.</summary>
    public void Interrupt(PlayerPet player)
    {
        if (_inBehavior)
        {
            _inBehavior = false;
            _behaviorTimer = 0;
        }
        _idleTimer = 0;
    }

    private void TryStartBehavior(PlayerPet player, MoodType mood)
    {
        if (!player.HasBehaviorAnimations)
        {
            _idleTimer = 0; // Resetar para não ficar tentando
            return;
        }

        bool started;
        bool isSleep = false;

        // Humor Sleepy favorece dormir
        if (mood == MoodType.Sleepy && player.HasSleepAnimation && _random.NextDouble() < 0.6)
        {
            started = player.StartSleeping();
            isSleep = true;
        }
        // Humor Sad favorece deitar/sentar
        else if (mood == MoodType.Sad && _random.NextDouble() < 0.5)
        {
            started = player.HasLayAnimation ? player.StartLaying() : player.StartSitting();
        }
        // Humor Happy favorece hop/pose
        else if (mood == MoodType.Happy && _random.NextDouble() < 0.4)
        {
            started = player.HasHopAnimation ? player.StartHopping() : player.StartPosing();
        }
        else
        {
            started = player.StartRandomIdleBehavior(_random);
            isSleep = player.State == EntityState.Sleeping;
        }

        if (started)
        {
            _inBehavior = true;
            _behaviorTimer = 0;
            _behaviorDuration = isSleep
                ? MIN_SLEEP_DURATION + (_random.NextDouble() * (MAX_SLEEP_DURATION - MIN_SLEEP_DURATION))
                : MIN_BEHAVIOR_DURATION + (_random.NextDouble() * (MAX_BEHAVIOR_DURATION - MIN_BEHAVIOR_DURATION));
            _idleTimer = 0;
            Log.Debug("IdleBehavior: Started {State} for {Duration:F1}s (mood: {Mood})",
                player.State, _behaviorDuration, mood);
        }
        else
        {
            _idleTimer = 0; // Tentar novamente depois
        }
    }

    private void EndBehavior(PlayerPet player)
    {
        _inBehavior = false;
        _behaviorTimer = 0;
        _idleTimer = 0;

        // Voltar ao idle
        if (player.State == EntityState.Sleeping || player.State == EntityState.SpecialIdle)
        {
            player.StartIdle();
        }

        Log.Debug("IdleBehavior: Ended, returning to Idle");
    }

    private double GetIdleThreshold(MoodType mood)
    {
        var baseThreshold = _minIdleBeforeBehavior + 
            (_random.NextDouble() * (_maxIdleBeforeBehavior - _minIdleBeforeBehavior));

        return mood switch
        {
            MoodType.Sleepy => baseThreshold * 0.5,  // Fica sonolento mais rápido
            MoodType.Sad => baseThreshold * 0.7,      // Senta/deita mais cedo
            MoodType.Happy => baseThreshold * 1.3,    // Mais ativo, demora mais
            _ => baseThreshold
        };
    }

    private void ResetIdleTimer()
    {
        _idleTimer = 0;
        _behaviorTimer = 0;
        _inBehavior = false;
    }
}
