using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pokebar.Core.Models;
using Serilog;

namespace Pokebar.DesktopPet.Services;

/// <summary>
/// Gerencia o sistema de evolução dos Pokémon.
/// Carrega tabela de evoluções de JSON e verifica condições.
/// </summary>
public class EvolutionService
{
    private readonly List<EvolutionEntry> _entries = new();
    private readonly Dictionary<int, List<EvolutionEntry>> _byFromDex = new();

    /// <summary>
    /// Carrega a tabela de evoluções de um arquivo JSON.
    /// </summary>
    public void LoadFromFile(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };
            var entries = JsonSerializer.Deserialize<List<EvolutionEntry>>(json, options);
            if (entries != null)
            {
                _entries.Clear();
                _byFromDex.Clear();
                foreach (var e in entries)
                {
                    _entries.Add(e);
                    if (!_byFromDex.TryGetValue(e.FromDex, out var list))
                    {
                        list = new List<EvolutionEntry>();
                        _byFromDex[e.FromDex] = list;
                    }
                    list.Add(e);
                }
                Log.Information("EvolutionService: loaded {Count} evolution entries", _entries.Count);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "EvolutionService: failed to load evolution data from {Path}", path);
        }
    }

    /// <summary>
    /// Verifica se o Pokémon atual pode evoluir por nível.
    /// Retorna a entrada de evolução se possível, ou null.
    /// </summary>
    public EvolutionEntry? CheckLevelEvolution(int currentDex, int currentLevel)
    {
        if (!_byFromDex.TryGetValue(currentDex, out var candidates))
            return null;

        return candidates.FirstOrDefault(e =>
            e.Method == EvolutionMethod.Level && currentLevel >= e.MinLevel);
    }

    /// <summary>
    /// Verifica se o Pokémon atual pode evoluir por amizade (requer level-up trigger).
    /// Retorna a entrada de evolução se possível, ou null.
    /// </summary>
    public EvolutionEntry? CheckFriendshipEvolution(int currentDex, int currentFriendship)
    {
        if (!_byFromDex.TryGetValue(currentDex, out var candidates))
            return null;

        return candidates.FirstOrDefault(e =>
            e.Method == EvolutionMethod.Friendship && currentFriendship >= e.MinFriendship);
    }

    /// <summary>
    /// Retorna todas as evoluções possíveis por pedra para o dex dado.
    /// </summary>
    public List<EvolutionEntry> GetStoneEvolutions(int currentDex)
    {
        if (!_byFromDex.TryGetValue(currentDex, out var candidates))
            return new List<EvolutionEntry>();

        return candidates.Where(e => e.Method == EvolutionMethod.Stone).ToList();
    }

    /// <summary>
    /// Verifica evolução por pedra específica.
    /// </summary>
    public EvolutionEntry? CheckStoneEvolution(int currentDex, EvolutionStone stone)
    {
        if (!_byFromDex.TryGetValue(currentDex, out var candidates))
            return null;

        return candidates.FirstOrDefault(e =>
            e.Method == EvolutionMethod.Stone && e.Stone == stone);
    }

    /// <summary>
    /// Verifica se um dex tem alguma evolução possível (qualquer método).
    /// </summary>
    public bool HasEvolution(int dex) => _byFromDex.ContainsKey(dex);

    /// <summary>
    /// Retorna todas as evoluções para um dex origem.
    /// </summary>
    public IReadOnlyList<EvolutionEntry> GetEvolutions(int fromDex)
    {
        return _byFromDex.TryGetValue(fromDex, out var list)
            ? list.AsReadOnly()
            : Array.Empty<EvolutionEntry>();
    }

    /// <summary>
    /// Gera uma pedra de evolução aleatória (para rewards).
    /// Pesos: pedras comuns (Fire/Water/Thunder/Leaf/Moon) mais prováveis.
    /// </summary>
    public static EvolutionStone GetRandomStone(Random rng)
    {
        var weighted = new (EvolutionStone Stone, int Weight)[]
        {
            (EvolutionStone.Fire, 20),
            (EvolutionStone.Water, 20),
            (EvolutionStone.Thunder, 20),
            (EvolutionStone.Leaf, 20),
            (EvolutionStone.Moon, 15),
            (EvolutionStone.Sun, 12),
            (EvolutionStone.Ice, 10),
            (EvolutionStone.Dusk, 8),
            (EvolutionStone.Dawn, 8),
            (EvolutionStone.Shiny, 5),
        };

        var total = weighted.Sum(w => w.Weight);
        var roll = rng.Next(total);
        var cumulative = 0;
        foreach (var (stone, weight) in weighted)
        {
            cumulative += weight;
            if (roll < cumulative)
                return stone;
        }
        return EvolutionStone.Fire;
    }
}
