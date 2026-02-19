using System;
using Pokebar.Core.Models;
using Pokebar.DesktopPet.Entities;
using Serilog;

namespace Pokebar.DesktopPet.Services;

/// <summary>
/// Gerencia movimento inteligente do pet: pontos de descanso, evitar bordas,
/// pausas periódicas e contexto de velocidade baseado em humor.
/// </summary>
public class SmartMovementService
{
    private readonly Random _random = new();
    private double _pauseTimer;
    private double _nextPauseTime;
    private double _pauseDuration;
    private bool _isPaused;
    private double _restPointX = double.NaN;

    // Constantes de timing
    private const double MIN_WALK_BEFORE_PAUSE = 5.0;
    private const double MAX_WALK_BEFORE_PAUSE = 18.0;
    private const double MIN_PAUSE_DURATION = 1.5;
    private const double MAX_PAUSE_DURATION = 4.0;
    private const double EDGE_SOFTZONE_PX = 100.0;
    private const double EDGE_SLOWDOWN_FACTOR = 0.4;

    public SmartMovementService()
    {
        ScheduleNextPause();
    }

    /// <summary>Se o pet está numa pausa de descanso voluntária.</summary>
    public bool IsResting => _isPaused;

    /// <summary>
    /// Atualiza o sistema de movimento inteligente.
    /// Retorna o multiplicador de velocidade a aplicar (-1 a 1 = normal, 0 = parado).
    /// </summary>
    public double Update(PlayerPet player, double deltaTime, MoodType mood,
        double leftBound, double rightBound)
    {
        // Não interferir com combate, sleeping, special idle
        if (player.State == EntityState.Fighting || player.State == EntityState.Sleeping ||
            player.State == EntityState.SpecialIdle || player.State == EntityState.Fainted ||
            player.State == EntityState.Captured || player.State == EntityState.Dead)
        {
            _isPaused = false;
            return 1.0;
        }

        // Se parado em pausa de descanso
        if (_isPaused)
        {
            _pauseTimer += deltaTime;
            if (_pauseTimer >= _pauseDuration)
            {
                _isPaused = false;
                ScheduleNextPause();
                return 1.0;
            }
            return 0.0;
        }

        // Se andando, contar tempo até próxima pausa
        if (player.State == EntityState.Walking)
        {
            _pauseTimer += deltaTime;
            if (_pauseTimer >= _nextPauseTime)
            {
                StartPause(player, mood);
                return 0.0;
            }
        }

        // Calcular slowdown perto das bordas
        var edgeFactor = CalculateEdgeSlowdown(player.X, leftBound, rightBound);

        // Aplicar modificador de humor na velocidade
        var moodFactor = mood switch
        {
            MoodType.Happy => 1.15,
            MoodType.Sad => 0.75,
            MoodType.Sleepy => 0.6,
            _ => 1.0
        };

        return edgeFactor * moodFactor;
    }

    /// <summary>Define um ponto de descanso preferencial (centro da taskbar, por exemplo).</summary>
    public void SetRestPoint(double x)
    {
        _restPointX = x;
    }

    /// <summary>Interrompe qualquer pausa ativa e volta ao movimento.</summary>
    public void CancelPause()
    {
        _isPaused = false;
        ScheduleNextPause();
    }

    private void StartPause(PlayerPet player, MoodType mood)
    {
        _isPaused = true;
        _pauseTimer = 0;

        // Humor afeta duração da pausa
        var baseDuration = MIN_PAUSE_DURATION + (_random.NextDouble() * (MAX_PAUSE_DURATION - MIN_PAUSE_DURATION));
        _pauseDuration = mood switch
        {
            MoodType.Sleepy => baseDuration * 1.5,
            MoodType.Sad => baseDuration * 1.3,
            MoodType.Happy => baseDuration * 0.7,
            _ => baseDuration
        };

        player.StartIdle();
        Log.Debug("SmartMovement: Pause for {Duration:F1}s (mood: {Mood})", _pauseDuration, mood);
    }

    private void ScheduleNextPause()
    {
        _pauseTimer = 0;
        _nextPauseTime = MIN_WALK_BEFORE_PAUSE + (_random.NextDouble() * (MAX_WALK_BEFORE_PAUSE - MIN_WALK_BEFORE_PAUSE));
    }

    private double CalculateEdgeSlowdown(double x, double leftBound, double rightBound)
    {
        var distFromLeft = x - leftBound;
        var distFromRight = rightBound - x;
        var minDist = Math.Min(distFromLeft, distFromRight);

        if (minDist < EDGE_SOFTZONE_PX && minDist > 0)
        {
            // Interpolar suavemente — mais perto = mais lento
            var t = minDist / EDGE_SOFTZONE_PX;
            return EDGE_SLOWDOWN_FACTOR + ((1.0 - EDGE_SLOWDOWN_FACTOR) * t);
        }

        return 1.0;
    }
}
