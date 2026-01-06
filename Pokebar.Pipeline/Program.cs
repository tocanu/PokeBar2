using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using Pokebar.Core;
using Pokebar.Core.Models;
using Pokebar.Core.Serialization;
using Pokebar.Core.Sprites;

const double HitboxShrinkFactor = 0.9;
const int MinHitboxSize = 6;

Console.WriteLine("Pokebar Pipeline - stub inicial (gera JSON bruto por pasta de sprite).");

var expectedDex = Enumerable.Range(DexConstants.MinDex, DexConstants.TotalDex).ToHashSet(); // 0001..1025
var generated = 0;
var foundDex = new HashSet<int>();
var errors = new List<string>();
var anomalies = new List<string>();
var frameHeights = new List<int>();
var frameWidths = new List<int>();
var groundOffsets = new List<int>();
var centerOffsets = new List<int>();
var geometries = new Dictionary<string, SpriteGeometry>();

// Se nÃ£o informar nada, usa SpriteCollab/sprite no repo.
var spriteRoot = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "SpriteCollab", "sprite"));

// SaÃ­da padrÃ£o Ã© Assets/Raw
var outputDir = args.Length > 1
    ? Path.GetFullPath(args[1])
    : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Raw"));

if (!Directory.Exists(spriteRoot))
{
    Console.WriteLine($"Raiz dos sprites nÃ£o encontrada: {spriteRoot}");
    Console.WriteLine("Passe explicitamente o caminho do diretÃ³rio 'sprite' do SpriteCollab.");
    return;
}

Directory.CreateDirectory(outputDir);

Console.WriteLine($"Raiz dos sprites: {spriteRoot}");
Console.WriteLine($"DiretÃ³rio de saÃ­da dos JSONs: {outputDir}");

// Enumera todos os Pokémon e suas formas
var allVariants = SpriteDirectoryHelper.EnumerateSpriteFolders(spriteRoot).ToList();
Console.WriteLine($"Encontrados {allVariants.Count} variantes (incluindo formas).");

