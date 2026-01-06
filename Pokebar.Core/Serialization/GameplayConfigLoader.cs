using System.Text.Json;
using Pokebar.Core.Models;

namespace Pokebar.Core.Serialization;

/// <summary>
/// Carrega e salva configurações de gameplay em JSON.
/// </summary>
public static class GameplayConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// Carrega configurações de um arquivo JSON.
    /// Se o arquivo não existir ou houver erro, retorna configuração padrão.
    /// </summary>
    public static GameplayConfig Load(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return new GameplayConfig();

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<GameplayConfig>(json, JsonOptions) 
                   ?? new GameplayConfig();
        }
        catch (Exception)
        {
            // Em caso de erro, retorna configuração padrão
            return new GameplayConfig();
        }
    }

    /// <summary>
    /// Salva configurações em um arquivo JSON.
    /// </summary>
    public static void Save(string filePath, GameplayConfig config)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Cria um arquivo de configuração padrão com comentários úteis.
    /// </summary>
    public static void CreateDefault(string filePath)
    {
        var config = new GameplayConfig();
        Save(filePath, config);
    }

    /// <summary>
    /// Obtém o caminho padrão para o arquivo de configuração.
    /// </summary>
    public static string GetDefaultConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "Pokebar");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "gameplay.json");
    }

    /// <summary>
    /// Carrega configuração do caminho padrão ou cria se não existir.
    /// </summary>
    public static GameplayConfig LoadOrCreateDefault()
    {
        var path = GetDefaultConfigPath();
        if (!File.Exists(path))
        {
            var config = new GameplayConfig();
            Save(path, config);
            return config;
        }
        return Load(path);
    }
}
