using System.Text.Json;
using Pokebar.Core.Models;

namespace Pokebar.Core.Serialization;

public record OffsetAdjustment(
    int DexNumber,
    int GroundOffsetY,
    int CenterOffsetX,
    bool Reviewed,
    int HitboxX,
    int HitboxY,
    int HitboxWidth,
    int HitboxHeight,
    int? FrameWidth = null,
    int? FrameHeight = null,
    int? GridColumns = null,
    int? GridRows = null,
    string? PrimarySpriteFile = null,
    string? WalkSpriteFile = null,
    string? IdleSpriteFile = null,
    string? FightSpriteFile = null,
    bool HasAttackAnimation = false);

public static class FinalOffsets
{
    public static IReadOnlyDictionary<int, OffsetAdjustment> Load(string path)
    {
        if (!File.Exists(path)) return new Dictionary<int, OffsetAdjustment>();
        var json = File.ReadAllText(path);
        var items = JsonSerializer.Deserialize<OffsetAdjustment[]>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Array.Empty<OffsetAdjustment>();

        // Em caso de chaves duplicadas no arquivo, mantÃ©m a Ãºltima ocorrÃªncia.
        return items
            .GroupBy(i => i.DexNumber)
            .ToDictionary(g => g.Key, g => g.Last());
    }
}
