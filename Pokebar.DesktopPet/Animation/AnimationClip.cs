using System.Windows.Media.Imaging;

namespace Pokebar.DesktopPet.Animation;

/// <summary>
/// Representa um clip de animaÃ§Ã£o (walk, idle, sleep, etc)
/// ContÃ©m todos os frames e metadados da animaÃ§Ã£o
/// </summary>
public class AnimationClip
{
    public string Name { get; }
    public List<BitmapSource> Frames { get; }
    public List<double> FrameGroundLines { get; }
    public double FrameTime { get; }
    public bool Loop { get; }

    public int FrameCount => Frames.Count;
    public double TotalDuration => FrameCount * FrameTime;

    public AnimationClip(string name, List<BitmapSource> frames, List<double> frameGroundLines, double frameTime = 0.1, bool loop = true)
    {
        Name = name;
        Frames = frames;
        FrameGroundLines = frameGroundLines;
        FrameTime = frameTime;
        Loop = loop;
    }

    public (BitmapSource Frame, double GroundLineY) GetFrame(int index)
    {
        if (Frames.Count == 0)
            throw new InvalidOperationException("AnimationClip has no frames");

        index = Math.Clamp(index, 0, Frames.Count - 1);
        return (Frames[index], FrameGroundLines[index]);
    }
}
