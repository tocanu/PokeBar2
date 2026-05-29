using System;
using Pokebar.Core.Models;
using Pokebar.DesktopPet.Entities;
using Serilog;
using StatusEffectType = Pokebar.Core.Models.StatusEffectType;

namespace Pokebar.DesktopPet.Capture;

/// <summary>
/// Manages the full Pokéball capture sequence:
///   Travel  → arc flight from player to enemy, ball spinning
///   Absorb  → enemy shrinks, lid opens/closes, glow flash
///   Drop    → ball falls to ground with gravity, landing squish
///   Shake   → ball rocks left/right (3 shakes), scale recovery
///   Done    → success/fail resolution
/// </summary>
public class CaptureManager
{
    // ── Timings ────────────────────────────────────────────────────────────────
    private readonly double _travelDuration;    // arc flight
    private const double ABSORB_DURATION = 0.55;
    private const double DROP_DURATION   = 0.38;
    private const double SHAKE_DURATION  = 0.55; // per individual shake
    private const int    SHAKE_COUNT     = 3;

    // ── Shake parameters ──────────────────────────────────────────────────────
    private const double ROCK_AMPLITUDE  = 20.0; // degrees
    private const double SHIFT_AMPLITUDE =  5.0; // px horizontal offset during rock

    // ── State ─────────────────────────────────────────────────────────────────
    private readonly GameplayConfig _config;
    private readonly double _baseSuccessRate;
    private readonly Random _random = new();
    private CaptureSequence? _active;
    private bool _hidden;

    public CaptureManager(GameplayConfig config)
    {
        _config          = config;
        _travelDuration  = config.Capture.ShrinkDuration > 0 ? config.Capture.ShrinkDuration : 0.65;
        _baseSuccessRate = Math.Clamp(config.Capture.BaseSuccessRate, 0.0, 1.0);
    }

    public bool IsActive => _active != null;

    public event Action<EnemyPet>? CaptureCompleted;
    public event Action<EnemyPet>? CaptureFailed;

    // ── Public API ─────────────────────────────────────────────────────────────

    public bool TryStartCapture(PlayerPet player, EnemyPet enemy, PetWindow? enemyWindow,
        double dpiScale, double playerScreenCenterX = 0, double playerScreenCenterY = 0)
    {
        if (enemy.State != EntityState.Fainted || enemy.IsCaptureInProgress)
            return false;

        if (!player.TryConsumePokeball())
        {
            Log.Debug("Capture blocked: no Pokéballs (has {Pokeballs})", player.Pokeballs);
            return false;
        }

        Log.Information("Capture started: dex={Dex} pokéballs_left={Balls}",
            enemy.Dex, player.Pokeballs);
        enemy.BeginCapture();

        if (enemyWindow == null)
        {
            enemy.MarkCaptured();
            CaptureCompleted?.Invoke(enemy);
            return true;
        }

        enemyWindow.SetCaptureScale(1);
        var ballWindow = new CaptureBallWindow(_config);

        var (enemyCenterX, enemyCenterY) = enemyWindow.GetScreenCenter();
        var enemyGroundY = enemyWindow.GetScreenGroundY();

        ballWindow.UpdatePosition(playerScreenCenterX, playerScreenCenterY);
        ballWindow.Show();
        ballWindow.SetHidden(_hidden);

        _active = new CaptureSequence(enemy, enemyWindow, ballWindow)
        {
            StartX  = playerScreenCenterX,
            StartY  = playerScreenCenterY,
            TargetX = enemyCenterX,
            TargetY = enemyCenterY,
            DropY   = enemyGroundY,
        };
        return true;
    }

    public void Update(double deltaTime)
    {
        if (_active == null) return;
        _active.Elapsed += deltaTime;

        switch (_active.Phase)
        {
            case CapturePhase.Travel: UpdateTravel(deltaTime); break;
            case CapturePhase.Absorb: UpdateAbsorb();          break;
            case CapturePhase.Drop:   UpdateDrop(deltaTime);   break;
            case CapturePhase.Shake:  UpdateShake();           break;
            case CapturePhase.Done:   FinishCapture();         break;
        }
    }

    public void SetHidden(bool hidden)
    {
        _hidden = hidden;
        _active?.BallWindow.SetHidden(hidden);
    }

    public void Shutdown()
    {
        if (_active == null) return;
        _active.BallWindow.Close();
        _active = null;
    }

    // ── Phase: Travel ──────────────────────────────────────────────────────────
    // Ball flies on a parabolic arc from the player to the enemy, spinning.

