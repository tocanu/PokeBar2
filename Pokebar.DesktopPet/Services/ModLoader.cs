using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Pokebar.Core.Models;
using Serilog;

namespace Pokebar.DesktopPet.Services;

/// <summary>
/// Carrega e gerencia mods/packs de sprites.
/// Mods são pastas em %AppData%/Pokebar/mods/ com um manifest.json.
/// </summary>
public class ModLoader
{
    private readonly ModConfig _config;
    private readonly string _modsBasePath;
    private readonly List<LoadedMod> _loadedMods = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public ModLoader(ModConfig config)
    {
        _config = config;
        _modsBasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Pokebar", config.ModsFolder);
    }

    /// <summary>Lista de mods carregados.</summary>
    public IReadOnlyList<LoadedMod> LoadedMods => _loadedMods;

    /// <summary>
    /// Escaneia a pasta de mods e carrega os que estão na lista de habilitados.
    /// </summary>
    public void LoadMods(IEnumerable<string> enabledModIds)
    {
        _loadedMods.Clear();

        if (!_config.Enabled || !Directory.Exists(_modsBasePath))
        {
            Log.Debug("ModLoader: Mods disabled or folder not found: {Path}", _modsBasePath);
            return;
        }

        var enabledSet = new HashSet<string>(enabledModIds, StringComparer.OrdinalIgnoreCase);
        var modDirs = Directory.GetDirectories(_modsBasePath);

        foreach (var modDir in modDirs)
        {
            if (_loadedMods.Count >= _config.MaxLoadedMods)
            {
                Log.Warning("ModLoader: Max loaded mods ({Max}) reached, skipping remaining", _config.MaxLoadedMods);
                break;
            }

            var manifestPath = Path.Combine(modDir, "manifest.json");
            if (!File.Exists(manifestPath))
                continue;

            try
            {
                var json = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<ModManifest>(json, JsonOptions);
                if (manifest == null || !manifest.IsValid)
                {
                    Log.Warning("ModLoader: Invalid manifest in {Dir}", modDir);
                    continue;
                }

                if (!enabledSet.Contains(manifest.Id))
                {
                    Log.Debug("ModLoader: Mod {Id} not enabled, skipping", manifest.Id);
                    continue;
                }

                var spritePath = Path.Combine(modDir, manifest.SpritePath);
                if (!Directory.Exists(spritePath))
                {
                    Log.Warning("ModLoader: Sprite path not found for mod {Id}: {Path}", manifest.Id, spritePath);
                    continue;
                }

                string? offsetsPath = null;
                if (!string.IsNullOrWhiteSpace(manifest.OffsetsFile))
                {
                    offsetsPath = Path.Combine(modDir, manifest.OffsetsFile);
                    if (!File.Exists(offsetsPath))
                    {
                        Log.Warning("ModLoader: Offsets file not found for mod {Id}: {Path}", manifest.Id, offsetsPath);
                        offsetsPath = null;
                    }
                }

                var loadedMod = new LoadedMod(manifest, modDir, spritePath, offsetsPath);
                _loadedMods.Add(loadedMod);
                Log.Information("ModLoader: Loaded mod {Id} v{Version} ({Name}) — {DexCount} dex overrides",
                    manifest.Id, manifest.Version, manifest.Name, manifest.DexOverrides.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ModLoader: Failed to load mod from {Dir}", modDir);
            }
        }

        Log.Information("ModLoader: {Count} mods loaded from {Path}", _loadedMods.Count, _modsBasePath);
    }

    /// <summary>
    /// Descobre todos os mods disponíveis (instalados) na pasta de mods.
    /// </summary>
    public List<ModManifest> DiscoverMods()
    {
        var mods = new List<ModManifest>();

        if (!Directory.Exists(_modsBasePath))
            return mods;

        foreach (var modDir in Directory.GetDirectories(_modsBasePath))
        {
            var manifestPath = Path.Combine(modDir, "manifest.json");
            if (!File.Exists(manifestPath))
                continue;

            try
            {
                var json = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<ModManifest>(json, JsonOptions);
                if (manifest != null && manifest.IsValid)
                    mods.Add(manifest);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "ModLoader: Could not read manifest from {Dir}", modDir);
            }
        }

        return mods;
    }

    /// <summary>
    /// Verifica se um dex number é substituído por algum mod carregado.
    /// Retorna o caminho de sprite alternativo, ou null se não.
    /// </summary>
    public string? GetModSpritePath(int dex)
    {
        foreach (var mod in _loadedMods)
        {
            if (mod.Manifest.DexOverrides.Contains(dex))
            {
                var dexPath = Path.Combine(mod.SpritePath, dex.ToString("D4"));
                if (Directory.Exists(dexPath))
                    return mod.SpritePath;
            }
        }
        return null;
    }

    /// <summary>
    /// Cria a pasta de mods se não existir.
    /// </summary>
    public void EnsureModsFolder()
    {
        if (!Directory.Exists(_modsBasePath))
        {
            Directory.CreateDirectory(_modsBasePath);
            Log.Debug("ModLoader: Created mods folder: {Path}", _modsBasePath);
        }
    }
}

/// <summary>
/// Representa um mod carregado com caminhos resolvidos.
/// </summary>
public record LoadedMod(
    ModManifest Manifest,
    string BasePath,
    string SpritePath,
    string? OffsetsPath);
