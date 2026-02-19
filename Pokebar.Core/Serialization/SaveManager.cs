using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Pokebar.Core.Models;

namespace Pokebar.Core.Serialization;

/// <summary>
/// Gerencia save/load do progresso do jogador em JSON.
/// Arquivo salvo em %AppData%/Pokebar/save.json com backup automático.
/// </summary>
public static class SaveManager
{
    private const string SaveFileName = "save.json";
    private const string BackupFileName = "save.backup.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// Obtém o diretório de saves.
    /// </summary>
    public static string GetSaveDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "Pokebar");
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>
    /// Obtém o caminho completo do arquivo de save.
    /// </summary>
    public static string GetSavePath()
    {
        return Path.Combine(GetSaveDirectory(), SaveFileName);
    }

    /// <summary>
    /// Carrega o save. Se não existir ou estiver corrompido, retorna null.
    /// Tenta o backup se o principal falhar.
    /// </summary>
    public static SaveData? Load()
    {
        var path = GetSavePath();
        var data = TryLoadFrom(path);
        if (data != null)
            return data;

        // Tentar backup
        var backupPath = Path.Combine(GetSaveDirectory(), BackupFileName);
        data = TryLoadFrom(backupPath);
        if (data != null)
        {
            Trace.TraceWarning("Save principal corrompido, restaurado do backup: '{0}'", backupPath);
            // Restaurar o backup como principal
            try { File.Copy(backupPath, path, true); } catch { /* best effort */ }
        }

        return data;
    }

    /// <summary>
    /// Salva o progresso com backup atômico.
    /// Fluxo: escreve .tmp → move principal → .backup → .tmp → principal
    /// </summary>
    public static bool Save(SaveData data)
    {
        try
        {
            var dir = GetSaveDirectory();
            var path = Path.Combine(dir, SaveFileName);
            var backupPath = Path.Combine(dir, BackupFileName);
            var tmpPath = path + ".tmp";

            // Atualizar timestamp
            data = data with { LastSaved = DateTime.UtcNow };

            // Serializar para arquivo temporário
            var json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(tmpPath, json);

            // Backup do save anterior (se existir)
            if (File.Exists(path))
            {
                try { File.Copy(path, backupPath, true); } catch { /* best effort */ }
            }

            // Mover tmp → principal (atômico no mesmo volume)
            File.Move(tmpPath, path, true);

            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceError("Falha ao salvar progresso: {0}", ex);
            return false;
        }
    }

    /// <summary>
    /// Verifica se um save existe.
    /// </summary>
    public static bool SaveExists()
    {
        return File.Exists(GetSavePath());
    }

    /// <summary>
    /// Deleta o save e backup (reset completo).
    /// </summary>
    public static void DeleteSave()
    {
        var dir = GetSaveDirectory();
        var path = Path.Combine(dir, SaveFileName);
        var backupPath = Path.Combine(dir, BackupFileName);

        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(backupPath)) File.Delete(backupPath);
    }

    private static SaveData? TryLoadFrom(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<SaveData>(json, JsonOptions);

            if (data == null)
                return null;

            // Validação básica
            if (data.Version < 1 || data.ActiveDex <= 0)
            {
                Trace.TraceWarning("Save inválido em '{0}': Version={1}, ActiveDex={2}", 
                    path, data.Version, data.ActiveDex);
                return null;
            }

            return data;
        }
        catch (Exception ex)
        {
            Trace.TraceError("Falha ao carregar save de '{0}': {1}", path, ex);
            return null;
        }
    }
}
