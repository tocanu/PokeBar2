using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Pokebar.Core;
using Pokebar.Core.Models;
using Pokebar.Core.Serialization;
using Pokebar.Core.Sprites;

namespace Pokebar.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly IReadOnlyDictionary<int, OffsetAdjustment> _offsets;
    private readonly List<BitmapSource> _frames = new();
    private readonly List<double> _frameGroundLines = new();
    private DispatcherTimer? _timer;
    private int _frameIndex;

    public string RuntimeInfo { get; }
    public string RuntimePath { get; }
    public string Status { get; private set; } = string.Empty;
    public string DexInput { get; set; } = "1";
    public ImageSource? PreviewImage { get; private set; }
    public double GroundLineY { get; private set; }

    public MainWindow()
    {
        InitializeComponent();

        var loadResult = LoadOffsets();
        _offsets = loadResult.Offsets;
        RuntimePath = loadResult.Path;
        RuntimeInfo = loadResult.Info;

        Status = _offsets.Values.Any(o => o.Reviewed)
            ? "Possui ajustes revisados."
            : "Sem revisados marcados.";

        DataContext = this;
    }

    private void LoadDex_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(DexInput, out var dex) || !DexConstants.IsValid(dex))
        {
            Status = "Dex invÃ¡lido.";
            OnPropertyChanged(nameof(Status));
            return;
        }

        try
        {
            LoadSprite(dex);
        }
        catch (Exception ex)
        {
            Status = $"Erro: {ex.Message}";
            PreviewImage = null;
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(PreviewImage));
        }
    }

    private void LoadSprite(int dex)
    {
        _timer?.Stop();
        _frames.Clear();
        _frameGroundLines.Clear();

        OffsetAdjustment? adj = null;
        if (_offsets.TryGetValue(dex, out var existing))
        {
            adj = existing;
        }

        var spritePath = ResolveSpritePath(dex, adj?.PrimarySpriteFile);
        if (spritePath is null)
        {
            Status = "Sprite nao encontrado.";
            PreviewImage = null;
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(PreviewImage));
            return;
        }

        var sheet = new BitmapImage();
        sheet.BeginInit();
        sheet.CacheOption = BitmapCacheOption.OnLoad;
        sheet.UriSource = new Uri(Path.GetFullPath(spritePath), UriKind.Absolute);
        sheet.EndInit();

        var preferStandardGrid = PreferStandardGrid(spritePath);
        SpriteGrid grid;
        FrameSize frame;
        if (adj is not null && TryGetStoredFrameGrid(adj, sheet, out var storedGrid, out var storedFrame))
        {
            grid = storedGrid;
            frame = storedFrame;
        }
        else
        {
            var buffer = BuildPixelBuffer(sheet);
            (grid, frame) = SpriteSheetAnalyzer.DetectGrid(buffer, preferStandardGrid);
        }

        var frameW = frame.Width;
        var frameH = frame.Height;
        var offset = adj ?? new OffsetAdjustment(
            dex,
            0,
            0,
            false,
            0,
            0,
            frameW,
            frameH,
            frameW,
            frameH,
            grid.Columns,
            grid.Rows,
            Path.GetFileName(spritePath));

        BuildAnimationFrames(sheet, grid, frame, offset, preferStandardGrid);

        if (_frames.Count == 0)
        {
            PreviewImage = null;
            GroundLineY = 0;
        }
        else
        {
            _frameIndex = 0;
            PreviewImage = _frames[_frameIndex];
            GroundLineY = _frameGroundLines[_frameIndex];
        }

        var visW = offset.HitboxWidth > 0 ? offset.HitboxWidth : frameW;
        var visH = offset.HitboxHeight > 0 ? offset.HitboxHeight : frameH;
        Status = $"Dex {dex:D4} - Frame {frameW}x{frameH} - OffsetY {offset.GroundOffsetY} CenterX {offset.CenterOffsetX} Hitbox {offset.HitboxWidth}x{offset.HitboxHeight}@({offset.HitboxX},{offset.HitboxY}) Vis {visW}x{visH}";

        OnPropertyChanged(nameof(PreviewImage));
        OnPropertyChanged(nameof(GroundLineY));
        OnPropertyChanged(nameof(Status));
    }

    private static string? ResolveSpritePath(int dex, string? primaryFile)
    {
        var baseDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "SpriteCollab", "sprite", dex.ToString("D4"));
        var alt = Path.Combine("SpriteCollab", "sprite", dex.ToString("D4"));
        var root = Directory.Exists(baseDir) ? baseDir : Directory.Exists(alt) ? alt : null;
        if (root is null) return null;

        var order = new List<string>();
        if (!string.IsNullOrEmpty(primaryFile)) order.Add(primaryFile);
        order.AddRange(new[] { "Walk-Anim.png", "Idle-Anim.png", "Sleep.png" });
        foreach (var name in order)
        {
            var p = Path.Combine(root, name);
            if (File.Exists(p)) return p;
        }
        return null;
    }

    private static (IReadOnlyDictionary<int, OffsetAdjustment> Offsets, string Path, string Info) LoadOffsets()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Assets", "Final")), // repo raiz
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Assets", "Final")),        // fallback local
            Path.GetFullPath(Path.Combine("Assets", "Final"))                                   // relativo ao cwd
        }.Distinct().ToList();

        foreach (var dir in candidates)
        {
            var finalPath = Path.Combine(dir, "pokemon_offsets_final.json");
            if (File.Exists(finalPath))
            {
                var offsets = FinalOffsets.Load(finalPath);
                var info = $"Usando offsets finais do editor ({offsets.Count} entradas).";
                return (offsets, finalPath, info);
            }
        }

        foreach (var dir in candidates)
        {
            var runtimePath = Path.Combine(dir, "pokemon_offsets_runtime.json");
            if (File.Exists(runtimePath))
            {
                var offsets = FinalOffsets.Load(runtimePath);
                var info = $"Usando offsets runtime ({offsets.Count} entradas).";
                return (offsets, runtimePath, info);
            }
        }

        return (new Dictionary<int, OffsetAdjustment>(), "(nÃ£o encontrado)", "Arquivo de offsets nÃ£o encontrado. Rode pipeline/editor.");
    }

    private void BuildAnimationFrames(BitmapSource sheet, SpriteGrid grid, FrameSize frame, OffsetAdjustment offset, bool preferStandardGrid)
    {
        var frameW = frame.Width;
        var frameH = frame.Height;
        var cols = grid.Columns;
        var rows = grid.Rows;

        var validRows = SelectPreviewRows(rows, preferStandardGrid);
        if (validRows.Count == 0) return;

        var hasHitbox = offset.HitboxWidth > 0 && offset.HitboxHeight > 0;
        var hx = Math.Clamp(offset.HitboxX, 0, frameW);
        var hy = Math.Clamp(offset.HitboxY, 0, frameH);
        var hw = Math.Clamp(hasHitbox ? offset.HitboxWidth : frameW, 1, frameW - hx);
        var hh = Math.Clamp(hasHitbox ? offset.HitboxHeight : frameH, 1, frameH - hy);
        var groundInCrop = (frameH - offset.GroundOffsetY) - hy;

        foreach (var row in validRows)
        {
            for (var col = 0; col < cols; col++)
            {
                var srcRect = new Int32Rect(col * frameW, row * frameH, frameW, frameH);
                if (srcRect.X + srcRect.Width > sheet.PixelWidth || srcRect.Y + srcRect.Height > sheet.PixelHeight) continue;
                var frameCrop = new CroppedBitmap(sheet, srcRect);
                var finalFrame = hasHitbox ? new CroppedBitmap(frameCrop, new Int32Rect(hx, hy, hw, hh)) : frameCrop;
                _frames.Add(finalFrame);
                _frameGroundLines.Add(groundInCrop);
            }
        }

        _timer = null;
        if (_frames.Count > 1)
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(140)
            };
            _timer.Tick += (_, _) =>
            {
                _frameIndex = (_frameIndex + 1) % _frames.Count;
                PreviewImage = _frames[_frameIndex];
                GroundLineY = _frameGroundLines[_frameIndex];
                OnPropertyChanged(nameof(PreviewImage));
                OnPropertyChanged(nameof(GroundLineY));
            };
            _timer.Start();
        }
    }

    private static List<int> SelectPreviewRows(int rows, bool preferStandardGrid)
    {
        var validRows = new List<int>();
        if (preferStandardGrid && rows > 6)
        {
            validRows.Add(2);
            validRows.Add(6);
            return validRows;
        }

        if (rows > 0) validRows.Add(0);
        return validRows;
    }

    private static bool PreferStandardGrid(string spritePath)
    {
        var name = Path.GetFileName(spritePath);
        if (string.Equals(name, "Walk-Anim.png", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(name, "Idle-Anim.png", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static PixelBuffer BuildPixelBuffer(BitmapSource source)
    {
        var bitsPerPixel = source.Format.BitsPerPixel;
        var bytesPerPixel = Math.Max(1, (bitsPerPixel + 7) / 8);
        var stride = (source.PixelWidth * bitsPerPixel + 7) / 8;
        var buffer = new byte[stride * source.PixelHeight];
        source.CopyPixels(buffer, stride, 0);
        return new PixelBuffer(buffer, source.PixelWidth, source.PixelHeight, stride, bytesPerPixel);
    }

    private static bool TryGetStoredFrameGrid(OffsetAdjustment offset, BitmapSource source, out SpriteGrid grid, out FrameSize frame)
    {
        var frameW = offset.FrameWidth;
        var frameH = offset.FrameHeight;
        var cols = offset.GridColumns;
        var rows = offset.GridRows;

        if (frameW is null || frameH is null || cols is null || rows is null)
        {
            grid = new SpriteGrid(1, 1);
            frame = new FrameSize(0, 0);
            return false;
        }

        if (frameW <= 0 || frameH <= 0 || cols <= 0 || rows <= 0)
        {
            grid = new SpriteGrid(1, 1);
            frame = new FrameSize(0, 0);
            return false;
        }

        if (frameW.Value * cols.Value > source.PixelWidth || frameH.Value * rows.Value > source.PixelHeight)
        {
            grid = new SpriteGrid(1, 1);
            frame = new FrameSize(0, 0);
            return false;
        }

        grid = new SpriteGrid(cols.Value, rows.Value);
        frame = new FrameSize(frameW.Value, frameH.Value);
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}













