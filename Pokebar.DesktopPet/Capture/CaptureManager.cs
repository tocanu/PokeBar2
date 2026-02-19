using System;
using Pokebar.Core.Models;
using Pokebar.DesktopPet.Entities;
using Serilog;

namespace Pokebar.DesktopPet.Capture;

public class CaptureManager
{
    private readonly double _travelDuration;
    private readonly double _absorbDuration;
    private readonly double _shakeDuration;
    private readonly int _shakeCount;
    private readonly double _shakeAmplitude;
    private readonly GameplayConfig _config;
    private readonly double _baseSuccessRate;
    private readonly Random _random = new();
    private CaptureSequence? _active;
    private bool _hidden;

    public CaptureManager(
        GameplayConfig config,
        double travelDuration = 0.6,
        double absorbDuration = 0.5,
        double shakeDuration = 0.25,
        int shakeCount = 3,
        double shakeAmplitude = 6)
    {
        _config = config;
        _travelDuration = travelDuration;
        _absorbDuration = absorbDuration;
        _shakeDuration = shakeDuration;
        _shakeCount = Math.Max(1, shakeCount);
        _shakeAmplitude = shakeAmplitude;
        _baseSuccessRate = Math.Clamp(config.Capture.BaseSuccessRate, 0.0, 1.0);
    }

    public bool IsActive => _active != null;

    public event Action<EnemyPet>? CaptureCompleted;
    public event Action<EnemyPet>? CaptureFailed;

    public bool TryStartCapture(PlayerPet player, EnemyPet enemy, PetWindow? enemyWindow, double dpiScale,
        double playerScreenCenterX = 0, double playerScreenCenterY = 0)
    {
        if (enemy.State != EntityState.Fainted || enemy.IsCaptureInProgress)
            return false;

        // Consumir pokeball - sem bola, sem captura
        if (!player.TryConsumePokeball())
        {
            Log.Debug("Capture blocked: no Pokéballs available (Player has {Pokeballs})", player.Pokeballs);
            return false;
        }

        Log.Information("Capture attempt started: Player throwing Pokéball at enemy Dex {EnemyDex} (Pokéballs remaining: {Pokeballs})", 
            enemy.Dex, player.Pokeballs);
        enemy.BeginCapture();

        if (enemyWindow == null)
        {
            Log.Information("Capture completed instantly (no window). Enemy Dex: {Dex}", enemy.Dex);
            enemy.MarkCaptured();
            CaptureCompleted?.Invoke(enemy);
            return true;
        }

        enemyWindow.SetCaptureScale(1);
        var ballWindow = new CaptureBallWindow(_config);

        // Usar posições de tela reais (DIPs) lidas diretamente das janelas
        var (enemyCenterX, enemyCenterY) = enemyWindow.GetScreenCenter();
        var enemyGroundY = enemyWindow.GetScreenGroundY();

        Log.Debug("Capture ball: Player({PlayerX:F0},{PlayerY:F0}) → Enemy({EnemyX:F0},{EnemyY:F0}), BallSize={Size}",
            playerScreenCenterX, playerScreenCenterY, enemyCenterX, enemyCenterY, _config.Capture.BallSizePx);

        // Posicionar a bola ANTES de Show para garantir posição inicial correta
        ballWindow.UpdatePosition(playerScreenCenterX, playerScreenCenterY);
        ballWindow.Show();
        ballWindow.SetHidden(_hidden);

        var sequence = new CaptureSequence(enemy, enemyWindow, ballWindow)
        {
            StartX = playerScreenCenterX,
            StartY = playerScreenCenterY,
            TargetX = enemyCenterX,
            TargetY = enemyCenterY,
            DropY = enemyGroundY
        };

        _active = sequence;
        return true;
    }

    public void Update(double deltaTime)
    {
        if (_active == null)
            return;

        _active.Elapsed += deltaTime;

        switch (_active.Phase)
        {
            case CapturePhase.Travel:
                UpdateTravel();
                break;
            case CapturePhase.Absorb:
                UpdateAbsorb();
                break;
            case CapturePhase.Shake:
                UpdateShake();
                break;
            case CapturePhase.Done:
                FinishCapture();
                break;
        }
    }

    public void SetHidden(bool hidden)
    {
        _hidden = hidden;
        if (_active?.BallWindow != null)
            _active.BallWindow.SetHidden(hidden);
    }

