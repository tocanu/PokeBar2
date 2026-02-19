using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Pokebar.Core.Localization;

/// <summary>
/// Sistema de localização JSON-based.
/// Carrega strings de arquivos locale/{culture}.json.
/// Fallback: chave solicitada → en-US → chave literal.
/// Thread-safe para leitura após inicialização.
/// </summary>
public sealed class Localizer
{
    private static Localizer? _instance;
    private static readonly object _lock = new();

    private readonly Dictionary<string, string> _strings;
    private readonly Dictionary<string, string> _fallback;
    private readonly string _culture;

    /// <summary>
    /// Cultura ativa (ex: "pt-BR", "en-US").
    /// </summary>
    public string Culture => _culture;

    /// <summary>
    /// Culturas suportadas disponíveis em disco.
    /// </summary>
    public static IReadOnlyList<string> AvailableCultures => _availableCultures;
    private static List<string> _availableCultures = new() { "en-US", "pt-BR" };

    private Localizer(string culture, Dictionary<string, string> strings, Dictionary<string, string> fallback)
    {
        _culture = culture;
        _strings = strings;
        _fallback = fallback;
    }

    /// <summary>
    /// Instância global do localizer.
    /// </summary>
    public static Localizer Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= Initialize(DetectCulture());
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Atalho estático: Localizer.Get("key") ou Localizer.Get("key", "param1", value1)
    /// </summary>
    public static string Get(string key) => Instance.Resolve(key);

    /// <summary>
    /// Atalho com formatação: Localizer.Get("key", arg0, arg1, ...)
    /// </summary>
    public static string Get(string key, params object[] args)
    {
        var template = Instance.Resolve(key);
        try
        {
            return string.Format(template, args);
        }
        catch
        {
            return template;
        }
    }

    /// <summary>
    /// Resolve uma chave para a string localizada.
    /// Fallback: cultura atual → en-US → chave literal.
    /// </summary>
    public string Resolve(string key)
    {
        if (_strings.TryGetValue(key, out var value))
            return value;
        if (_fallback.TryGetValue(key, out var fbValue))
            return fbValue;
        return key;
    }

    /// <summary>
    /// Re-inicializa com uma cultura diferente.
    /// </summary>
    public static void SetCulture(string culture)
    {
        lock (_lock)
        {
            _instance = Initialize(culture);
        }
    }

    /// <summary>
    /// Detecta cultura do sistema ou usa configuração salva.
    /// </summary>
    public static string DetectCulture()
    {
        // Verificar se há preferência salva no AppSettings
        var settingsPath = GetSettingsPath();
        if (File.Exists(settingsPath))
        {
            try
            {
                var json = File.ReadAllText(settingsPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("language", out var langProp))
                {
                    var saved = langProp.GetString();
                    if (!string.IsNullOrEmpty(saved))
                        return saved;
                }
            }
            catch { /* ignore, fallback to system */ }
        }

        // Fallback: cultura do sistema
        var systemCulture = CultureInfo.CurrentUICulture.Name;
        if (systemCulture.StartsWith("pt", StringComparison.OrdinalIgnoreCase))
            return "pt-BR";
        return "en-US";
    }

    private static Localizer Initialize(string culture)
    {
        var strings = LoadLocaleFile(culture);
        var fallback = culture == "en-US"
            ? new Dictionary<string, string>()
            : LoadLocaleFile("en-US");

        // Atualizar lista de culturas disponíveis
        _availableCultures = ScanAvailableCultures();

        Trace.TraceInformation("Localizer initialized: culture={0}, keys={1}, fallback={2}",
            culture, strings.Count, fallback.Count);

        return new Localizer(culture, strings, fallback);
    }

    private static Dictionary<string, string> LoadLocaleFile(string culture)
    {
        var candidates = GetLocalePaths(culture);
        foreach (var path in candidates)
        {
            if (!File.Exists(path))
                continue;

            try
            {
                var json = File.ReadAllText(path);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (dict != null)
                {
                    Trace.TraceInformation("Loaded locale '{0}' from '{1}' ({2} keys)", culture, path, dict.Count);
                    return dict;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to load locale '{0}' from '{1}': {2}", culture, path, ex);
            }
        }

        // Retornar dicionário embutido como fallback final
        return GetBuiltinStrings(culture);
    }

    private static string[] GetLocalePaths(string culture)
    {
        var baseDir = AppContext.BaseDirectory;
        return new[]
        {
            Path.Combine(baseDir, "locale", $"{culture}.json"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "locale", $"{culture}.json")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "locale", $"{culture}.json")),
        };
    }

    private static List<string> ScanAvailableCultures()
    {
        var cultures = new HashSet<string> { "en-US", "pt-BR" };
        var baseDir = AppContext.BaseDirectory;
        var dirs = new[]
        {
            Path.Combine(baseDir, "locale"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "locale")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "locale")),
        };

        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir))
                continue;
            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (name.Length >= 2)
                    cultures.Add(name);
            }
        }

        return new List<string>(cultures);
    }

    private static string GetSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Pokebar", "settings.json");
    }

    /// <summary>
    /// Strings embutidas no código como fallback definitivo (caso os JSONs não existam).
    /// Apenas as strings mais críticas (erros fatais).
    /// </summary>
    private static Dictionary<string, string> GetBuiltinStrings(string culture)
    {
        if (culture.StartsWith("pt", StringComparison.OrdinalIgnoreCase))
        {
            return new Dictionary<string, string>
            {
                ["error.fatal.title"] = "Pokebar - Erro Fatal",
                ["error.fatal.message"] = "Erro fatal não tratado:\n{0}\n\nO aplicativo será encerrado.\nVerifique os logs em %AppData%\\Pokebar\\Logs",
                ["error.ui.title"] = "Pokebar - Erro",
                ["error.ui.message"] = "Erro não tratado na interface:\n{0}\n\nVerifique os logs em %AppData%\\Pokebar\\Logs",
                ["diagnostic.generating"] = "Gerando diagnóstico...",
                ["diagnostic.success"] = "Diagnóstico salvo em:\n{0}",
                ["diagnostic.fail"] = "Falha ao gerar diagnóstico.",
                ["profile.default"] = "Padrão",
                ["profile.work"] = "Trabalho",
                ["profile.gaming"] = "Jogo",
                ["profile.stream"] = "Stream",
            };
        }

        return new Dictionary<string, string>
        {
            ["error.fatal.title"] = "Pokebar - Fatal Error",
            ["error.fatal.message"] = "Unhandled fatal error:\n{0}\n\nThe application will close.\nCheck logs at %AppData%\\Pokebar\\Logs",
            ["error.ui.title"] = "Pokebar - Error",
            ["error.ui.message"] = "Unhandled UI error:\n{0}\n\nCheck logs at %AppData%\\Pokebar\\Logs",
            ["diagnostic.generating"] = "Generating diagnostic...",
            ["diagnostic.success"] = "Diagnostic saved to:\n{0}",
            ["diagnostic.fail"] = "Failed to generate diagnostic.",
            ["profile.default"] = "Default",
            ["profile.work"] = "Work",
            ["profile.gaming"] = "Gaming",
            ["profile.stream"] = "Stream",
        };
    }
}
