using System.Windows.Media.Imaging;

namespace Pokebar.DesktopPet.Animation;

/// <summary>
/// Player de animaÃ§Ã£o que gerencia o estado atual e transiÃ§Ãµes
/// </summary>
public class AnimationPlayer
{
    private AnimationClip? _currentClip;
    private int _currentFrame;
    private double _elapsedTime;
    private bool _isPlaying;

    public AnimationClip? CurrentClip => _currentClip;
    public int CurrentFrame => _currentFrame;
    public bool IsPlaying => _isPlaying;

    public event Action<BitmapSource, double>? FrameChanged;

    public void Play(AnimationClip clip, bool restart = false)
    {
        if (_currentClip == clip && !restart && _isPlaying)
            return;

        _currentClip = clip;
        _currentFrame = 0;
        _elapsedTime = 0;
        _isPlaying = true;

        EmitCurrentFrame();
    }

    public void Stop()
    {
        _isPlaying = false;
    }

    public void Pause()
    {
        _isPlaying = false;
    }

    public void Resume()
    {
        _isPlaying = true;
    }

    public void Update(double deltaTime)
    {
        if (!_isPlaying || _currentClip == null)
            return;

        _elapsedTime += deltaTime;

        while (_elapsedTime >= _currentClip.FrameTime)
        {
            _elapsedTime -= _currentClip.FrameTime;
            _currentFrame++;

            if (_currentFrame >= _currentClip.FrameCount)
            {
                if (_currentClip.Loop)
                {
                    _currentFrame = 0;
                }
                else
                {
                    _currentFrame = _currentClip.FrameCount - 1;
                    _isPlaying = false;
                }
            }

            EmitCurrentFrame();
        }
    }

    private void EmitCurrentFrame()
    {
        if (_currentClip == null || _currentClip.FrameCount == 0)
            return;

        var (frame, groundLineY) = _currentClip.GetFrame(_currentFrame);
        FrameChanged?.Invoke(frame, groundLineY);
    }
}
