using System.IO;
using System.Media;
using Serilog;

namespace Pokebar.DesktopPet.Services;

/// <summary>
/// Serviço de efeitos sonoros GBA-style para a UI.
/// Gera sons curtos (beeps, confirmações) de forma programática usando WAV PCM.
/// Todos os sons são sintetizados em memória — nenhum arquivo externo necessário.
/// </summary>
public sealed class UiSfxService : IDisposable
{
    // ── GBA-authentic frequencies (Hz) ──
    private const int FREQ_SELECT   = 880;    // A5  — menu cursor move
    private const int FREQ_CONFIRM  = 1047;   // C6  — button confirm
    private const int FREQ_CANCEL   = 440;    // A4  — cancel/back
    private const int FREQ_ERROR    = 220;    // A3  — error buzz
    private const int FREQ_CAPTURE  = 1319;   // E6  — capture success fanfare note
    private const int FREQ_TYPECHAR = 1760;   // A6  — typewriter char tick

    // ── Durations (ms) ──
    private const int DUR_SELECT    = 50;
    private const int DUR_CONFIRM   = 80;
    private const int DUR_CANCEL    = 100;
    private const int DUR_ERROR     = 150;
    private const int DUR_CAPTURE   = 120;
    private const int DUR_TYPECHAR  = 15;

    // ── Volume (0.0 to 1.0) ──
    private const double VOL_DEFAULT = 0.25;

    private readonly Dictionary<SfxType, byte[]> _wavCache = new();
    private bool _enabled = true;
    private double _volume = VOL_DEFAULT;

