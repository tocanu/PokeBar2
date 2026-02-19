using System.IO;
using Pokebar.Core.Models;
using Pokebar.Core.Serialization;
using Xunit;

namespace Pokebar.Tests;

public class CoreCriticalTests
{
    [Fact]
    public void FinalOffsetsLoad_LastDuplicateWins()
    {
        var json = """
            [
              { "uniqueId": "0001", "groundOffsetY": 1, "centerOffsetX": 0, "reviewed": false, "hitboxX": 0, "hitboxY": 0, "hitboxWidth": 0, "hitboxHeight": 0 },
              { "uniqueId": "0001", "groundOffsetY": 2, "centerOffsetX": 0, "reviewed": true,  "hitboxX": 0, "hitboxY": 0, "hitboxWidth": 0, "hitboxHeight": 0 }
            ]
            """;

        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json);
            var offsets = FinalOffsets.Load(path);
            Assert.Single(offsets);
            Assert.Equal(2, offsets["0001"].GroundOffsetY);
            Assert.True(offsets["0001"].Reviewed);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PokemonVariant_UniqueId_FormatsBaseAndForm()
    {
        var baseForm = new PokemonVariant(25, "0000");
        var altForm = new PokemonVariant(25, "0006");

        Assert.Equal("0025", baseForm.UniqueId);
        Assert.Equal("0025_0006", altForm.UniqueId);
    }
}
