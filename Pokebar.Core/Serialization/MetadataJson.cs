using System.Text.Json;
using System.Text.Json.Serialization;
using Pokebar.Core.Models;

namespace Pokebar.Core.Serialization;

/// <summary>
/// Utilitários centralizados para serializar/deserializar metadados de sprites.
/// Mantém opções consistentes (camelCase, ignore nulls, identado).
/// </summary>
public static class MetadataJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string ToJson(PokemonSpriteMetadata metadata) =>
        JsonSerializer.Serialize(metadata, Options);

    public static PokemonSpriteMetadata? FromJson(string json) =>
        JsonSerializer.Deserialize<PokemonSpriteMetadata>(json, Options);

    public static void SerializeToFile(PokemonSpriteMetadata metadata, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, ToJson(metadata));
    }

    public static PokemonSpriteMetadata? DeserializeFromFile(string path)
    {
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        return FromJson(json);
    }
}