    private void UpdateTravel(double deltaTime)
    {
        var progress = Math.Clamp(_active!.Elapsed / _travelDuration, 0, 1);

        var x = Lerp(_active.StartX, _active.TargetX, progress);
        var y = Lerp(_active.StartY, _active.TargetY, progress);

        // Parabolic arc: rises to peak at mid-flight then falls onto enemy
        var arcHeight = Math.Max(40.0, _config.Capture.Gravity / 8.0);
        y -= -4.0 * arcHeight * progress * (progress - 1.0);

        // Continuous spin: ~2.5 full rotations over travel duration
        _active.SpinAngle = (_active.SpinAngle + deltaTime * (900.0 / _travelDuration)) % 360.0;
        _active.BallWindow.SetSpinAngle(_active.SpinAngle);

        _active.BallWindow.UpdatePosition(x, y);
        _active.BallWindow.EnsureTopmost();

        if (progress >= 1.0)
        {
            _active.BallWindow.SetSpinAngle(0);
            _active.Elapsed = Math.Max(0, _active.Elapsed - _travelDuration);
            _active.Phase   = CapturePhase.Absorb;
        }
    }

    // ── Phase: Absorb ─────────────────────────────────────────────────────────
    // Lid opens, enemy shrinks and vanishes, glow flashes, lid closes.

    private void UpdateAbsorb()
    {
        var p = Math.Clamp(_active!.Elapsed / ABSORB_DURATION, 0.0, 1.0);

        // Enemy: scale 1→0 over the whole absorb window
        _active.EnemyWindow.SetCaptureScale(Math.Max(0, 1.0 - p));

        // Lid: open 0→0.45, hold flat, close 0.55→1.0
        double lidT;
        if (p < 0.45)
            lidT = Ease(p / 0.45);
        else if (p < 0.55)
            lidT = 1.0;
        else
            lidT = 1.0 - Ease((p - 0.55) / 0.45);
        _active.BallWindow.SetLidOpen(lidT);

        // Glow: ramps up then down, peak at p≈0.5
        var glow = Math.Max(0, Math.Sin(p * Math.PI));
        _active.BallWindow.SetGlow(glow * 0.85);

        // Scale pulse: ball swells slightly then snaps shut
        var pulse = Math.Sin(p * Math.PI) * 0.18;
        _active.BallWindow.SetBallScale(1.0 + pulse, 1.0 + pulse * 0.6);

        _active.BallWindow.UpdatePosition(_active.TargetX, _active.TargetY);
        _active.BallWindow.EnsureTopmost();

        if (p >= 1.0)
        {
            _active.EnemyWindow.SetHidden(true);
            _active.BallWindow.SetLidOpen(0);
            _active.BallWindow.SetGlow(0);
            _active.BallWindow.SetBallScale(1, 1);
            _active.Elapsed = Math.Max(0, _active.Elapsed - ABSORB_DURATION);
            _active.Phase   = CapturePhase.Drop;
        }
    }

    // ── Phase: Drop ───────────────────────────────────────────────────────────
    // Ball falls from enemy-centre down to the taskbar with gravity, then squishes.

    private void UpdateDrop(double deltaTime)
    {
        var p = Math.Clamp(_active!.Elapsed / DROP_DURATION, 0.0, 1.0);

        // Quadratic ease-in (accelerating gravity)
        var fallT = p * p;
        var y = Lerp(_active.TargetY, _active.DropY, fallT);

        // Slow trailing spin during fall (~¼ turn = 90° over DROP_DURATION seconds)
        _active.SpinAngle = (_active.SpinAngle + deltaTime * (90.0 / DROP_DURATION)) % 360.0;
        _active.BallWindow.SetSpinAngle(_active.SpinAngle);

        _active.BallWindow.UpdatePosition(_active.TargetX, y);
        _active.BallWindow.EnsureTopmost();

        if (p >= 1.0)
        {
            // Landing squish: wide and flat, will recover in Shake
            _active.BallWindow.SetBallScale(1.35, 0.70);
            _active.BallWindow.SetSpinAngle(0);
            _active.BallWindow.UpdatePosition(_active.TargetX, _active.DropY);
            _active.Elapsed = Math.Max(0, _active.Elapsed - DROP_DURATION);
            _active.Phase   = CapturePhase.Shake;
        }
    }

    // ── Phase: Shake ──────────────────────────────────────────────────────────
    // Ball rocks left–right N times; squish recovers during the first shake.

