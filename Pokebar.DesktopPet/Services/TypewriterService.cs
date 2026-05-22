using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Serilog;

namespace Pokebar.DesktopPet.Services;

/// <summary>
/// Serviço GBA-style typewriter text effect.
/// Revela texto caractere por caractere com cursor piscante ▼.
/// Usado em diálogos, speech bubbles e notificações.
/// </summary>
public sealed class TypewriterService
{
    /// <summary>Default chars per second (GBA speed).</summary>
    private const double DEFAULT_CPS = 24.0;

    /// <summary>Cursor blink interval in ms.</summary>
    private const int CURSOR_BLINK_MS = 500;

    private readonly DispatcherTimer _charTimer;
    private readonly DispatcherTimer _cursorTimer;

    private TextBlock? _targetTextBlock;
    private TextBlock? _cursorBlock;
    private string _fullText = "";
    private int _charIndex;
    private double _charsPerSecond;
    private bool _cursorVisible;
    private bool _isTyping;

    /// <summary>Fires when typewriter finishes revealing all text.</summary>
    public event Action? TypewriterComplete;

    /// <summary>Fires for each character revealed (for SFX triggers).</summary>
    public event Action? CharRevealed;

    public TypewriterService()
    {
        _charsPerSecond = DEFAULT_CPS;

        _charTimer = new DispatcherTimer(DispatcherPriority.Render);
        _charTimer.Tick += OnCharTick;
        UpdateCharInterval();

        _cursorTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(CURSOR_BLINK_MS)
        };
        _cursorTimer.Tick += OnCursorBlink;
    }

    public bool IsTyping => _isTyping;
    public double CharsPerSecond
    {
        get => _charsPerSecond;
        set
        {
            _charsPerSecond = Math.Max(1, value);
            UpdateCharInterval();
        }
    }

    /// <summary>
    /// Start typewriter effect on a TextBlock pair.
    /// </summary>
    /// <param name="target">TextBlock that will show the revealed text.</param>
    /// <param name="text">Full text to reveal.</param>
    /// <param name="cursorBlock">Optional TextBlock for blinking ▼ cursor.</param>
    public void Start(TextBlock target, string text, TextBlock? cursorBlock = null)
    {
        Stop();

        _targetTextBlock = target;
        _cursorBlock = cursorBlock;
        _fullText = text ?? "";
        _charIndex = 0;
        _isTyping = true;

        _targetTextBlock.Text = "";

        if (_cursorBlock != null)
        {
            _cursorBlock.Text = "▼";
            _cursorBlock.Visibility = Visibility.Visible;
            _cursorVisible = true;
            _cursorTimer.Start();
        }

        if (_fullText.Length > 0)
        {
            _charTimer.Start();
        }
        else
        {
            FinishTyping();
        }
    }

    /// <summary>
    /// Instantly reveal all remaining text (user pressed button / skip).
    /// </summary>
    public void Skip()
    {
        if (!_isTyping || _targetTextBlock == null) return;

        _charTimer.Stop();
        _targetTextBlock.Text = _fullText;
        FinishTyping();
    }

    /// <summary>Stop and reset all state.</summary>
    public void Stop()
    {
        _charTimer.Stop();
        _cursorTimer.Stop();
        _isTyping = false;
        _charIndex = 0;
        _fullText = "";

        if (_cursorBlock != null)
            _cursorBlock.Visibility = Visibility.Collapsed;
    }

    private void OnCharTick(object? sender, EventArgs e)
    {
        if (_targetTextBlock == null || _charIndex >= _fullText.Length)
        {
            _charTimer.Stop();
            FinishTyping();
            return;
        }

        _charIndex++;
        _targetTextBlock.Text = _fullText[.._charIndex];
        CharRevealed?.Invoke();
    }

    private void OnCursorBlink(object? sender, EventArgs e)
    {
        if (_cursorBlock == null) return;
        _cursorVisible = !_cursorVisible;
        _cursorBlock.Visibility = _cursorVisible ? Visibility.Visible : Visibility.Hidden;
    }

    private void FinishTyping()
    {
        _charTimer.Stop();
        _isTyping = false;

        // Keep cursor blinking after done (GBA style — waits for button press)
        // Caller can call Stop() to hide it.

        try { TypewriterComplete?.Invoke(); }
        catch (Exception ex) { Log.Warning(ex, "TypewriterComplete handler error"); }
    }

    private void UpdateCharInterval()
    {
        _charTimer.Interval = TimeSpan.FromSeconds(1.0 / _charsPerSecond);
    }
}
