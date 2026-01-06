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
    private CaptureSequence? _active;
    private bool _hidden;

    public CaptureManager(
        GameplayConfig config,
        double travelDuration = 0.35,
        double absorbDuration = 0.35,
        double shakeDuration = 0.2,
        int shakeCount = 3,
        double shakeAmplitude = 4)
    {
        _config = config;
        _travelDuration = travelDuration;
        _absorbDuration = absorbDuration;
        _shakeDuration = shakeDuration;
        _shakeCount = Math.Max(1, shakeCount);
        _shakeAmplitude = shakeAmplitude;
    }

    public bool IsActive => _active != null;

    public event Action<EnemyPet>? CaptureCompleted;

    public bool TryStartCapture(PlayerPet player, EnemyPet enemy, PetWindow? enemyWindow, double dpiScale)
    {
        if (enemy.State != EntityState.Fainted || enemy.IsCaptureInProgress)
            return false;

        Log.Information("Capture attempt started: Player throwing Pokéball at enemy Dex {EnemyDex}", enemy.Dex);
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
        ballWindow.Show();
        ballWindow.SetHidden(_hidden);

        var sequence = new CaptureSequence(enemy, enemyWindow, ballWindow)
        {
            DpiScale = dpiScale,
            StartX = player.X,
            StartY = GetCenterY(player),
            TargetX = enemy.X,
            TargetY = GetCenterY(enemy),
            DropY = enemy.Y - (_config.Capture.BallSizePx / 2)
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
        _active.BallWindow.UpdatePosition(x, y, _active.DpiScale);

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
        _active.BallWindow.UpdatePosition(_active.TargetX, _active.TargetY, _active.DpiScale);

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
        _active.BallWindow.UpdatePosition(x, _active.DropY, _active.DpiScale);
    }

    private void FinishCapture()
    {
        if (_active == null)
            return;

        Log.Information("Capture successful! Enemy Dex {Dex} captured", _active.Enemy.Dex);
        _active.Enemy.MarkCaptured();
        _active.BallWindow.Close();
        CaptureCompleted?.Invoke(_active.Enemy);
        _active = null;
    }

    private double GetCenterY(BaseEntity entity)
    {
        var center = entity.GetCenterY();
        if (center <= 0)
            return entity.Y - _config.Capture.BallSizePx;
        return center;
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
        public double DpiScale { get; set; } = 1.0;
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