foreach (var (dex, formId, dir) in allVariants)
{
    try
    {
        var variant = new PokemonVariant(dex, formId);
        var walkFile = FindFile(dir, SpriteFileNames.Walk);
        var idleFile = FindFile(dir, SpriteFileNames.Idle);
        var sleepFile = FindFile(dir, SpriteFileNames.Sleep);
        var emoteFiles = FindEmotes(dir);

        var walkInfo = AnalyzeSprite(dir, walkFile, preferStandardGrid: true);
        var idleInfo = AnalyzeSprite(dir, idleFile, preferStandardGrid: true);
        var sleepInfo = AnalyzeSprite(dir, sleepFile, preferStandardGrid: false);

        (string? file, SpriteGrid? grid, FrameSize? frame) offsetsSource =
            walkInfo.Frame is not null ? (walkFile, walkInfo.Grid, walkInfo.Frame) :
            idleInfo.Frame is not null ? (idleFile, idleInfo.Grid, idleInfo.Frame) :
            sleepInfo.Frame is not null ? (sleepFile, sleepInfo.Grid, sleepInfo.Frame) :
            (null, null, null);

        // Usar linhas 3 e 7 (0-based 2 e 6) para cÃ¡lculo de offsets do walk; se nÃ£o existirem, usa todas.
        int[]? rowsToUse = offsetsSource.Item1 == walkFile ? new[] { 2, 6 } : null;

        var geometry = offsetsSource.Item1 is not null && offsetsSource.Item2 is not null && offsetsSource.Item3 is not null
            ? ComputeGeometry(Path.Combine(dir, offsetsSource.Item1), offsetsSource.Item2, offsetsSource.Item3, rowsToUse)
            : SpriteGeometry.Empty;
        var offsets = geometry.Offsets;

        var bodyType = SuggestBodyType(walkInfo.Frame ?? idleInfo.Frame ?? sleepInfo.Frame);

        var metadata = new PokemonSpriteMetadata(
            DexNumber: dex,
            Species: $"{dex:D4}",
            Form: formId != "0000" ? formId : null,
            Walk: walkInfo,
            Idle: idleInfo,
            Sleep: sleepInfo,
            Animations: new AnimationSummary(
                HasWalk: walkFile != null,
                HasIdle: idleFile != null,
                HasSleep: sleepFile != null,
                Emotes: emoteFiles),
            Offsets: offsets,
            BodyType: bodyType,
            Notes: Array.Empty<string>());

        geometries[variant.UniqueId] = geometry;

        var outputPath = Path.Combine(outputDir, $"pokemon_{variant.UniqueId}_raw.json");
        MetadataJson.SerializeToFile(metadata, outputPath);
        generated++;
        foundDex.Add(dex);
        if (offsetsSource.Item3 is not null)
        {
            frameWidths.Add(offsetsSource.Item3.Width);
            frameHeights.Add(offsetsSource.Item3.Height);
            groundOffsets.Add(offsets.GroundOffsetY);
            centerOffsets.Add(offsets.CenterOffsetX);
        }

        // Logs de anomalias
        var variantId = variant.UniqueId;
        if (walkFile is null) anomalies.Add($"{variantId}: {SpriteFileNames.Walk} ausente");
        if (idleFile is null) anomalies.Add($"{variantId}: {SpriteFileNames.Idle} ausente");
        if (sleepFile is null) anomalies.Add($"{variantId}: {SpriteFileNames.Sleep} ausente");

        // sem filtro de linhas especÃ­fico; usamos todos os frames disponÃ­veis

        if (walkInfo.Frame is not null && idleInfo.Frame is not null &&
            (DiffRatio(walkInfo.Frame.Width, idleInfo.Frame.Width) > 0.25 ||
             DiffRatio(walkInfo.Frame.Height, idleInfo.Frame.Height) > 0.25))
        {
            anomalies.Add($"{variantId}: Frame walk {walkInfo.Frame?.Width}x{walkInfo.Frame?.Height} difere do idle {idleInfo.Frame?.Width}x{idleInfo.Frame?.Height}");
        }

        if (walkInfo.Grid is not null && idleInfo.Grid is not null &&
            (walkInfo.Grid?.Columns != idleInfo.Grid?.Columns || walkInfo.Grid?.Rows != idleInfo.Grid?.Rows) &&
            ((walkInfo.Grid?.Columns ?? 1) * (walkInfo.Grid?.Rows ?? 1) > 1 ||
             (idleInfo.Grid?.Columns ?? 1) * (idleInfo.Grid?.Rows ?? 1) > 1))
        {
            anomalies.Add($"{variantId}: Grid walk {walkInfo.Grid?.Columns}x{walkInfo.Grid?.Rows} difere do idle {idleInfo.Grid?.Columns}x{idleInfo.Grid?.Rows}");
        }

        var refFrame = offsetsSource.Item3;
        if (refFrame is not null)
        {
            if (offsets.GroundOffsetY > refFrame.Height * 0.6)
            {
                anomalies.Add($"{variantId}: groundOffsetY alto ({offsets.GroundOffsetY}) para frame {refFrame.Height}px");
            }

            if (Math.Abs(offsets.CenterOffsetX) > refFrame.Width * 0.6)
            {
                anomalies.Add($"{variantId}: centerOffsetX extremo ({offsets.CenterOffsetX}) para frame {refFrame.Width}px");
            }
        }
    }
    catch (Exception ex)
    {
        errors.Add($"Dex {dex:D4} Form {formId}: {ex.Message}");
    }
}

// Gera placeholders para os que nÃ£o vieram no repositÃ³rio clonado
var missing = expectedDex.Except(foundDex).OrderBy(x => x).ToList();
foreach (var dex in missing)
{
    var metadata = new PokemonSpriteMetadata(
        DexNumber: dex,
        Species: $"{dex:D4}",
        Form: null,
        Walk: new SpriteSheetInfo(null, null, null),
        Idle: new SpriteSheetInfo(null, null, null),
        Sleep: new SpriteSheetInfo(null, null, null),
        Animations: new AnimationSummary(
            HasWalk: false,
            HasIdle: false,
            HasSleep: false,
            Emotes: Array.Empty<string>()),
        Offsets: new SpriteOffsets(GroundOffsetY: 0, CenterOffsetX: 0),
        BodyType: BodyTypeSuggestion.Unknown,
        Notes: new[] { "Sprite folder missing in SpriteCollab clone." });

    var outputPath = Path.Combine(outputDir, $"pokemon_{dex:D4}_raw.json");
    MetadataJson.SerializeToFile(metadata, outputPath);
    generated++;
}

