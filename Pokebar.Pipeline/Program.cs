using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Json;
using Pokebar.Core.Models;
using Pokebar.Core.Serialization;

Console.WriteLine("Pokebar Pipeline - stub inicial (gera JSON bruto por pasta de sprite).");

var expectedDex = Enumerable.Range(1, 1025).ToHashSet(); // 0001..1025
var generated = 0;
var foundDex = new HashSet<int>();
var errors = new List<string>();
var anomalies = new List<string>();
var frameHeights = new List<int>();
var frameWidths = new List<int>();
var groundOffsets = new List<int>();
var centerOffsets = new List<int>();

// Se não informar nada, usa SpriteCollab/sprite no repo.
var spriteRoot = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "SpriteCollab", "sprite"));

// Saída padrão é Assets/Raw
var outputDir = args.Length > 1
    ? Path.GetFullPath(args[1])
    : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Raw"));

if (!Directory.Exists(spriteRoot))
{
    Console.WriteLine($"Raiz dos sprites não encontrada: {spriteRoot}");
    Console.WriteLine("Passe explicitamente o caminho do diretório 'sprite' do SpriteCollab.");
    return;
}

Directory.CreateDirectory(outputDir);

Console.WriteLine($"Raiz dos sprites: {spriteRoot}");
Console.WriteLine($"Diretório de saída dos JSONs: {outputDir}");

var speciesDirs = Directory.GetDirectories(spriteRoot);