    private void UpdateShake()
    {
        _active!.ShakeElapsed += _active.Elapsed;
        _active.Elapsed = 0;

        var totalShake = SHAKE_DURATION * SHAKE_COUNT;

        if (_active.ShakeElapsed >= totalShake)
        {
            _active.BallWindow.SetRockAngle(0);
            _active.BallWindow.SetBallScale(1, 1);
            _active.Phase = CapturePhase.Done;
            return;
        }

        // Squish recovery: lerp back to (1,1) over first 0.18 s
        const double squishRecover = 0.18;
        if (_active.ShakeElapsed < squishRecover)
        {
            var t  = _active.ShakeElapsed / squishRecover;
            var sx = Lerp(1.35, 1.0, t);
            var sy = Lerp(0.70, 1.0, t);
            _active.BallWindow.SetBallScale(sx, sy);
        }
        else
        {
            _active.BallWindow.SetBallScale(1, 1);
        }

        // Rock oscillation for each shake
        var shakePhase = _active.ShakeElapsed / SHAKE_DURATION;
        var shakeIndex = (int)Math.Floor(shakePhase);
        var local      = shakePhase - shakeIndex;

        // Alternate direction; sine gives smooth ease-in/out pivot
        var dir       = (shakeIndex % 2 == 0) ? -1.0 : 1.0;
        var rockAngle = Math.Sin(local * Math.PI) * ROCK_AMPLITUDE * dir;
        var xOffset   = Math.Sin(local * Math.PI) * SHIFT_AMPLITUDE  * dir;

        _active.BallWindow.SetRockAngle(rockAngle);
        _active.BallWindow.UpdatePosition(_active.TargetX + xOffset, _active.DropY);
        _active.BallWindow.EnsureTopmost();
    }

    // ── Phase: Done ───────────────────────────────────────────────────────────

    private void FinishCapture()
    {
        if (_active == null) return;
        var enemy = _active.Enemy;

        // Capture formula: baseRate × hpFactor × statusBonus / rarityDifficulty
        var hpFactor = enemy.MaxHp > 0
            ? (3.0 * enemy.MaxHp - 2.0 * enemy.CurrentHp) / (3.0 * enemy.MaxHp)
            : 1.0;

        var statusBonus = enemy.ActiveStatus switch
        {
            StatusEffectType.Sleep                                        => _config.Capture.SleepCaptureBonus,
            StatusEffectType.Poison or StatusEffectType.Paralysis         => _config.Capture.StatusCaptureBonus,
            _                                                             => 1.0,
        };

        var rarityIndex  = (int)enemy.Rarity;
        var rarityDiff   = rarityIndex >= 0 && rarityIndex < _config.Capture.RarityDifficulty.Length
            ? _config.Capture.RarityDifficulty[rarityIndex] : 1.0;

        var captureChance = Math.Clamp(
            _baseSuccessRate * hpFactor * statusBonus / Math.Max(0.1, rarityDiff),
            0.05, 0.95);

        var roll    = _random.NextDouble();
        var success = roll < captureChance;

        Log.Information(
            "Capture roll: {Roll:F3} vs {Chance:F3} " +
            "(base={Base:F2} hp={Hp:F2} status={St}×{SB:F1} rarity={R}÷{RD:F1}) → {Result}",
            roll, captureChance, _baseSuccessRate, hpFactor,
            enemy.ActiveStatus, statusBonus, enemy.Rarity, rarityDiff,
            success ? "SUCCESS" : "FAILED");

        if (success)
        {
            _active.BallWindow.SetButtonSuccess();
            enemy.MarkCaptured();
            _active.BallWindow.Close();
            CaptureCompleted?.Invoke(enemy);
        }
        else
        {
            // Pop the lid open then restore enemy
            _active.BallWindow.SetLidOpen(1.0);
            enemy.IsCaptureInProgress = false;
            _active.EnemyWindow.SetCaptureScale(1);
            _active.EnemyWindow.SetHidden(false);
            _active.BallWindow.Close();
            CaptureFailed?.Invoke(enemy);
        }

        _active = null;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    /// <summary>Smooth-step ease: slow start, fast middle, slow end.</summary>
    private static double Ease(double t) => t * t * (3 - 2 * t);

    // ── Inner types ────────────────────────────────────────────────────────────

    private sealed class CaptureSequence
    {
        public CaptureSequence(EnemyPet enemy, PetWindow enemyWindow, CaptureBallWindow ballWindow)
        {
            Enemy       = enemy;
            EnemyWindow = enemyWindow;
            BallWindow  = ballWindow;
        }

        public EnemyPet          Enemy        { get; }
        public PetWindow          EnemyWindow  { get; }
        public CaptureBallWindow  BallWindow   { get; }
        public CapturePhase       Phase        { get; set; } = CapturePhase.Travel;
        public double             Elapsed      { get; set; }
        public double             ShakeElapsed { get; set; }

        // Travel/Drop
        public double StartX  { get; set; }
        public double StartY  { get; set; }
        public double TargetX { get; set; }
        public double TargetY { get; set; }
        public double DropY   { get; set; }
        public double SpinAngle { get; set; }
    }

    private enum CapturePhase
    {
        Travel = 0,
        Absorb = 1,
        Drop   = 2,   // ball falls to ground after absorbing enemy
        Shake  = 3,
        Done   = 4,
    }
}