    public enum SfxType
    {
        Select,     // Menu cursor / hover
        Confirm,    // Button press / OK
        Cancel,     // Back / ESC
        Error,      // Invalid action
        Capture,    // Pokéball capture success
        TypeChar    // Typewriter character tick
    }

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public double Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0.0, 1.0);
    }

    public UiSfxService()
    {
        PreGenerateAll();
    }

    /// <summary>Play a UI sound effect.</summary>
    public void Play(SfxType type)
    {
        if (!_enabled) return;

        try
        {
            if (_wavCache.TryGetValue(type, out var wavData))
            {
                using var ms = new MemoryStream(wavData);
                using var player = new SoundPlayer(ms);
                player.Play(); // async, non-blocking
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "SFX play failed: {Type}", type);
        }
    }

    /// <summary>Play the select/cursor SFX (convenience).</summary>
    public void PlaySelect() => Play(SfxType.Select);
    public void PlayConfirm() => Play(SfxType.Confirm);
    public void PlayCancel() => Play(SfxType.Cancel);
    public void PlayError() => Play(SfxType.Error);
    public void PlayCapture() => Play(SfxType.Capture);
    public void PlayTypeChar() => Play(SfxType.TypeChar);

    private void PreGenerateAll()
    {
        _wavCache[SfxType.Select]   = GenerateSquareWav(FREQ_SELECT,   DUR_SELECT,   _volume);
        _wavCache[SfxType.Confirm]  = GenerateSquareWav(FREQ_CONFIRM,  DUR_CONFIRM,  _volume);
        _wavCache[SfxType.Cancel]   = GenerateDescendWav(FREQ_CANCEL,  DUR_CANCEL,   _volume);
        _wavCache[SfxType.Error]    = GenerateBuzzWav(FREQ_ERROR,      DUR_ERROR,     _volume);
        _wavCache[SfxType.Capture]  = GenerateChirpWav(FREQ_CAPTURE,   DUR_CAPTURE,  _volume);
        _wavCache[SfxType.TypeChar] = GenerateSquareWav(FREQ_TYPECHAR, DUR_TYPECHAR, _volume * 0.5);
    }

    /// <summary>Generate a square wave WAV in memory (GBA-authentic sound).</summary>
    private static byte[] GenerateSquareWav(int freq, int durationMs, double volume)
    {
        const int sampleRate = 22050; // GBA-like sample rate
        int numSamples = sampleRate * durationMs / 1000;
        int samplesPerCycle = sampleRate / freq;
        var samples = new byte[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            // Square wave: high or low based on cycle position
            bool high = (i % samplesPerCycle) < (samplesPerCycle / 2);
            // Apply volume envelope with fade-out on last 20%
            double env = i > numSamples * 0.8 
                ? volume * (1.0 - (double)(i - numSamples * 0.8) / (numSamples * 0.2))
                : volume;
            samples[i] = (byte)(128 + (high ? 1 : -1) * (int)(64 * env));
        }

        return WrapInWav(samples, sampleRate);
    }

    /// <summary>Generate a descending tone (for cancel SFX).</summary>
    private static byte[] GenerateDescendWav(int startFreq, int durationMs, double volume)
    {
        const int sampleRate = 22050;
        int numSamples = sampleRate * durationMs / 1000;
        var samples = new byte[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / numSamples;
            double freq = startFreq * (1.0 - t * 0.5); // descend by half
            double phase = 2.0 * Math.PI * freq * i / sampleRate;
            double env = volume * (1.0 - t);
            // Square-ish by using sign of sine
            double val = Math.Sign(Math.Sin(phase));
            samples[i] = (byte)(128 + (int)(48 * val * env));
        }

        return WrapInWav(samples, sampleRate);
    }

    /// <summary>Generate a short buzz (for error SFX).</summary>
    private static byte[] GenerateBuzzWav(int freq, int durationMs, double volume)
    {
        const int sampleRate = 22050;
        int numSamples = sampleRate * durationMs / 1000;
        var samples = new byte[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / numSamples;
            // Noise-modulated square wave for buzzy feel
            double phase = 2.0 * Math.PI * freq * i / sampleRate;
            double env = volume * (1.0 - t * 0.7);
            double noise = ((i * 7919 + 104729) % 256) / 256.0; // pseudo-random
            double val = Math.Sign(Math.Sin(phase)) * (0.7 + 0.3 * noise);
            samples[i] = (byte)(128 + (int)(48 * val * env));
        }

        return WrapInWav(samples, sampleRate);
    }

    /// <summary>Generate a chirp ascending tone (for capture SFX).</summary>
    private static byte[] GenerateChirpWav(int baseFreq, int durationMs, double volume)
    {
        const int sampleRate = 22050;
        int numSamples = sampleRate * durationMs / 1000;
        var samples = new byte[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / numSamples;
            double freq = baseFreq * (1.0 + t * 0.5); // ascend by 50%
            double phase = 2.0 * Math.PI * freq * i / sampleRate;
            double env = volume * Math.Sin(Math.PI * t); // bell curve envelope
            double val = Math.Sign(Math.Sin(phase));
            samples[i] = (byte)(128 + (int)(56 * val * env));
        }

        return WrapInWav(samples, sampleRate);
    }

    /// <summary>Wrap raw 8-bit unsigned PCM samples into a valid WAV file.</summary>
    private static byte[] WrapInWav(byte[] samples, int sampleRate)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        int dataSize = samples.Length;
        int fileSize = 36 + dataSize;

        // RIFF header
        bw.Write("RIFF"u8);
        bw.Write(fileSize);
        bw.Write("WAVE"u8);

        // fmt chunk
        bw.Write("fmt "u8);
        bw.Write(16);           // chunk size
        bw.Write((short)1);     // PCM format
        bw.Write((short)1);     // mono
        bw.Write(sampleRate);   // sample rate
        bw.Write(sampleRate);   // byte rate (8-bit mono = sampleRate)
        bw.Write((short)1);     // block align
        bw.Write((short)8);     // bits per sample

        // data chunk
        bw.Write("data"u8);
        bw.Write(dataSize);
        bw.Write(samples);

        return ms.ToArray();
    }

    public void Dispose()
    {
        _wavCache.Clear();
    }
}
