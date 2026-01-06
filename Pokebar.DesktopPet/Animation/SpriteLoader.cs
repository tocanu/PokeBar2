using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Pokebar.Core.Models;
using Pokebar.Core.Serialization;
using Pokebar.Core.Sprites;

namespace Pokebar.DesktopPet.Animation;

public enum AnimationType
{
    Walk = 0,
    Idle = 1,
    Sleep = 2,
    Fight = 3
}

public class SpriteLoader
{
    private static readonly string[] FightCandidates = SpriteFileNames.AttackAnimations;

    private readonly IReadOnlyDictionary<string, OffsetAdjustment> _offsets;
    private readonly string _spriteBasePath;

    public SpriteLoader(string offsetsJsonPath, string spriteBasePath)
    {
        _spriteBasePath = spriteBasePath;
        _offsets = File.Exists(offsetsJsonPath)
            ? FinalOffsets.Load(offsetsJsonPath)
            : new Dictionary<string, OffsetAdjustment>();
    }

    public bool TryGetOffset(string uniqueId, [NotNullWhen(true)] out OffsetAdjustment? offset)
    {
        if (_offsets.TryGetValue(uniqueId, out var found))
        {
            offset = found;
            return true;
        }

        offset = null;
        return false;
    }

    public AnimationClip? LoadAnimation(
        int dex,
        string formId,
        AnimationType type,
        int[]? rowsToUse = null,
        int[]? columnsToUse = null,
        bool requireSelection = false)
    {
        var uniqueId = new PokemonVariant(dex, formId).UniqueId;
        if (type == AnimationType.Fight)
        {
            return LoadFightAnimation(dex, formId, uniqueId);
        }

        _offsets.TryGetValue(uniqueId, out var offset);
        var spritePath = ResolveSpritePath(dex, formId, offset, type);
        return LoadClipFromPath(dex, formId, uniqueId, spritePath, offset, type, rowsToUse, columnsToUse, requireSelection);
    }

    private AnimationClip? LoadFightAnimation(int dex, string formId, string uniqueId)
    {
        _offsets.TryGetValue(uniqueId, out var offset);
        var fightPath = ResolveFightSpritePath(dex, formId, offset);
        
        // Tenta carregar fight animation usando apenas linha 3 (direita, 0-based = 2)
        // Usaremos flip para virar quando necessário, igual ao walk fallback
        if (!string.IsNullOrEmpty(fightPath) && File.Exists(fightPath))
        {
            // Tenta carregar apenas linha 3 (direita)
            var fightRight = LoadClipFromPath(dex, formId, uniqueId, fightPath, offset, AnimationType.Fight, new[] { 2 }, null, false);
            if (fightRight != null)
                return fightRight;
            
            // Se nÃ£o funcionar, tenta carregar linha 4 (0-based = 3, frente)
            var fightFront = LoadClipFromPath(dex, formId, uniqueId, fightPath, offset, AnimationType.Fight, new[] { 3 }, null, false);
            if (fightFront != null)
                return fightFront;
            
            // Último recurso: carrega primeira linha disponível
            var fightClip = LoadClipFromPath(dex, formId, uniqueId, fightPath, offset, AnimationType.Fight, new[] { 0 }, null, false);
            if (fightClip != null)
                return fightClip;
        }

        // Fallback: usa walk animation linha 4 (0-based = 3) com colunas centrais
        var walkPath = ResolveSpritePath(dex, formId, offset, AnimationType.Walk);
        var walkFallback = LoadClipFromPath(
            dex,
            formId,
            uniqueId,
            walkPath,
            offset,
            AnimationType.Fight,
            new[] { 3 },
            new[] { 1, 2, 3 },
            true);
        if (walkFallback != null)
            return walkFallback;

        return LoadAnimation(dex, formId, AnimationType.Idle);
    }

