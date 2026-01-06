using System;
using System.Collections.Generic;
using System.Linq;
using Pokebar.Core.Models;

namespace Pokebar.Core.Sprites;

public readonly struct PixelBuffer
{
    private readonly byte[] _data;

    public PixelBuffer(byte[] data, int width, int height, int stride, int bytesPerPixel)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (bytesPerPixel <= 0) throw new ArgumentOutOfRangeException(nameof(bytesPerPixel));
        if (stride < width * bytesPerPixel) throw new ArgumentOutOfRangeException(nameof(stride));
        if (data.Length < stride * height)
        {
            throw new ArgumentException("Pixel buffer is smaller than stride * height.", nameof(data));
        }

        _data = data;
        Width = width;
        Height = height;
        Stride = stride;
        BytesPerPixel = bytesPerPixel;
    }

    public ReadOnlySpan<byte> Data => _data;
    public int Width { get; }
    public int Height { get; }
    public int Stride { get; }
    public int BytesPerPixel { get; }
}

public static class SpriteSheetAnalyzer
{
    public static (SpriteGrid Grid, FrameSize Frame) DetectGrid(PixelBuffer buffer, bool preferStandardGrid)
    {
        var width = buffer.Width;
        var height = buffer.Height;

        if (preferStandardGrid && height % 8 == 0)
        {
            var rows = 8;
            var colCandidates = Enumerable.Range(1, Math.Min(12, width))
                .Where(c => width % c == 0);

            var (grid, frame) = PickBestGrid(buffer, colCandidates, new[] { rows });
            if (grid.Rows == rows)
            {
                if (grid.Columns == 1 && HasFourColumnSeparators(buffer))
                {
                    return (new SpriteGrid(4, 8), new FrameSize(width / 4, height / 8));
                }

                return (grid, frame);
            }
        }

        if (preferStandardGrid)
        {
            if (width % 6 == 0 && height % 8 == 0)
            {
                return (new SpriteGrid(6, 8), new FrameSize(width / 6, height / 8));
            }

            if (width % 4 == 0 && height % 2 == 0)
            {
                return (new SpriteGrid(4, 2), new FrameSize(width / 4, height / 2));
            }

            if (width % 4 == 0)
            {
                return (new SpriteGrid(4, 1), new FrameSize(width / 4, height));
            }
        }

        var colCandidatesGeneric = Enumerable.Range(1, Math.Min(8, width))
            .Where(c => width % c == 0);
        var rowCandidatesGeneric = Enumerable.Range(1, Math.Min(8, height))
            .Where(r => height % r == 0);

        return PickBestGrid(buffer, colCandidatesGeneric, rowCandidatesGeneric);
    }

    public static SpriteOffsets ComputeOffsets(PixelBuffer buffer, SpriteGrid grid, FrameSize frame, int[]? rowsToUse = null)
    {
        var data = buffer.Data;
        var stride = buffer.Stride;
        var bpp = buffer.BytesPerPixel;

        var groundOffsetMax = 0;
        double centerOffsetAccum = 0;
        var framesCount = 0;

        HashSet<int>? allowedRows = null;
        if (rowsToUse is not null)
        {
            allowedRows = rowsToUse.Where(r => r >= 0 && r < grid.Rows).ToHashSet();
            if (allowedRows.Count == 0)
            {
                allowedRows = null;
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
                    var rowHasPixel = false;

                    for (var x = 0; x < frame.Width; x++)
                    {
                        var ptrX = frameOriginX + x;
                        var idx = (ptrY * stride) + (ptrX * bpp);
                        var alpha = bpp >= 4 ? data[idx + 3] : (byte)255;
                        if (alpha > 0)
                        {
                            rowHasPixel = true;
                            groundOffset = Math.Min(groundOffset, frame.Height - 1 - y);
                            minX = Math.Min(minX, x);
                            maxX = Math.Max(maxX, x);
                        }
                    }

                    if (rowHasPixel && groundOffset != frame.Height)
                    {
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

        var centerOffset = framesCount > 0
            ? (int)Math.Round(centerOffsetAccum / framesCount)
            : 0;

        return new SpriteOffsets(groundOffsetMax, centerOffset);
    }

    private static (SpriteGrid Grid, FrameSize Frame) PickBestGrid(PixelBuffer buffer, IEnumerable<int> colCandidates, IEnumerable<int> rowCandidates)
    {
        var data = buffer.Data;
        var stride = buffer.Stride;
        var bpp = buffer.BytesPerPixel;

        double bestScore = double.MaxValue;
        SpriteGrid bestGrid = new(1, 1);
        FrameSize bestFrame = new(buffer.Width, buffer.Height);

        foreach (var cols in colCandidates)
        {
            foreach (var rows in rowCandidates)
            {
                var frameW = buffer.Width / cols;
                var frameH = buffer.Height / rows;
                if (frameW == 0 || frameH == 0) continue;

                var score = EvaluateGrid(data, stride, bpp, cols, rows, frameW, frameH);

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

    private static double EvaluateGrid(ReadOnlySpan<byte> data, int stride, int bpp, int cols, int rows, int frameW, int frameH)
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
                        var idx = (ptrY * stride) + (ptrX * bpp);
                        var alpha = bpp >= 4 ? data[idx + 3] : (byte)255;
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

        var variation = (maxW - minW) + (maxH - minH);
        var emptyPenalty = empty * 10000;
        return variation + emptyPenalty;
    }

    private static bool HasFourColumnSeparators(PixelBuffer buffer)
    {
        if (buffer.BytesPerPixel < 4)
            return false;

        if (buffer.Width % 4 != 0 || buffer.Height % 8 != 0)
            return false;

        var frameW = buffer.Width / 4;
        return HasTransparentColumnNear(buffer, frameW)
            && HasTransparentColumnNear(buffer, frameW * 2)
            && HasTransparentColumnNear(buffer, frameW * 3);
    }

    private static bool HasTransparentColumnNear(PixelBuffer buffer, int x)
    {
        for (var dx = -1; dx <= 1; dx++)
        {
            if (IsTransparentColumn(buffer, x + dx))
                return true;
        }

        return false;
    }

    private static bool IsTransparentColumn(PixelBuffer buffer, int x)
    {
        if (x < 0 || x >= buffer.Width)
            return false;

        var data = buffer.Data;
        var stride = buffer.Stride;
        var bpp = buffer.BytesPerPixel;

        for (var y = 0; y < buffer.Height; y++)
        {
            var idx = (y * stride) + (x * bpp);
            var alpha = data[idx + 3];
            if (alpha > 0)
                return false;
        }

        return true;
    }
}
