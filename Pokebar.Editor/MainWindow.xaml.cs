using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Pokebar.Core.Models;
using Pokebar.Core.Serialization;
using Pokebar.Core.Sprites;

namespace Pokebar.Editor;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public ObservableCollection<PokemonEntry> Entries { get; } = new();

    private readonly Dictionary<string, OffsetAdjustment> _adjustments = new();
    private readonly List<string> _finalTargets;
    private readonly List<string> _runtimeTargets;
    private readonly string _finalPath;
    private readonly string _runtimePath;
    private PokemonEntry? _currentEntry;
    private readonly DispatcherTimer _autoSaveTimer;

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
        _autoSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _autoSaveTimer.Tick += (_, _) =>
        {
            _autoSaveTimer.Stop();
            PersistAdjustments("Auto-salvo");
        };
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

            var errors = new List<string>();
            var files = Directory.GetFiles(rawDir, "pokemon_*_raw.json")
                .OrderBy(ExtractDexFromRawPath)
                .ThenBy(Path.GetFileName)
                .ToList();
            foreach (var file in files)
            {
                try
                {
                    var meta = MetadataJson.DeserializeFromFile(file);
                    if (meta is null)
                    {
                        errors.Add($"{Path.GetFileName(file)}: JSON invalido");
                        continue;
                    }

                    Entries.Add(new PokemonEntry(meta));
                }
                catch (Exception ex)
                {
                    errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
                }
            }

            Status = errors.Count > 0
                ? $"Carregado: {Entries.Count} registros ({errors.Count} com erro)"
                : $"Carregado: {Entries.Count} registros";
        }
        catch (Exception ex)
        {
            Status = $"Erro: {ex.Message}";
        }

        OnAllPropertiesChanged();
    }

    private static int ExtractDexFromRawPath(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var parts = name.Split('_');
        if (parts.Length >= 2 && int.TryParse(parts[1], out var dex))
        {
            return dex;
        }

        return int.MaxValue;
    }

    private void LoadPreview(PokemonEntry entry)
    {
        _currentEntry = entry;
        SelectedEntryText = $"{entry.UniqueId} - {entry.Species}";
        OffsetAdjustment? adj = null;
        if (_adjustments.TryGetValue(entry.UniqueId, out var loaded))
        {
            adj = loaded;
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
            var sprite = ResolveSpritePath(entry.DexNumber, entry.FormId, adj?.PrimarySpriteFile ?? entry.PrimarySpriteFile, entry.SpriteFiles);
            if (sprite is not null && File.Exists(sprite))
            {
                SelectedSpritePath = sprite;
                var sheet = new BitmapImage();
                sheet.BeginInit();
                sheet.CacheOption = BitmapCacheOption.OnLoad;
                sheet.UriSource = new Uri(sprite);
                sheet.EndInit();

                var preferStandardGrid = PreferStandardGrid(sprite);
                var frameWOverride = CoalescePositive(adj?.FrameWidth, entry.FrameWidth);
                var frameHOverride = CoalescePositive(adj?.FrameHeight, entry.FrameHeight);
                var gridColsOverride = CoalescePositive(adj?.GridColumns, entry.GridColumns);
                var gridRowsOverride = CoalescePositive(adj?.GridRows, entry.GridRows);

                BuildPreview(sheet, SelectedGroundOffset, preferStandardGrid, frameWOverride, frameHOverride, gridColsOverride, gridRowsOverride, out var frameH, out var frameW);
                SelectedFrameHeight = frameH;

                if (!_adjustments.ContainsKey(entry.UniqueId))
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
        _adjustments.TryGetValue(_currentEntry.UniqueId, out var existing);
        _adjustments[_currentEntry.UniqueId] = BuildAdjustment(
            _currentEntry,
            existing,
            SelectedGroundOffset,
            SelectedCenterOffset,
            SelectedReviewed,
            HitboxX,
            HitboxY,
            HitboxWidth,
            HitboxHeight);
        PersistAdjustments($"Salvo Dex {_currentEntry.DexNumber:D4}");
        OnAllPropertiesChanged();
    }

    private void ExportAll()
    {
        PersistAdjustments("Exportado");
        OnAllPropertiesChanged();
    }

    private void BuildPreview(
        BitmapSource sheet,
        int groundOffset,
        bool preferStandardGrid,
        int? frameWOverride,
        int? frameHOverride,
        int? gridColsOverride,
        int? gridRowsOverride,
        out int frameH,
        out int frameW)
    {
        SpriteGrid grid;
        FrameSize frame;
        if (TryGetStoredFrameGrid(frameWOverride, frameHOverride, gridColsOverride, gridRowsOverride, sheet, out var storedGrid, out var storedFrame))
        {
            grid = storedGrid;
            frame = storedFrame;
        }
        else
        {
            var buffer = BuildPixelBuffer(sheet);
            (grid, frame) = SpriteSheetAnalyzer.DetectGrid(buffer, preferStandardGrid);
        }

        frameH = frame.Height;
        frameW = frame.Width;

        var rows = grid.Rows;
        var cols = grid.Columns;

        var validRows = SelectPreviewRows(rows, preferStandardGrid);
        if (validRows.Count == 0 || frameW <= 0 || frameH <= 0 || cols <= 0)
        {
            _segmentHeight = 0;
            _segmentsCount = 0;
            PreviewImage = null;
            return;
        }

        _segmentHeight = frameH;
        _segmentsCount = validRows.Count;

        var totalHeight = frameH * validRows.Count;
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
        if (string.Equals(name, SpriteFileNames.Walk, StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(name, SpriteFileNames.Idle, StringComparison.OrdinalIgnoreCase)) return true;
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

    private void ScheduleAutoSave()
    {
        _autoSaveTimer.Stop();
        _autoSaveTimer.Start();
    }

    private static bool TryGetStoredFrameGrid(int? frameW, int? frameH, int? cols, int? rows, BitmapSource source, out SpriteGrid grid, out FrameSize frame)
    {
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

    private static int? CoalescePositive(int? value, int? fallback)
    {
        if (value.HasValue && value.Value > 0) return value;
        return fallback;
    }

    private OffsetAdjustment BuildAdjustment(
        PokemonEntry entry,
        OffsetAdjustment? existing,
        int groundOffset,
        int centerOffset,
        bool reviewed,
        double hitboxX,
        double hitboxY,
        double hitboxWidth,
        double hitboxHeight)
    {
        var frameW = CoalescePositive(existing?.FrameWidth, entry.FrameWidth);
        var frameH = CoalescePositive(existing?.FrameHeight, entry.FrameHeight);
        var gridCols = CoalescePositive(existing?.GridColumns, entry.GridColumns);
        var gridRows = CoalescePositive(existing?.GridRows, entry.GridRows);
        var primary = existing?.PrimarySpriteFile ?? entry.PrimarySpriteFile;
        var walkFile = existing?.WalkSpriteFile;
        var idleFile = existing?.IdleSpriteFile;
        var fightFile = existing?.FightSpriteFile;
        var hasAttack = existing?.HasAttackAnimation ?? false;

        return new OffsetAdjustment(
            entry.UniqueId,
            groundOffset,
            centerOffset,
            reviewed,
            (int)Math.Round(hitboxX),
            (int)Math.Round(hitboxY),
            (int)Math.Round(hitboxWidth),
            (int)Math.Round(hitboxHeight),
            frameW,
            frameH,
            gridCols,
            gridRows,
            primary,
            walkFile,
            idleFile,
            fightFile,
            hasAttack);
    }

    private OffsetAdjustment BuildExportAdjustment(PokemonEntry entry, OffsetAdjustment? existing)
    {
        if (existing is null)
        {
            return BuildAdjustment(
                entry,
                null,
                entry.GroundOffsetY,
                entry.CenterOffsetX,
                false,
                0,
                0,
                entry.FrameWidth ?? 0,
                entry.FrameHeight ?? 0);
        }

        var frameW = CoalescePositive(existing.FrameWidth, entry.FrameWidth);
        var frameH = CoalescePositive(existing.FrameHeight, entry.FrameHeight);
        var gridCols = CoalescePositive(existing.GridColumns, entry.GridColumns);
        var gridRows = CoalescePositive(existing.GridRows, entry.GridRows);
        var primary = existing.PrimarySpriteFile ?? entry.PrimarySpriteFile;

        return existing with
        {
            FrameWidth = frameW,
            FrameHeight = frameH,
            GridColumns = gridCols,
            GridRows = gridRows,
            PrimarySpriteFile = primary
        };
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
            _adjustments.TryGetValue(e.UniqueId, out var adj);
            return BuildExportAdjustment(e, adj);
        }).OrderBy(r => r.UniqueId).ToList();
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
        _adjustments.TryGetValue(_currentEntry.UniqueId, out var existing);
        _adjustments[_currentEntry.UniqueId] = BuildAdjustment(
            _currentEntry,
            existing,
            SelectedGroundOffset,
            SelectedCenterOffset,
            SelectedReviewed,
            HitboxX,
            HitboxY,
            HitboxWidth,
            HitboxHeight);
        ScheduleAutoSave();
    }

    private static string? ResolveSpritePath(int dex, string formId, string? primaryFile, IReadOnlyList<string> emotes)
    {
        var dexPath = dex.ToString("D4");
        var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "SpriteCollab", "sprite", dexPath));
        if (!Directory.Exists(baseDir))
        {
            var alt = Path.GetFullPath(Path.Combine("SpriteCollab", "sprite", dexPath));
            if (!Directory.Exists(alt)) return null;
            baseDir = alt;
        }
        
        // Se não é forma base, procurar na subpasta
        if (formId != "0000")
        {
            var formDir = Path.Combine(baseDir, formId);
            if (Directory.Exists(formDir))
            {
                baseDir = formDir;
            }
        }

        var order = new List<string>();
        if (!string.IsNullOrEmpty(primaryFile)) order.Add(primaryFile);
        order.AddRange(new[] { SpriteFileNames.Walk, SpriteFileNames.Idle, SpriteFileNames.Sleep });
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
    public string FormId { get; }
    public string UniqueId { get; }
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
    public int? GridColumns { get; }
    public int? GridRows { get; }

    public PokemonEntry(PokemonSpriteMetadata meta)
    {
        DexNumber = meta.DexNumber;
        FormId = meta.Form ?? "0000";
        UniqueId = new PokemonVariant(meta.DexNumber, FormId).UniqueId;
        Species = meta.Species;
        HasWalk = meta.Animations.HasWalk;
        HasIdle = meta.Animations.HasIdle;
        HasSleep = meta.Animations.HasSleep;
        EmoteCount = meta.Animations.Emotes.Count;
        SpriteFiles = meta.Animations.Emotes;
        PrimarySpriteFile = meta.Walk.FileName ?? meta.Idle.FileName ?? meta.Sleep.FileName;

        var frame = meta.Walk.Frame ?? meta.Idle.Frame ?? meta.Sleep.Frame;
        var grid = meta.Walk.Grid ?? meta.Idle.Grid ?? meta.Sleep.Grid;

        FrameHeight = frame?.Height;
        FrameWidth = frame?.Width;
        GridColumns = grid?.Columns;
        GridRows = grid?.Rows;

        FrameSize = frame is not null
            ? $"{frame.Width}x{frame.Height}"
            : "-";

        Grid = grid is not null
            ? $"{grid.Columns}x{grid.Rows}"
            : "-";

        GroundOffsetY = meta.Offsets.GroundOffsetY;
        CenterOffsetX = meta.Offsets.CenterOffsetX;
        BodyType = meta.BodyType;
        Notes = string.Join(" | ", meta.Notes ?? Array.Empty<string>());
    }
}