Console.WriteLine($"JSONs gerados: {generated} (presentes: {foundDex.Count}, placeholders faltantes: {missing.Count})");
if (missing.Count > 0)
{
    Console.WriteLine("Dex numbers sem pasta no SpriteCollab: " + string.Join(", ", missing));
}

if (errors.Count > 0)
{
    Console.WriteLine($"Ocorreram {errors.Count} erros ao processar sprites:");
    foreach (var err in errors.Take(10))
    {
        Console.WriteLine(" - " + err);
    }
    if (errors.Count > 10) Console.WriteLine(" ...");
}

if (anomalies.Count > 0)
{
    Console.WriteLine($"Anomalias detectadas: {anomalies.Count}");
    foreach (var a in anomalies.Take(15))
    {
        Console.WriteLine(" - " + a);
    }
    if (anomalies.Count > 15) Console.WriteLine(" ...");

    var anomaliesPath = Path.Combine(outputDir, "anomalies.txt");
    File.WriteAllLines(anomaliesPath, anomalies);
    Console.WriteLine($"Lista completa salva em: {anomaliesPath}");
}

// Summary (JSON + CSV)
var summary = new
{
    totalDex = expectedDex.Count,
    present = foundDex.Count,
    placeholders = missing.Count,
    anomalies = anomalies.Count,
    avgFrameWidth = frameWidths.Count > 0 ? frameWidths.Average() : 0,
    avgFrameHeight = frameHeights.Count > 0 ? frameHeights.Average() : 0,
    avgGroundOffset = groundOffsets.Count > 0 ? groundOffsets.Average() : 0,
    avgCenterOffset = centerOffsets.Count > 0 ? centerOffsets.Average() : 0
};

var summaryJsonPath = Path.Combine(outputDir, "summary.json");
File.WriteAllText(summaryJsonPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));

var summaryCsvPath = Path.Combine(outputDir, "summary.csv");
var csvLines = new[]
{
    "totalDex,present,placeholders,anomalies,avgFrameWidth,avgFrameHeight,avgGroundOffset,avgCenterOffset",
    string.Join(",", new[]
    {
        summary.totalDex.ToString(CultureInfo.InvariantCulture),
        summary.present.ToString(CultureInfo.InvariantCulture),
        summary.placeholders.ToString(CultureInfo.InvariantCulture),
        summary.anomalies.ToString(CultureInfo.InvariantCulture),
        summary.avgFrameWidth.ToString("0.###", CultureInfo.InvariantCulture),
        summary.avgFrameHeight.ToString("0.###", CultureInfo.InvariantCulture),
        summary.avgGroundOffset.ToString("0.###", CultureInfo.InvariantCulture),
        summary.avgCenterOffset.ToString("0.###", CultureInfo.InvariantCulture)
    })
};
File.WriteAllLines(summaryCsvPath, csvLines);
Console.WriteLine($"Resumo salvo em: {summaryJsonPath} e {summaryCsvPath}");

