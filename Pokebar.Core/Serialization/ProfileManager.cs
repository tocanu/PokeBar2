using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Pokebar.Core.Models;

namespace Pokebar.Core.Serialization;

/// <summary>
/// Gerencia perfis (profiles) e configurações globais (AppSettings).
/// Cada perfil é um GameplayConfig separado salvo em gameplay_{profileId}.json.
/// AppSettings (idioma, perfil ativo) salvo em settings.json.
///
/// Estrutura em %AppData%/Pokebar:
///   settings.json                  ← AppSettings (global)
///   gameplay.json                  ← config do perfil "default" (retrocompatível)
///   gameplay_work.json             ← config do perfil "work"
///   gameplay_stream.json           ← config do perfil "stream"
///   save.json                      ← SaveData (compartilhado entre perfis)
/// </summary>
public static class ProfileManager
{
    private const string SettingsFileName = "settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// Carrega AppSettings. Cria com defaults se não existir.
    /// </summary>
    public static AppSettings LoadSettings()
    {
        var path = GetSettingsPath();
        try
        {
            if (!File.Exists(path))
            {
                var settings = CreateDefaultSettings();
                SaveSettings(settings);
                return settings;
            }

            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return loaded ?? CreateDefaultSettings();
        }
        catch (Exception ex)
        {
            Trace.TraceError("Failed to load settings: {0}", ex);
            return CreateDefaultSettings();
        }
    }

    /// <summary>
    /// Salva AppSettings.
    /// </summary>
    public static void SaveSettings(AppSettings settings)
    {
        try
        {
            var path = GetSettingsPath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Trace.TraceError("Failed to save settings: {0}", ex);
        }
    }

    /// <summary>
    /// Carrega o GameplayConfig do perfil ativo.
    /// </summary>
    public static GameplayConfig LoadActiveProfile(AppSettings settings)
    {
        return LoadProfile(settings.ActiveProfileId);
    }

    /// <summary>
    /// Carrega o GameplayConfig de um perfil específico.
    /// </summary>
    public static GameplayConfig LoadProfile(string profileId)
    {
        var path = GetProfileConfigPath(profileId);
        if (File.Exists(path))
            return GameplayConfigLoader.Load(path);

        // Se não existe, tentar criar a partir do default bundled ou hardcoded
        var config = GameplayConfigLoader.LoadOrCreateDefault();

        // Salvar como config deste perfil
        GameplayConfigLoader.Save(path, config);
        return config;
    }

    /// <summary>
    /// Salva o GameplayConfig de um perfil.
    /// </summary>
    public static void SaveProfile(string profileId, GameplayConfig config)
    {
        var path = GetProfileConfigPath(profileId);
        GameplayConfigLoader.Save(path, config);
    }

    /// <summary>
    /// Troca o perfil ativo. Retorna o novo GameplayConfig.
    /// </summary>
    public static (AppSettings settings, GameplayConfig config) SwitchProfile(AppSettings current, string profileId)
    {
        var profile = current.Profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile == null)
        {
            Trace.TraceWarning("Profile '{0}' not found, staying on '{1}'", profileId, current.ActiveProfileId);
            return (current, LoadActiveProfile(current));
        }

        var updated = current with { ActiveProfileId = profileId };
        SaveSettings(updated);
        var config = LoadProfile(profileId);

        Trace.TraceInformation("Switched to profile '{0}'", profileId);
        return (updated, config);
    }

    /// <summary>
    /// Cria um novo perfil. Opcionalmente copia config de outro perfil.
    /// </summary>
    public static AppSettings CreateProfile(AppSettings settings, string id, string name, string icon = "🎮", string? copyFromId = null)
    {
        if (settings.Profiles.Any(p => p.Id == id))
        {
            Trace.TraceWarning("Profile '{0}' already exists", id);
            return settings;
        }

        // Copiar config se solicitado
        if (copyFromId != null)
        {
            var sourceConfig = LoadProfile(copyFromId);
            SaveProfile(id, sourceConfig);
        }

        var newProfile = new ProfileEntry { Id = id, Name = name, Icon = icon };
        var profiles = new List<ProfileEntry>(settings.Profiles) { newProfile };
        var updated = settings with { Profiles = profiles };
        SaveSettings(updated);

        Trace.TraceInformation("Created profile '{0}' (name: {1})", id, name);
        return updated;
    }

    /// <summary>
    /// Remove um perfil. Não permite remover o perfil ativo nem o "default".
    /// </summary>
    public static AppSettings DeleteProfile(AppSettings settings, string id)
    {
        if (id == "default")
        {
            Trace.TraceWarning("Cannot delete default profile");
            return settings;
        }

        if (settings.ActiveProfileId == id)
        {
            Trace.TraceWarning("Cannot delete active profile '{0}'", id);
            return settings;
        }

        var profiles = settings.Profiles.Where(p => p.Id != id).ToList();
        var updated = settings with { Profiles = profiles };
        SaveSettings(updated);

        // Deletar config file
        var path = GetProfileConfigPath(id);
        if (File.Exists(path))
        {
            try { File.Delete(path); }
            catch { /* best effort */ }
        }

        Trace.TraceInformation("Deleted profile '{0}'", id);
        return updated;
    }

    /// <summary>
    /// Lista perfis disponíveis.
    /// </summary>
    public static IReadOnlyList<ProfileEntry> GetProfiles(AppSettings settings) => settings.Profiles;

    /// <summary>
    /// Obtém o caminho do config de um perfil.
    /// "default" → gameplay.json (retrocompatível)
    /// outro → gameplay_{id}.json
    /// </summary>
    public static string GetProfileConfigPath(string profileId)
    {
        var dir = GetAppDataDir();
        if (profileId == "default")
            return Path.Combine(dir, "gameplay.json");

        return Path.Combine(dir, $"gameplay_{profileId}.json");
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(GetAppDataDir(), SettingsFileName);
    }

    private static string GetAppDataDir()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "Pokebar");
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static AppSettings CreateDefaultSettings()
    {
        var settings = new AppSettings
        {
            Profiles = new List<ProfileEntry>
            {
                new() { Id = "default", Name = "profile.default", Icon = "🎮" },
                new() { Id = "work", Name = "profile.work", Icon = "💼" },
                new() { Id = "stream", Name = "profile.stream", Icon = "📺" },
            }
        };
        return settings;
    }
}