    public void Shutdown()
    {
        if (_active == null)
            return;

        _active.BallWindow.Close();
        _active = null;
    }

    private void UpdateTravel()
    {
        if (_active == null)
            return;

        var progress = Math.Clamp(_active.Elapsed / _travelDuration, 0, 1);
        var x = Lerp(_active.StartX, _active.TargetX, progress);
        var y = Lerp(_active.StartY, _active.TargetY, progress);

        // Arco parabólico: bola sobe e desce durante Travel (altura máx no meio)
        var arcHeight = 60.0; // DIPs de altura do arco
        var arc = -4.0 * arcHeight * progress * (progress - 1.0); // parábola: 0 → arcHeight → 0
        y -= arc;

        _active.BallWindow.UpdatePosition(x, y);
        _active.BallWindow.EnsureTopmost();

        if (progress >= 1)
        {
            _active.Elapsed = 0;
            _active.Phase = CapturePhase.Absorb;
        }
    }

    private void UpdateAbsorb()
    {
        if (_active == null)
            return;

        var progress = Math.Clamp(_active.Elapsed / _absorbDuration, 0, 1);
        var scale = 1 - progress;
        _active.EnemyWindow.SetCaptureScale(scale);
        _active.BallWindow.UpdatePosition(_active.TargetX, _active.TargetY);
        _active.BallWindow.EnsureTopmost();

        if (progress >= 1)
        {
            _active.EnemyWindow.SetHidden(true);
            _active.Elapsed = 0;
            _active.Phase = CapturePhase.Shake;
        }
    }

    private void UpdateShake()
    {
        if (_active == null)
            return;

        _active.ShakeElapsed += _active.Elapsed;
        _active.Elapsed = 0;

        var totalShake = _shakeDuration * _shakeCount;
        if (_active.ShakeElapsed >= totalShake)
        {
            _active.Phase = CapturePhase.Done;
            return;
        }

        var phase = _active.ShakeElapsed / _shakeDuration;
        var shakeIndex = (int)Math.Floor(phase);
        var local = phase - shakeIndex;
        var direction = (shakeIndex % 2 == 0) ? -1 : 1;
        var offset = Math.Sin(local * Math.PI) * _shakeAmplitude * direction;
        var x = _active.TargetX + offset;
        _active.BallWindow.UpdatePosition(x, _active.DropY);
        _active.BallWindow.EnsureTopmost();
    }

    private void FinishCapture()
    {
        if (_active == null)
            return;

        // Verificar taxa de sucesso
        var success = _random.NextDouble() < _baseSuccessRate;

        if (success)
        {
            Log.Information("Capture successful! Enemy Dex {Dex} captured (rate: {Rate:P0})", _active.Enemy.Dex, _baseSuccessRate);
            _active.Enemy.MarkCaptured();
            _active.BallWindow.Close();
            CaptureCompleted?.Invoke(_active.Enemy);
        }
        else
        {
            Log.Information("Capture failed! Enemy Dex {Dex} broke free (rate: {Rate:P0})", _active.Enemy.Dex, _baseSuccessRate);
            _active.Enemy.IsCaptureInProgress = false;
            _active.EnemyWindow.SetCaptureScale(1);
            _active.EnemyWindow.SetHidden(false);
            _active.BallWindow.Close();
            CaptureFailed?.Invoke(_active.Enemy);
        }

        _active = null;
    }

    private static double Lerp(double from, double to, double t) => from + ((to - from) * t);

    private sealed class CaptureSequence
    {
        public CaptureSequence(EnemyPet enemy, PetWindow enemyWindow, CaptureBallWindow ballWindow)
        {
            Enemy = enemy;
            EnemyWindow = enemyWindow;
            BallWindow = ballWindow;
        }

        public EnemyPet Enemy { get; }
        public PetWindow EnemyWindow { get; }
        public CaptureBallWindow BallWindow { get; }
        public CapturePhase Phase { get; set; } = CapturePhase.Travel;
        public double Elapsed { get; set; }
        public double ShakeElapsed { get; set; }
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double TargetX { get; set; }
        public double TargetY { get; set; }
        public double DropY { get; set; }
    }

    private enum CapturePhase
    {
        Travel = 0,
        Absorb = 1,
        Shake = 2,
        Done = 3
    }
}
