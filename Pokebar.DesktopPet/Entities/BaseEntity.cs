using System.Windows;
using System.Windows.Media.Imaging;
using Pokebar.DesktopPet.Animation;

namespace Pokebar.DesktopPet.Entities;

public abstract class BaseEntity
{
    private double _frameWidth;
    private double _frameHeight;
    private double _frameGroundLine;

    protected BaseEntity(int dex, string formId = "0000")
    {
        Dex = dex;
        FormId = formId;
        UniqueId = new Pokebar.Core.Models.PokemonVariant(dex, formId).UniqueId;
        AnimationPlayer = new AnimationPlayer();
        AnimationPlayer.FrameChanged += OnFrameChanged;
        State = EntityState.Idle;
    }

    public int Dex { get; protected set; }
    public string FormId { get; protected set; }
    public string UniqueId { get; protected set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double VelocityX { get; set; }
    public double VelocityY { get; set; }
    public bool FacingRight { get; set; } = true;
    public EntityState State { get; protected set; }
    public AnimationPlayer AnimationPlayer { get; }

    public double FrameWidth => _frameWidth;
    public double FrameHeight => _frameHeight;
    public double FrameGroundLine => _frameGroundLine;

    public double HitboxX { get; protected set; }
    public double HitboxY { get; protected set; }
    public double HitboxWidth { get; protected set; }
    public double HitboxHeight { get; protected set; }

    public virtual void Update(double deltaTime)
    {
        AnimationPlayer.Update(deltaTime);

        X += VelocityX * deltaTime;
        Y += VelocityY * deltaTime;

        // Não mudar direção durante combate ou captura
        if (State != EntityState.Fighting && State != EntityState.Captured)
        {
            if (VelocityX > 0)
                FacingRight = true;
            else if (VelocityX < 0)
                FacingRight = false;
        }
    }

    public Rect GetBounds()
    {
        if (_frameWidth <= 0 || _frameHeight <= 0)
            return Rect.Empty;

        var left = X - (_frameWidth / 2);
        var top = Y - _frameHeight;
        return new Rect(left, top, _frameWidth, _frameHeight);
    }

    public Rect GetHitbox()
    {
        var bounds = GetBounds();
        if (bounds.IsEmpty)
            return bounds;

        if (HitboxWidth <= 0 || HitboxHeight <= 0)
            return bounds;

        var left = bounds.Left + HitboxX;
        var top = bounds.Top + HitboxY;
        return new Rect(left, top, HitboxWidth, HitboxHeight);
    }

    public double GetTopY()
    {
        if (_frameHeight <= 0)
            return Y;
        return Y - _frameGroundLine;
    }

    public double GetCenterY()
    {
        return GetTopY() + (_frameHeight / 2);
    }

    protected void SetHitbox(double x, double y, double width, double height)
    {
        HitboxX = x;
        HitboxY = y;
        HitboxWidth = width;
        HitboxHeight = height;
    }

    private void OnFrameChanged(BitmapSource frame, double groundLineY)
    {
        _frameWidth = frame.PixelWidth;
        _frameHeight = frame.PixelHeight;
        _frameGroundLine = groundLineY;
    }
}