// Merge offsets com ajustes finais (se existirem) e gera arquivo Ãºnico para runtime
try
{
    var finalDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Final");
    Directory.CreateDirectory(finalDir);
    var adjustmentsPath = Path.Combine(finalDir, "pokemon_offsets_final.json");
    var runtimePath = Path.Combine(finalDir, "pokemon_offsets_runtime.json");

    var adjustments = FinalOffsets.Load(adjustmentsPath);
    var merged = new List<OffsetAdjustment>();

    // Processar todas as variantes encontradas (base + formas)
    foreach (var variant in allVariants.OrderBy(v => v.DexNumber).ThenBy(v => v.FormId))
    {
        var dex = variant.DexNumber;
        var formId = variant.FormId;
        var uniqueId = new PokemonVariant(dex, formId).UniqueId;
        var rawPath = Path.Combine(outputDir, $"pokemon_{uniqueId}_raw.json");
        var meta = MetadataJson.DeserializeFromFile(rawPath);
        var dexDir = Path.Combine(spriteRoot, dex.ToString("D4"));
        if (formId != "0000")
        {
            dexDir = Path.Combine(dexDir, formId);
        }
        var walkSpriteFile = meta?.Walk.FileName ?? FindFile(dexDir, "Walk-Anim.png");
        var idleSpriteFile = meta?.Idle.FileName ?? FindFile(dexDir, "Idle-Anim.png");
        var fightSpriteFile = FindFightFile(dexDir);
        var metaFrame = meta?.Walk.Frame ?? meta?.Idle.Frame ?? meta?.Sleep.Frame;
        var metaGrid = meta?.Walk.Grid ?? meta?.Idle.Grid ?? meta?.Sleep.Grid;
        var metaPrimaryFile = meta?.Walk.FileName ?? meta?.Idle.FileName ?? meta?.Sleep.FileName;
        var geometry = geometries.TryGetValue(uniqueId, out var geo) ? geo : SpriteGeometry.Empty;
        var baseOffsets = meta?.Offsets ?? geometry.Offsets;
        var hb = geometry.Hitbox.IsValid
            ? geometry.Hitbox
            : metaFrame is not null
                ? new BoundingBox(0, 0, metaFrame.Width, metaFrame.Height)
                : BoundingBox.Empty;

        if (adjustments.TryGetValue(uniqueId, out var adj))
        {
            var primaryFile = adj.PrimarySpriteFile ?? metaPrimaryFile ?? walkSpriteFile ?? idleSpriteFile;
            var mergedWalkFile = adj.WalkSpriteFile ?? walkSpriteFile;
            var mergedIdleFile = adj.IdleSpriteFile ?? idleSpriteFile;
            var mergedFightFile = adj.FightSpriteFile ?? fightSpriteFile;
            var hasAttack = adj.HasAttackAnimation || !string.IsNullOrEmpty(mergedFightFile);
            var adjFrameGridValid = IsFrameGridValidForSheet(spriteRoot, dex, primaryFile, adj);
            var frameW = adjFrameGridValid
                ? CoalescePositive(adj.FrameWidth, metaFrame?.Width)
                : metaFrame?.Width ?? adj.FrameWidth;
            var frameH = adjFrameGridValid
                ? CoalescePositive(adj.FrameHeight, metaFrame?.Height)
                : metaFrame?.Height ?? adj.FrameHeight;
            var gridCols = adjFrameGridValid
                ? CoalescePositive(adj.GridColumns, metaGrid?.Columns)
                : metaGrid?.Columns ?? adj.GridColumns;
            var gridRows = adjFrameGridValid
                ? CoalescePositive(adj.GridRows, metaGrid?.Rows)
                : metaGrid?.Rows ?? adj.GridRows;

            merged.Add(new OffsetAdjustment(
                uniqueId,
                adj.GroundOffsetY,
                adj.CenterOffsetX,
                adj.Reviewed,
                adj.HitboxX,
                adj.HitboxY,
                adj.HitboxWidth,
                adj.HitboxHeight,
                frameW,
                frameH,
                gridCols,
                gridRows,
                primaryFile,
                mergedWalkFile,
                mergedIdleFile,
                mergedFightFile,
                hasAttack));
        }
        else
        {
            merged.Add(new OffsetAdjustment(
                uniqueId,
                baseOffsets.GroundOffsetY,
                baseOffsets.CenterOffsetX,
                false,
                hb.X,
                hb.Y,
                hb.Width,
                hb.Height,
                metaFrame?.Width,
                metaFrame?.Height,
                metaGrid?.Columns,
                metaGrid?.Rows,
                metaPrimaryFile,
                walkSpriteFile,
                idleSpriteFile,
                fightSpriteFile,
                !string.IsNullOrEmpty(fightSpriteFile)));
        }
    }

    File.WriteAllText(runtimePath, JsonSerializer.Serialize(merged, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Offsets finais para runtime salvos em: {runtimePath}");
}
catch (Exception ex)
{
    Console.WriteLine($"Falha ao gerar offsets finais para runtime: {ex.Message}");
}

static string? FindFile(string dir, string fileName)
{
    var path = Path.Combine(dir, fileName);
    return File.Exists(path) ? fileName : null;
}

static string? FindFightFile(string dir)
{
    var candidates = SpriteFileNames.AttackAnimations;

    foreach (var name in candidates)
    {
        var path = Path.Combine(dir, name);
        if (File.Exists(path))
            return name;
    }

    return null;
}

static string[] FindEmotes(string dir)
{
    return Directory.GetFiles(dir, "Emote-*.png", SearchOption.TopDirectoryOnly)
        .Select(Path.GetFileName)
        .Where(f => f is not null)
        .Select(f => f!)
        .OrderBy(f => f)
        .ToArray();
}

[SupportedOSPlatform("windows")]
static SpriteSheetInfo AnalyzeSprite(string dir, string? fileName, bool preferStandardGrid)
{
    if (fileName is null) return new SpriteSheetInfo(null, null, null);

    var fullPath = Path.Combine(dir, fileName);
    if (!File.Exists(fullPath)) return new SpriteSheetInfo(null, null, null);

    using var bmp = SafeBitmap(fullPath);
    var buffer = BuildPixelBuffer(bmp);
    var (grid, frame) = SpriteSheetAnalyzer.DetectGrid(buffer, preferStandardGrid);
    return new SpriteSheetInfo(fileName, frame, grid);
}

[SupportedOSPlatform("windows")]
static SpriteGeometry ComputeGeometry(string fullPath, SpriteGrid grid, FrameSize frame, int[]? rowsToUse = null)
{
    using var bmp = SafeBitmap(fullPath);
    var buffer = BuildPixelBuffer(bmp);
    return SpriteSheetAnalyzer.ComputeGeometry(buffer, grid, frame, rowsToUse, HitboxShrinkFactor, MinHitboxSize);
}

static BodyTypeSuggestion SuggestBodyType(FrameSize? frame)
{
    if (frame is null) return BodyTypeSuggestion.Unknown;
    var h = frame.Height;
    if (h <= 40) return BodyTypeSuggestion.Small;
    if (h <= 64) return BodyTypeSuggestion.Medium;
    if (h <= 96) return BodyTypeSuggestion.Tall;
    return BodyTypeSuggestion.Long;
}

[SupportedOSPlatform("windows")]
static Bitmap SafeBitmap(string path)
{
    // Garante formato 32bpp para acesso rÃ¡pido.
    var original = new Bitmap(path);
    if (original.PixelFormat == PixelFormat.Format32bppArgb || original.PixelFormat == PixelFormat.Format32bppPArgb)
    {
        return original;
    }

    var clone = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(clone))
    {
        g.DrawImage(original, 0, 0, original.Width, original.Height);
    }
    original.Dispose();
    return clone;
}

[SupportedOSPlatform("windows")]
static PixelBuffer BuildPixelBuffer(Bitmap bmp)
{
    var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
    var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, bmp.PixelFormat);
    try
    {
        var bytesPerPixel = Image.GetPixelFormatSize(data.PixelFormat) / 8;
        var stride = data.Stride;
        var absStride = Math.Abs(stride);
        var buffer = new byte[absStride * bmp.Height];

        for (var y = 0; y < bmp.Height; y++)
        {
            var srcPtr = IntPtr.Add(data.Scan0, y * stride);
            Marshal.Copy(srcPtr, buffer, y * absStride, absStride);
        }

        return new PixelBuffer(buffer, bmp.Width, bmp.Height, absStride, bytesPerPixel);
    }
    finally
    {
        bmp.UnlockBits(data);
    }
}

static double DiffRatio(int a, int b)
{
    var max = Math.Max(a, b);
    if (max == 0) return 0;
    var min = Math.Min(a, b);
    return (double)(max - min) / max;
}

static int? CoalescePositive(int? value, int? fallback)
{
    if (value.HasValue && value.Value > 0) return value;
    return fallback;
}

[SupportedOSPlatform("windows")]
static bool IsFrameGridValidForSheet(string spriteRoot, int dex, string? fileName, OffsetAdjustment adj)
{
    if (adj.FrameWidth is not > 0 || adj.FrameHeight is not > 0)
        return false;
    if (adj.GridColumns is not > 0 || adj.GridRows is not > 0)
        return false;

    if (string.IsNullOrWhiteSpace(fileName))
        return true;

    var fullPath = Path.Combine(spriteRoot, dex.ToString("D4"), fileName);
    if (!File.Exists(fullPath))
        return true;

    using var bmp = SafeBitmap(fullPath);
    var totalW = adj.FrameWidth.Value * adj.GridColumns.Value;
    var totalH = adj.FrameHeight.Value * adj.GridRows.Value;
    return totalW == bmp.Width && totalH == bmp.Height;
}