    private AnimationClip? LoadClipFromPath(
        int dex,
        string formId,
        string uniqueId,
        string? spritePath,
        OffsetAdjustment? offset,
        AnimationType type,
        int[]? rowsToUse,
        int[]? columnsToUse,
        bool requireSelection)
    {
        if (string.IsNullOrWhiteSpace(spritePath) || !File.Exists(spritePath))
            return null;

        var sheet = LoadBitmap(spritePath);
        var (grid, frame) = GetGridAndFrame(sheet, spritePath, offset, type);

        if (grid.Rows <= 0 || grid.Columns <= 0 || frame.Width <= 0 || frame.Height <= 0)
            return null;

        var rows = FilterSelection(rowsToUse, grid.Rows);
        var cols = FilterSelection(columnsToUse, grid.Columns);
        if (requireSelection)
        {
            if (rowsToUse != null && rows.Count == 0)
                return null;
            if (columnsToUse != null && cols.Count == 0)
                return null;
        }

        if (rows.Count == 0)
            rows = Enumerable.Range(0, grid.Rows).ToList();
        if (cols.Count == 0)
            cols = Enumerable.Range(0, grid.Columns).ToList();

        var frames = BuildAnimationFrames(sheet, grid, frame, rows, cols);
        if (frames.Count == 0)
            return null;

        var groundOffset = offset?.GroundOffsetY ?? 0;
        groundOffset = Math.Clamp(groundOffset, 0, frame.Height);
        var groundLine = frame.Height - groundOffset;
        var groundLines = Enumerable.Repeat((double)groundLine, frames.Count).ToList();

        var clipName = $"{type}-{uniqueId}";
        return new AnimationClip(clipName, frames, groundLines);
    }

