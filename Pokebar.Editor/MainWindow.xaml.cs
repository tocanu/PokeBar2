using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pokebar.Core.Models;
using Pokebar.Core.Serialization;

namespace Pokebar.Editor;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public ObservableCollection<PokemonEntry> Entries { get; } = new();

    private readonly Dictionary<int, OffsetAdjustment> _adjustments = new();
    private readonly List<string> _finalTargets;
    private readonly List<string> _runtimeTargets;
    private readonly string _finalPath;
    private readonly string _runtimePath;
    private PokemonEntry? _currentEntry;

    private string _rawPath = string.Empty;
    private string _status = string.Empty;
    private ImageSource? _previewImage;
    private string _selectedEntryText = "Selecione um Pokémon";
    private string _selectedSpritePath = string.Empty;
    private double _groundLineY;
    private int _selectedGroundOffset;
    private int _selectedCenterOffset;
    private int _selectedFrameHeight;
    private int _selectedStripIndex;
    private bool _selectedReviewed;
    private double _hitboxX;
    private double _hitboxY;
    private double _hitboxWidth;
    private double _hitboxHeight;
    private int _segmentHeight;
    private int _segmentsCount;

    public string RawPath { get => _rawPath; private set => SetField(ref _rawPath, value); }
    public string Status { get => _status; private set => SetField(ref _status, value); }
    public ImageSource? PreviewImage { get => _previewImage; private set => SetField(ref _previewImage, value); }
    public string SelectedEntryText { get => _selectedEntryText; private set => SetField(ref _selectedEntryText, value); }
    public string SelectedSpritePath { get => _selectedSpritePath; private set => SetField(ref _selectedSpritePath, value); }
    public double GroundLineY { get => _groundLineY; private set => SetField(ref _groundLineY, value); }
    public int SelectedGroundOffset { get => _selectedGroundOffset; set { if (SetField(ref _selectedGroundOffset, value)) UpdateGroundLineY(); } }
    public int SelectedCenterOffset { get => _selectedCenterOffset; set => SetField(ref _selectedCenterOffset, value); }
    public int SelectedFrameHeight { get => _selectedFrameHeight; private set => SetField(ref _selectedFrameHeight, value); }
    public int SelectedStripIndex { get => _selectedStripIndex; set { if (SetField(ref _selectedStripIndex, value)) UpdateGroundLineY(); } }
    public bool SelectedReviewed { get => _selectedReviewed; set => SetField(ref _selectedReviewed, value); }
    public double HitboxX { get => _hitboxX; set => SetField(ref _hitboxX, value); }
    public double HitboxY { get => _hitboxY; set => SetField(ref _hitboxY, value); }
    public double HitboxWidth { get => _hitboxWidth; set => SetField(ref _hitboxWidth, value); }
    public double HitboxHeight { get => _hitboxHeight; set => SetField(ref _hitboxHeight, value); }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        (_finalTargets, _runtimeTargets) = ResolveTargets();
        _finalPath = _finalTargets.First();
        _runtimePath = _runtimeTargets.First();
        Reload();
    }

    private void Reload_Click(object sender, RoutedEventArgs e) => Reload();
    private void SaveCurrent_Click(object sender, RoutedEventArgs e) => SaveCurrentAdjustment();
    private void ExportAll_Click(object sender, RoutedEventArgs e) => ExportAll();

    private void DataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        SnapshotCurrent();
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is PokemonEntry entry)
        {
            LoadPreview(entry);
        }
    }

    private void Reload()
    {
        try
        {
            LoadAdjustmentsFromDisk();
            Entries.Clear();
            var rawDir = FindRawDir();
            RawPath = rawDir;

            var files = Directory.GetFiles(rawDir, "pokemon_*_raw.json");
            foreach (var file in files)
            {
                var meta = MetadataJson.DeserializeFromFile(file);
                if (meta is null) continue;
                Entries.Add(new PokemonEntry(meta));
            }

            Status = $"Carregado: {Entries.Count} registros";
        }
        catch (Exception ex)
        {
            Status = $"Erro: {ex.Message}";
        }

        OnAllPropertiesChanged();
    }

    private void LoadPreview(PokemonEntry entry)
    {
        _currentEntry = entry;
        SelectedEntryText = $"Dex {entry.DexNumber:D4} - {entry.Species}";

        if (_adjustments.TryGetValue(entry.DexNumber, out var adj))
        {
            SelectedGroundOffset = adj.GroundOffsetY;
            SelectedCenterOffset = adj.CenterOffsetX;
            SelectedReviewed = adj.Reviewed;
            HitboxX = adj.HitboxX;
            HitboxY = adj.HitboxY;
            HitboxWidth = adj.HitboxWidth;
            HitboxHeight = adj.HitboxHeight;
        }
        else
        {
            SelectedGroundOffset = entry.GroundOffsetY;
            SelectedCenterOffset = entry.CenterOffsetX;
            SelectedReviewed = false;
            HitboxX = 0;
            HitboxY = 0;
            HitboxWidth = entry.FrameWidth ?? 0;
            HitboxHeight = entry.FrameHeight ?? 0;
        }

        SelectedFrameHeight = entry.FrameHeight ?? 0;
        SelectedStripIndex = 0;
        PreviewImage = null;
        SelectedSpritePath = string.Empty;
        UpdateGroundLineY(SelectedGroundOffset);

        try
        {
            var sprite = ResolveSpritePath(entry.DexNumber, entry.PrimarySpriteFile, entry.SpriteFiles);
            if (sprite is not null && File.Exists(sprite))
            {
                SelectedSpritePath = sprite;
                var sheet = new BitmapImage();
                sheet.BeginInit();
                sheet.CacheOption = BitmapCacheOption.OnLoad;
                sheet.UriSource = new Uri(sprite);
                sheet.EndInit();

                BuildPreview(sheet, SelectedGroundOffset, out var frameH, out var frameW);
                SelectedFrameHeight = frameH;

                if (!_adjustments.ContainsKey(entry.DexNumber))
                {
                    HitboxWidth = frameW;
                    HitboxHeight = frameH;
                    HitboxX = 0;
                    HitboxY = 0;
                }
            }
            else
            {
                SelectedSpritePath = "Sprite não encontrado";
            }
        }
        catch (Exception ex)
        {
            SelectedSpritePath = $"Erro: {ex.Message}";
        }

        OnAllPropertiesChanged();
    }

    private void GroundSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        SelectedGroundOffset = (int)e.NewValue;
        SnapshotCurrent();
    }

    private void CenterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        SelectedCenterOffset = (int)e.NewValue;
        SnapshotCurrent();
    }

    private void HitboxSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        SnapshotCurrent();
    }

    private void SaveCurrentAdjustment()
    {
        if (_currentEntry is null) return;
        _adjustments[_currentEntry.DexNumber] = new OffsetAdjustment(
            DexNumber: _currentEntry.DexNumber,
            GroundOffsetY: SelectedGroundOffset,
            CenterOffsetX: SelectedCenterOffset,
            Reviewed: SelectedReviewed,
            HitboxX: (int)Math.Round(HitboxX),
            HitboxY: (int)Math.Round(HitboxY),
            HitboxWidth: (int)Math.Round(HitboxWidth),
            HitboxHeight: (int)Math.Round(HitboxHeight));
        PersistAdjustments($"Salvo Dex {_currentEntry.DexNumber:D4}");
        OnAllPropertiesChanged();
    }

    private void ExportAll()
    {
        PersistAdjustments("Exportado");
        OnAllPropertiesChanged();
    }

    private void BuildPreview(BitmapSource sheet, int groundOffset, out int frameH, out int frameW)
    {
        var rows = 8;
        var cols = Enumerable.Range(1, Math.Min(12, sheet.PixelWidth))
            .Where(c => sheet.PixelWidth % c == 0)
            .OrderByDescending(c => c)
            .FirstOrDefault();
        if (cols == 0) cols = 1;

        frameH = sheet.PixelHeight / rows;
        frameW = sheet.PixelWidth / cols;

        var targetRows = new List<int> { 2, 6 };
        var validRows = targetRows.Where(r => r < rows).ToList();
        if (validRows.Count == 0) validRows = new List<int> { 0 };

        _segmentHeight = frameH;
        _segmentsCount = validRows.Count == 0 ? 1 : validRows.Count;

        var totalHeight = frameH * validRows.Count;
        if (totalHeight == 0) totalHeight = frameH;
        var totalWidth = frameW * cols;

        UpdateGroundLineY(groundOffset);

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            double destY = 0;
            foreach (var r in validRows)
            {
                var y = r * frameH;
                var rect = new Int32Rect(0, y, totalWidth, frameH);
                if (rect.X + rect.Width > sheet.PixelWidth || rect.Y + rect.Height > sheet.PixelHeight) continue;
                var crop = new CroppedBitmap(sheet, rect);
                dc.DrawImage(crop, new Rect(0, destY, rect.Width, rect.Height));
                destY += rect.Height;
            }
        }

        var rtb = new RenderTargetBitmap(totalWidth, totalHeight, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        PreviewImage = rtb;
    }

    private void UpdateGroundLineY(int? groundOverride = null)
    {
        var ground = groundOverride ?? SelectedGroundOffset;
        if (_segmentHeight <= 0) return;
        var segmentIndex = Math.Min(SelectedStripIndex, Math.Max(0, _segmentsCount - 1));
        GroundLineY = _segmentHeight * (segmentIndex + 1) - ground;
    }

    private void LoadAdjustmentsFromDisk()
    {
        _adjustments.Clear();
        var loadedPath = string.Empty;
        try
        {
            foreach (var path in _finalTargets.Concat(_runtimeTargets))
            {
                if (File.Exists(path))
                {
                    var loaded = FinalOffsets.Load(path);
                    foreach (var kvp in loaded)
                    {
                        _adjustments[kvp.Key] = kvp.Value;
                    }
                    loadedPath = path;
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Status = $"Erro ao carregar ajustes: {ex.Message}";
            return;
        }

        if (!string.IsNullOrEmpty(loadedPath))
        {
            Status = $"Ajustes carregados: {_adjustments.Count} ({loadedPath})";
        }
    }

    private IReadOnlyList<OffsetAdjustment> BuildRecords()
    {
        return Entries.Select(e =>
        {
            if (_adjustments.TryGetValue(e.DexNumber, out var adj))
            {
                return adj with { };
            }

            return new OffsetAdjustment(
                DexNumber: e.DexNumber,
                GroundOffsetY: e.GroundOffsetY,
                CenterOffsetX: e.CenterOffsetX,
                Reviewed: false,
                HitboxX: 0,
                HitboxY: 0,
                HitboxWidth: e.FrameWidth ?? 0,
                HitboxHeight: e.FrameHeight ?? 0);
        }).OrderBy(r => r.DexNumber).ToList();
    }

    private void PersistAdjustments(string successMessage)
    {
        try
        {
            var records = BuildRecords();
            var json = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
            var finalDirs = _finalTargets
                .Select(Path.GetDirectoryName)
                .Where(d => !string.IsNullOrEmpty(d))
                .Distinct();
            foreach (var dir in finalDirs)
            {
                Directory.CreateDirectory(dir!);
            }
            foreach (var path in _finalTargets)
            {
                File.WriteAllText(path, json);
            }
            var runtimeDirs = _runtimeTargets
                .Select(Path.GetDirectoryName)
                .Where(d => !string.IsNullOrEmpty(d))
                .Distinct();
            foreach (var dir in runtimeDirs)
            {
                Directory.CreateDirectory(dir!);
            }
            foreach (var path in _runtimeTargets)
            {
                File.WriteAllText(path, json);
            }

            Status = $"{successMessage}: gravado em {string.Join(" | ", _finalTargets)} (total {records.Count})";
            LoadAdjustmentsFromDisk();
        }
        catch (Exception ex)
        {
            Status = $"Erro ao salvar ajustes: {ex.Message}";
        }
    }

    private static (List<string> finalTargets, List<string> runtimeTargets) ResolveTargets()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Assets", "Final")), // repo raiz (quando executa de bin/)
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Assets", "Final")),        // fallback (Pokebar.Editor/Assets/Final)
            Path.GetFullPath(Path.Combine("Assets", "Final"))                                   // relativo ao cwd
        };

        var finals = candidates.Select(p => Path.Combine(p, "pokemon_offsets_final.json")).Distinct().ToList();
        var runtimes = candidates.Select(p => Path.Combine(p, "pokemon_offsets_runtime.json")).Distinct().ToList();
        return (finals, runtimes);
    }

    private void SnapshotCurrent()
    {
        if (_currentEntry is null) return;
        _adjustments[_currentEntry.DexNumber] = new OffsetAdjustment(
            DexNumber: _currentEntry.DexNumber,
            GroundOffsetY: SelectedGroundOffset,
            CenterOffsetX: SelectedCenterOffset,
            Reviewed: SelectedReviewed,
            HitboxX: (int)Math.Round(HitboxX),
            HitboxY: (int)Math.Round(HitboxY),
            HitboxWidth: (int)Math.Round(HitboxWidth),
            HitboxHeight: (int)Math.Round(HitboxHeight));
        PersistAdjustments("Auto-salvo");
    }

    private static string? ResolveSpritePath(int dex, string? primaryFile, IReadOnlyList<string> emotes)
    {
        var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "SpriteCollab", "sprite", dex.ToString("D4")));
        if (!Directory.Exists(baseDir))
        {
            var alt = Path.GetFullPath(Path.Combine("SpriteCollab", "sprite", dex.ToString("D4")));
            if (!Directory.Exists(alt)) return null;
            baseDir = alt;
        }

        var order = new List<string>();
        if (!string.IsNullOrEmpty(primaryFile)) order.Add(primaryFile);
        order.AddRange(new[] { "Walk-Anim.png", "Idle-Anim.png", "Sleep.png" });
        foreach (var name in order)
        {
            var p = Path.Combine(baseDir, name);
            if (File.Exists(p)) return p;
        }

        foreach (var name in emotes)
        {
            var p = Path.Combine(baseDir, name);
            if (File.Exists(p)) return p;
        }

        return null;
    }

    private static string FindRawDir()
    {
        var baseDir = AppContext.BaseDirectory;
        var raw = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Assets", "Raw"));
        if (Directory.Exists(raw)) return raw;

        var current = Path.GetFullPath("Assets/Raw");
        if (Directory.Exists(current)) return current;
        throw new DirectoryNotFoundException("Assets/Raw não encontrado.");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnAllPropertiesChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public class PokemonEntry
{
    public int DexNumber { get; }
    public string Species { get; }
    public bool HasWalk { get; }
    public bool HasIdle { get; }
    public bool HasSleep { get; }
    public int EmoteCount { get; }
    public string FrameSize { get; }
    public string Grid { get; }
    public int GroundOffsetY { get; }
    public int CenterOffsetX { get; }
    public BodyTypeSuggestion BodyType { get; }
    public string Notes { get; }
    public IReadOnlyList<string> SpriteFiles { get; }
    public string? PrimarySpriteFile { get; }
    public int? FrameHeight { get; }
    public int? FrameWidth { get; }

    public PokemonEntry(PokemonSpriteMetadata meta)
    {
        DexNumber = meta.DexNumber;
        Species = meta.Species;
        HasWalk = meta.Animations.HasWalk;
        HasIdle = meta.Animations.HasIdle;
        HasSleep = meta.Animations.HasSleep;
        EmoteCount = meta.Animations.Emotes.Count;
        SpriteFiles = meta.Animations.Emotes;
        PrimarySpriteFile = meta.Walk.FileName ?? meta.Idle.FileName ?? meta.Sleep.FileName;

        FrameHeight = meta.Walk.Frame?.Height ?? meta.Idle.Frame?.Height ?? meta.Sleep.Frame?.Height;
        FrameWidth = meta.Walk.Frame?.Width ?? meta.Idle.Frame?.Width ?? meta.Sleep.Frame?.Width;

        FrameSize = meta.Walk.Frame is not null
            ? $"{meta.Walk.Frame.Width}x{meta.Walk.Frame.Height}"
            : meta.Idle.Frame is not null
                ? $"{meta.Idle.Frame.Width}x{meta.Idle.Frame.Height}"
                : meta.Sleep.Frame is not null
                    ? $"{meta.Sleep.Frame.Width}x{meta.Sleep.Frame.Height}"
                    : "-";

        Grid = meta.Walk.Grid is not null
            ? $"{meta.Walk.Grid.Columns}x{meta.Walk.Grid.Rows}"
            : meta.Idle.Grid is not null
                ? $"{meta.Idle.Grid.Columns}x{meta.Idle.Grid.Rows}"
                : meta.Sleep.Grid is not null
                    ? $"{meta.Sleep.Grid.Columns}x{meta.Sleep.Grid.Rows}"
                    : "-";

        GroundOffsetY = meta.Offsets.GroundOffsetY;
        CenterOffsetX = meta.Offsets.CenterOffsetX;
        BodyType = meta.BodyType;
        Notes = string.Join(" | ", meta.Notes ?? Array.Empty<string>());
    }
}
