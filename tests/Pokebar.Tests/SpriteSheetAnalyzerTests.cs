using Pokebar.Core.Models;
using Pokebar.Core.Sprites;
using Xunit;

namespace Pokebar.Tests;

public class SpriteSheetAnalyzerTests
{
    [Fact]
    public void DetectGrid_PrefersFourColumnsWhenSeparatorsPresent()
    {
        var buffer = CreateBuffer(4, 8, (x, _) => x == 0 ? (byte)255 : (byte)0);

        var (grid, frame) = SpriteSheetAnalyzer.DetectGrid(buffer, preferStandardGrid: true);

        Assert.Equal(4, grid.Columns);
        Assert.Equal(8, grid.Rows);
        Assert.Equal(1, frame.Width);
        Assert.Equal(1, frame.Height);
    }

    [Fact]
    public void DetectGrid_FallsBackToSixByEightWhenEmpty()
    {
        var buffer = CreateBuffer(6, 8, (_, _) => (byte)0);

        var (grid, frame) = SpriteSheetAnalyzer.DetectGrid(buffer, preferStandardGrid: true);

        Assert.Equal(6, grid.Columns);
        Assert.Equal(8, grid.Rows);
        Assert.Equal(1, frame.Width);
        Assert.Equal(1, frame.Height);
    }

    private static PixelBuffer CreateBuffer(int width, int height, Func<int, int, byte> alpha)
    {
        var stride = width * 4;
        var data = new byte[stride * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var idx = (y * stride) + (x * 4);
                data[idx + 3] = alpha(x, y);
            }
        }

        return new PixelBuffer(data, width, height, stride, 4);
    }
}