    private string? ResolveSpritePath(int dex, string formId, OffsetAdjustment? offset, AnimationType type)
    {
        var dexPath = dex.ToString("D4");
        var dir = Path.Combine(_spriteBasePath, dexPath);
        if (!Directory.Exists(dir))
            return null;
        
        // Se não é forma base, procurar na subpasta
        if (formId != "0000")
        {
            var formDir = Path.Combine(dir, formId);
            if (Directory.Exists(formDir))
            {
                dir = formDir;
            }
        }

        var order = new List<string>();
        if (offset != null)
        {
            if (type == AnimationType.Walk && !string.IsNullOrWhiteSpace(offset.WalkSpriteFile))
                order.Add(offset.WalkSpriteFile);
            if (type == AnimationType.Idle && !string.IsNullOrWhiteSpace(offset.IdleSpriteFile))
                order.Add(offset.IdleSpriteFile);
            if (!string.IsNullOrWhiteSpace(offset.PrimarySpriteFile))
                order.Add(offset.PrimarySpriteFile);
        }

        if (type == AnimationType.Walk)
        {
            order.AddRange(new[] { "Walk-Anim.png", "Idle-Anim.png", "Sleep.png" });
        }
        else if (type == AnimationType.Idle)
        {
            order.AddRange(new[] { "Idle-Anim.png", "Walk-Anim.png", "Sleep.png" });
        }
        else if (type == AnimationType.Sleep)
        {
            order.AddRange(new[] { "Sleep.png", "Idle-Anim.png", "Walk-Anim.png" });
        }
        else
        {
            order.AddRange(new[] { "Walk-Anim.png", "Idle-Anim.png", "Sleep.png" });
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in order)
        {
            if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
                continue;
            var path = Path.Combine(dir, name);
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private string? ResolveFightSpritePath(int dex, string formId, OffsetAdjustment? offset)
    {
        var dexPath = dex.ToString("D4");
        var dir = Path.Combine(_spriteBasePath, dexPath);
        if (!Directory.Exists(dir))
            return null;
        
        // Se não é forma base, procurar na subpasta
        if (formId != "0000")
        {
            var formDir = Path.Combine(dir, formId);
            if (Directory.Exists(formDir))
            {
                dir = formDir;
            }
        }

        var order = new List<string>();
        if (!string.IsNullOrWhiteSpace(offset?.FightSpriteFile))
            order.Add(offset.FightSpriteFile!);
        order.AddRange(FightCandidates);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in order)
        {
            if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
                continue;
            var path = Path.Combine(dir, name);
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static List<BitmapSource> BuildAnimationFrames(
        BitmapSource sheet,
        SpriteGrid grid,
        FrameSize frame,
        IReadOnlyList<int> rows,
        IReadOnlyList<int> cols)
    {
        var frames = new List<BitmapSource>();
        foreach (var row in rows)
        {
            foreach (var col in cols)
            {
                var srcX = col * frame.Width;
                var srcY = row * frame.Height;
                if (srcX + frame.Width > sheet.PixelWidth || srcY + frame.Height > sheet.PixelHeight)
                    continue;
                var frameRect = new Int32Rect(srcX, srcY, frame.Width, frame.Height);
                var cropped = new CroppedBitmap(sheet, frameRect);
                if (cropped.CanFreeze)
                    cropped.Freeze();
                frames.Add(cropped);
            }
        }

        return frames;
    }

    private static BitmapImage LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
        bitmap.EndInit();
        if (bitmap.CanFreeze)
            bitmap.Freeze();
        return bitmap;
    }

    private static (SpriteGrid Grid, FrameSize Frame) GetGridAndFrame(
        BitmapSource sheet,
        string spritePath,
        OffsetAdjustment? offset,
        AnimationType type)
    {
        var preferStandardGrid = PreferStandardGrid(spritePath);
        if (TryGetFrameSizeFromXml(spritePath, out var xmlFrame) &&
            TryBuildGridFromFrame(sheet, xmlFrame, out var xmlGrid))
        {
            return (xmlGrid, xmlFrame);
        }

        var ignoreStoredGrid = type == AnimationType.Walk || type == AnimationType.Fight;
        if (!ignoreStoredGrid && offset != null && TryGetStoredFrameGrid(offset, sheet, out var storedGrid, out var storedFrame))
        {
            return (storedGrid, storedFrame);
        }

        var buffer = BuildPixelBuffer(sheet);
        var detected = SpriteSheetAnalyzer.DetectGrid(buffer, preferStandardGrid);
        if (ShouldFallbackToFrameSize(offset, sheet, detected))
        {
            var frameW = offset!.FrameWidth!.Value;
            var frameH = offset.FrameHeight!.Value;
            var cols = sheet.PixelWidth / frameW;
            var rows = sheet.PixelHeight / frameH;
            return (new SpriteGrid(cols, rows), new FrameSize(frameW, frameH));
        }

        return detected;
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

    private static bool TryGetStoredFrameGrid(OffsetAdjustment offset, BitmapSource sheet, out SpriteGrid grid, out FrameSize frame)
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

        var totalW = frameW.Value * cols.Value;
        var totalH = frameH.Value * rows.Value;
        if (totalW != sheet.PixelWidth || totalH != sheet.PixelHeight)
        {
            grid = new SpriteGrid(1, 1);
            frame = new FrameSize(0, 0);
            return false;
        }

        grid = new SpriteGrid(cols.Value, rows.Value);
        frame = new FrameSize(frameW.Value, frameH.Value);
        return true;
    }

    private static List<int> FilterSelection(int[]? selection, int maxExclusive)
    {
        var result = new List<int>();
        if (selection == null)
            return result;

        foreach (var value in selection)
        {
            if (value < 0 || value >= maxExclusive)
                continue;
            if (!result.Contains(value))
                result.Add(value);
        }

        return result;
    }

    private static bool ShouldFallbackToFrameSize(OffsetAdjustment? offset, BitmapSource sheet, (SpriteGrid Grid, FrameSize Frame) detected)
    {
        if (offset?.FrameWidth is not > 0 || offset.FrameHeight is not > 0)
            return false;

        var frameW = offset.FrameWidth.Value;
        var frameH = offset.FrameHeight.Value;
        if (sheet.PixelWidth % frameW != 0 || sheet.PixelHeight % frameH != 0)
            return false;

        var cols = sheet.PixelWidth / frameW;
        var rows = sheet.PixelHeight / frameH;
        if (cols <= 0 || rows <= 0)
            return false;

        if (detected.Grid.Columns == 1 && detected.Grid.Rows == 1 && (cols > 1 || rows > 1))
            return true;

        return false;
    }

    private static bool TryBuildGridFromFrame(BitmapSource sheet, FrameSize frame, out SpriteGrid grid)
    {
        grid = new SpriteGrid(1, 1);
        if (frame.Width <= 0 || frame.Height <= 0)
            return false;

        if (sheet.PixelWidth % frame.Width != 0 || sheet.PixelHeight % frame.Height != 0)
            return false;

        var cols = sheet.PixelWidth / frame.Width;
        var rows = sheet.PixelHeight / frame.Height;
        if (cols <= 0 || rows <= 0)
            return false;

        grid = new SpriteGrid(cols, rows);
        return true;
    }

    private static bool TryGetFrameSizeFromXml(string spritePath, out FrameSize frame)
    {
        frame = new FrameSize(0, 0);
        var dir = Path.GetDirectoryName(spritePath);
        if (string.IsNullOrWhiteSpace(dir))
            return false;

        var animDataPath = Path.Combine(dir, "AnimData.xml");
        if (!File.Exists(animDataPath))
            return false;

        var name = Path.GetFileName(spritePath);
        var desiredNames = GetAnimNamesForFile(name);
        if (desiredNames.Count == 0)
            return false;

        try
        {
            var doc = XDocument.Load(animDataPath);
            var anims = doc.Descendants("Anim").ToList();
            foreach (var desired in desiredNames)
            {
                var match = FindAnimByName(anims, desired);
                if (match == null)
                    continue;

                if (TryResolveAnimFrame(match, anims, out frame))
                    return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static List<string> GetAnimNamesForFile(string fileName)
    {
        var names = new List<string>();
        if (string.Equals(fileName, "Walk-Anim.png", StringComparison.OrdinalIgnoreCase))
        {
            names.Add("Walk");
            return names;
        }

        if (string.Equals(fileName, "Idle-Anim.png", StringComparison.OrdinalIgnoreCase))
        {
            names.Add("Idle");
            return names;
        }

        if (string.Equals(fileName, "Sleep.png", StringComparison.OrdinalIgnoreCase))
        {
            names.Add("Sleep");
            return names;
        }

        var isFight = false;
        if (string.Equals(fileName, "Attack-Anim.png", StringComparison.OrdinalIgnoreCase))
        {
            names.Add("Attack");
            isFight = true;
        }
        else if (string.Equals(fileName, "Strike-Anim.png", StringComparison.OrdinalIgnoreCase))
        {
            names.Add("Strike");
            isFight = true;
        }
        else if (string.Equals(fileName, "QuickStrike-Anim.png", StringComparison.OrdinalIgnoreCase))
        {
            names.Add("QuickStrike");
            isFight = true;
        }
        else if (string.Equals(fileName, "MultiStrike-Anim.png", StringComparison.OrdinalIgnoreCase))
        {
            names.Add("MultiStrike");
            names.Add("MultiScratch");
            isFight = true;
        }
        else if (string.Equals(fileName, "MultiScratch-Anim.png", StringComparison.OrdinalIgnoreCase))
        {
            names.Add("MultiScratch");
            names.Add("MultiStrike");
            isFight = true;
        }
        else if (string.Equals(fileName, "Scratch-Anim.png", StringComparison.OrdinalIgnoreCase))
        {
            names.Add("Scratch");
            isFight = true;
        }
        else
        {
            return names;
        }

        if (isFight && !names.Contains("Attack", StringComparer.OrdinalIgnoreCase))
            names.Add("Attack");

        return names;
    }

    private static XElement? FindAnimByName(IEnumerable<XElement> anims, string name)
    {
        return anims.FirstOrDefault(a =>
            string.Equals((string?)a.Element("Name"), name, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryResolveAnimFrame(XElement anim, List<XElement> anims, out FrameSize frame)
    {
        frame = new FrameSize(0, 0);
        if (TryParseFrame(anim, out frame))
            return true;

        var copyOf = (string?)anim.Element("CopyOf");
        if (string.IsNullOrWhiteSpace(copyOf))
            return false;

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return TryResolveAnimFrameByName(copyOf, anims, visited, out frame);
    }

    private static bool TryResolveAnimFrameByName(string name, List<XElement> anims, HashSet<string> visited, out FrameSize frame)
    {
        frame = new FrameSize(0, 0);
        if (!visited.Add(name))
            return false;

        var match = FindAnimByName(anims, name);
        if (match == null)
            return false;

        if (TryParseFrame(match, out frame))
            return true;

        var copyOf = (string?)match.Element("CopyOf");
        if (string.IsNullOrWhiteSpace(copyOf))
            return false;

        return TryResolveAnimFrameByName(copyOf, anims, visited, out frame);
    }

    private static bool TryParseFrame(XElement anim, out FrameSize frame)
    {
        frame = new FrameSize(0, 0);
        if (int.TryParse(anim.Element("FrameWidth")?.Value, out var w) &&
            int.TryParse(anim.Element("FrameHeight")?.Value, out var h) &&
            w > 0 && h > 0)
        {
            frame = new FrameSize(w, h);
            return true;
        }

        return false;
    }
}