foreach (var dir in speciesDirs)
{
    var name = Path.GetFileName(dir);
    if (!int.TryParse(name, out var dex) || dex < 1 || dex > 1025)
    {
        // pula pastas que não são números de dex ou fora do range válido
        continue;
    }

    try
    {
        var walkFile = FindFile(dir, "Walk-Anim.png");
        var idleFile = FindFile(dir, "Idle-Anim.png");
        var sleepFile = FindFile(dir, "Sleep.png");
        var emoteFiles = FindEmotes(dir);

        var walkInfo = AnalyzeSprite(dir, walkFile, preferStandardGrid: true);
        var idleInfo = AnalyzeSprite(dir, idleFile, preferStandardGrid: true);
        var sleepInfo = AnalyzeSprite(dir, sleepFile, preferStandardGrid: false);

        (string? file, SpriteGrid? grid, FrameSize? frame) offsetsSource =
            walkInfo.Frame is not null ? (walkFile, walkInfo.Grid, walkInfo.Frame) :
            idleInfo.Frame is not null ? (idleFile, idleInfo.Grid, idleInfo.Frame) :
            sleepInfo.Frame is not null ? (sleepFile, sleepInfo.Grid, sleepInfo.Frame) :
            (null, null, null);

        // Usar linhas 3 e 7 (0-based 2 e 6) para cálculo de offsets do walk; se não existirem, usa todas.
        int[]? rowsToUse = offsetsSource.Item1 == walkFile ? new[] { 2, 6 } : null;

        var offsets = offsetsSource.Item1 is not null && offsetsSource.Item2 is not null && offsetsSource.Item3 is not null
            ? ComputeOffsets(Path.Combine(dir, offsetsSource.Item1), offsetsSource.Item2, offsetsSource.Item3, rowsToUse)
            : new SpriteOffsets(GroundOffsetY: 0, CenterOffsetX: 0);

        var bodyType = SuggestBodyType(walkInfo.Frame ?? idleInfo.Frame ?? sleepInfo.Frame);

        var metadata = new PokemonSpriteMetadata(
            DexNumber: dex,
            Species: name,
            Form: null,
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

        var outputPath = Path.Combine(outputDir, $"pokemon_{dex:D4}_raw.json");
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
        if (walkFile is null) anomalies.Add($"Dex {dex:D4}: Walk-Anim.png ausente");
        if (idleFile is null) anomalies.Add($"Dex {dex:D4}: Idle-Anim.png ausente");
        if (sleepFile is null) anomalies.Add($"Dex {dex:D4}: Sleep.png ausente");

        // sem filtro de linhas específico; usamos todos os frames disponíveis

        if (walkInfo.Frame is not null && idleInfo.Frame is not null &&
            (DiffRatio(walkInfo.Frame.Width, idleInfo.Frame.Width) > 0.25 ||
             DiffRatio(walkInfo.Frame.Height, idleInfo.Frame.Height) > 0.25))
        {
            anomalies.Add($"Dex {dex:D4}: Frame walk {walkInfo.Frame?.Width}x{walkInfo.Frame?.Height} difere do idle {idleInfo.Frame?.Width}x{idleInfo.Frame?.Height}");
        }

        if (walkInfo.Grid is not null && idleInfo.Grid is not null &&
            (walkInfo.Grid?.Columns != idleInfo.Grid?.Columns || walkInfo.Grid?.Rows != idleInfo.Grid?.Rows) &&
            ((walkInfo.Grid?.Columns ?? 1) * (walkInfo.Grid?.Rows ?? 1) > 1 ||
             (idleInfo.Grid?.Columns ?? 1) * (idleInfo.Grid?.Rows ?? 1) > 1))
        {
            anomalies.Add($"Dex {dex:D4}: Grid walk {walkInfo.Grid?.Columns}x{walkInfo.Grid?.Rows} difere do idle {idleInfo.Grid?.Columns}x{idleInfo.Grid?.Rows}");
        }

        var refFrame = offsetsSource.Item3;
        if (refFrame is not null)
        {
            if (offsets.GroundOffsetY > refFrame.Height * 0.6)
            {
                anomalies.Add($"Dex {dex:D4}: groundOffsetY alto ({offsets.GroundOffsetY}) para frame {refFrame.Height}px");
            }

            if (Math.Abs(offsets.CenterOffsetX) > refFrame.Width * 0.6)
            {
                anomalies.Add($"Dex {dex:D4}: centerOffsetX extremo ({offsets.CenterOffsetX}) para frame {refFrame.Width}px");
            }
        }
    }
    catch (Exception ex)
    {
        errors.Add($"Dex {dex:D4}: {ex.Message}");
    }
}

// Gera placeholders para os que não vieram no repositório clonado
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

// Merge offsets com ajustes finais (se existirem) e gera arquivo único para runtime
try
{
    var finalDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Final");
    Directory.CreateDirectory(finalDir);
    var adjustmentsPath = Path.Combine(finalDir, "pokemon_offsets_final.json");
    var runtimePath = Path.Combine(finalDir, "pokemon_offsets_runtime.json");

    var adjustments = FinalOffsets.Load(adjustmentsPath);
    var merged = new List<OffsetAdjustment>();

    foreach (var dex in expectedDex.OrderBy(d => d))
    {
        var rawPath = Path.Combine(outputDir, $"pokemon_{dex:D4}_raw.json");
        var meta = MetadataJson.DeserializeFromFile(rawPath);
        var baseOffsets = meta?.Offsets ?? new SpriteOffsets(0, 0);
        var hb = (meta?.Walk.Frame is not null)
            ? (HitboxX: 0, HitboxY: 0, HitboxWidth: meta.Walk.Frame.Width, HitboxHeight: meta.Walk.Frame.Height)
            : (HitboxX: 0, HitboxY: 0, HitboxWidth: 0, HitboxHeight: 0);

        if (adjustments.TryGetValue(dex, out var adj))
        {
            merged.Add(new OffsetAdjustment(dex, adj.GroundOffsetY, adj.CenterOffsetX, adj.Reviewed, adj.HitboxX, adj.HitboxY, adj.HitboxWidth, adj.HitboxHeight));
        }
        else
        {
            merged.Add(new OffsetAdjustment(dex, baseOffsets.GroundOffsetY, baseOffsets.CenterOffsetX, false, hb.HitboxX, hb.HitboxY, hb.HitboxWidth, hb.HitboxHeight));
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

static string[] FindEmotes(string dir)
{
    return Directory.GetFiles(dir, "Emote-*.png", SearchOption.TopDirectoryOnly)
        .Select(Path.GetFileName)
        .Where(f => f is not null)
        .Select(f => f!)
        .OrderBy(f => f)
        .ToArray();
}

static SpriteSheetInfo AnalyzeSprite(string dir, string? fileName, bool preferStandardGrid)
{
    if (fileName is null) return new SpriteSheetInfo(null, null, null);

    var fullPath = Path.Combine(dir, fileName);
    if (!File.Exists(fullPath)) return new SpriteSheetInfo(null, null, null);

    using var bmp = SafeBitmap(fullPath);
    var (grid, frame) = DetectGrid(bmp, preferStandardGrid);
    return new SpriteSheetInfo(fileName, frame, grid);
}

static (SpriteGrid Grid, FrameSize Frame) DetectGrid(Bitmap bmp, bool preferStandardGrid)
{
    // Caso SpriteCollab típico de walk: 8 linhas fixas, colunas variáveis.
    if (preferStandardGrid && bmp.Height % 8 == 0)
    {
        var rows = 8;
        var colCandidates = Enumerable.Range(1, Math.Min(12, bmp.Width))
            .Where(c => bmp.Width % c == 0);

        var (grid, frame) = PickBestGrid(bmp, colCandidates, new[] { rows });
        if (grid.Rows == rows) return (grid, frame);
    }

    // Favoritos anteriores
    if (preferStandardGrid)
    {
        if (bmp.Width % 6 == 0 && bmp.Height % 8 == 0)
        {
            return (new SpriteGrid(6, 8), new FrameSize(bmp.Width / 6, bmp.Height / 8));
        }

        if (bmp.Width % 4 == 0 && bmp.Height % 2 == 0)
        {
            return (new SpriteGrid(4, 2), new FrameSize(bmp.Width / 4, bmp.Height / 2));
        }

        if (bmp.Width % 4 == 0)
        {
            return (new SpriteGrid(4, 1), new FrameSize(bmp.Width / 4, bmp.Height));
        }
    }

    // Busca genérica 1..8
    var colCandidatesGeneric = Enumerable.Range(1, Math.Min(8, bmp.Width))
        .Where(c => bmp.Width % c == 0);
    var rowCandidatesGeneric = Enumerable.Range(1, Math.Min(8, bmp.Height))
        .Where(r => bmp.Height % r == 0);

    return PickBestGrid(bmp, colCandidatesGeneric, rowCandidatesGeneric);
}

static (SpriteGrid Grid, FrameSize Frame) PickBestGrid(Bitmap bmp, IEnumerable<int> colCandidates, IEnumerable<int> rowCandidates)
{
    var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
    var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, bmp.PixelFormat);

    try
    {
        var bpp = Image.GetPixelFormatSize(data.PixelFormat) / 8;
        var stride = data.Stride;
        var basePtr = data.Scan0;

        double bestScore = double.MaxValue;
        SpriteGrid bestGrid = new(1, 1);
        FrameSize bestFrame = new(bmp.Width, bmp.Height);

        foreach (var cols in colCandidates)
        {
            foreach (var rows in rowCandidates)
            {
                var frameW = bmp.Width / cols;
                var frameH = bmp.Height / rows;
                if (frameW == 0 || frameH == 0) continue;

                var score = EvaluateGrid(basePtr, stride, bpp, cols, rows, frameW, frameH);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestGrid = new SpriteGrid(cols, rows);
                    bestFrame = new FrameSize(frameW, frameH);
                }
            }
        }

        return (bestGrid, bestFrame);
    }
    finally
    {
        bmp.UnlockBits(data);
    }
}

static unsafe double EvaluateGrid(IntPtr basePtr, int stride, int bpp, int cols, int rows, int frameW, int frameH)
{
    var minW = int.MaxValue;
    var maxW = 0;
    var minH = int.MaxValue;
    var maxH = 0;
    var empty = 0;

    for (var row = 0; row < rows; row++)
    {
        for (var col = 0; col < cols; col++)
        {
            var frameOriginX = col * frameW;
            var frameOriginY = row * frameH;

            var localMinX = frameW;
            var localMaxX = -1;
            var localMinY = frameH;
            var localMaxY = -1;

            for (var y = 0; y < frameH; y++)
            {
                var ptrY = frameOriginY + y;
                for (var x = 0; x < frameW; x++)
                {
                    var ptrX = frameOriginX + x;
                    var pixelPtr = (byte*)basePtr + ptrY * stride + ptrX * bpp;
                    var alpha = bpp >= 4 ? pixelPtr[3] : (byte)255;
                    if (alpha > 0)
                    {
                        localMinX = Math.Min(localMinX, x);
                        localMaxX = Math.Max(localMaxX, x);
                        localMinY = Math.Min(localMinY, y);
                        localMaxY = Math.Max(localMaxY, y);
                    }
                }
            }

            if (localMaxX < localMinX || localMaxY < localMinY)
            {
                empty++;
                continue;
            }

            var w = localMaxX - localMinX + 1;
            var h = localMaxY - localMinY + 1;
            minW = Math.Min(minW, w);
            maxW = Math.Max(maxW, w);
            minH = Math.Min(minH, h);
            maxH = Math.Max(maxH, h);
        }
    }

    if (empty == cols * rows)
    {
        return double.MaxValue;
    }

    // Quanto menor a variação de largura/altura entre frames, melhor.
    var variation = (maxW - minW) + (maxH - minH);
    var emptyPenalty = empty * 10000;
    return variation + emptyPenalty;
}

static SpriteOffsets ComputeOffsets(string fullPath, SpriteGrid grid, FrameSize frame, int[]? rowsToUse = null)
{
    using var bmp = SafeBitmap(fullPath);
    var groundOffsetMax = 0;
    double centerOffsetAccum = 0;
    var framesCount = 0;

    var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
    var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, bmp.PixelFormat);
    try
    {
        var bytesPerPixel = Image.GetPixelFormatSize(data.PixelFormat) / 8;
        var stride = data.Stride;
        var basePtr = data.Scan0;

    HashSet<int>? allowedRows = null;
    if (rowsToUse is not null)
    {
        allowedRows = rowsToUse.Where(r => r >= 0 && r < grid.Rows).ToHashSet();
        if (allowedRows.Count == 0)
        {
            allowedRows = null; // se ficou vazio, usa todas as linhas disponíveis
        }
    }

    for (var row = 0; row < grid.Rows; row++)
    {
        if (allowedRows is not null && !allowedRows.Contains(row)) continue;

        for (var col = 0; col < grid.Columns; col++)
        {
            var frameOriginX = col * frame.Width;
            var frameOriginY = row * frame.Height;

                var groundOffset = frame.Height;
                var minX = frame.Width;
                var maxX = -1;

                for (var y = frame.Height - 1; y >= 0; y--)
                {
                    var ptrY = frameOriginY + y;
                    bool rowHasPixel = false;
                    for (var x = 0; x < frame.Width; x++)
                    {
                        var ptrX = frameOriginX + x;
                        unsafe
                        {
                            var pixelPtr = (byte*)basePtr + ptrY * stride + ptrX * bytesPerPixel;
                            var alpha = bytesPerPixel >= 4 ? pixelPtr[3] : (byte)255;
                            if (alpha > 0)
                            {
                                rowHasPixel = true;
                                groundOffset = Math.Min(groundOffset, frame.Height - 1 - y);
                                minX = Math.Min(minX, x);
                                maxX = Math.Max(maxX, x);
                            }
                        }
                    }

                    // Se achou pixel nesta linha, continua buscando minX/maxX nos demais pixels do frame,
                    // mas não precisa subir linhas se já alcançou topo visível.
                    if (rowHasPixel && groundOffset != frame.Height)
                    {
                        // embora continue para x, vamos subir mais linhas para pegar bounding box completo
                        continue;
                    }
                }

                if (maxX >= minX && minX < frame.Width && maxX >= 0)
                {
                    var center = (minX + maxX) / 2.0;
                    var offsetX = center - (frame.Width / 2.0);
                    centerOffsetAccum += offsetX;
                    framesCount++;
                }

                groundOffsetMax = Math.Max(groundOffsetMax, groundOffset);
            }
        }
    }
    finally
    {
        bmp.UnlockBits(data);
    }

    var centerOffset = framesCount > 0
        ? (int)Math.Round(centerOffsetAccum / framesCount)
        : 0;

    return new SpriteOffsets(groundOffsetMax, centerOffset);
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

static Bitmap SafeBitmap(string path)
{
    // Garante formato 32bpp para acesso rápido.
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

static double DiffRatio(int a, int b)
{
    var max = Math.Max(a, b);
    if (max == 0) return 0;
    var min = Math.Min(a, b);
    return (double)(max - min) / max;
}
