using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Pokebar.DesktopPet.Services;

/// <summary>
/// Verifica novas versões no GitHub Releases e gerencia o download + instalação silenciosa.
///
/// Fluxo:
///   CheckAsync()            → compara versão atual com a última release do GitHub
///   DownloadInstallerAsync  → baixa o Setup.exe para %TEMP%
///   InstallAndRestart       → executa o instalador silencioso e fecha o app
///
/// Configuração: altere GithubOwner e GithubRepo para apontar ao seu repositório.
/// </summary>
public static class UpdateService
{
    // ── Configuração ──────────────────────────────────────────────────────────
    // Altere estes valores para o seu repositório GitHub.
    public const string GithubOwner = "tocanu";
    public const string GithubRepo  = "PokeBar2";
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly string ApiUrl =
        $"https://api.github.com/repos/{GithubOwner}/{GithubRepo}/releases/latest";

    // HttpClient estático compartilhado — padrão recomendado (.NET)
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20),
        DefaultRequestHeaders =
        {
            { "User-Agent",  "PokeBar-Updater/1.0" },
            { "Accept",      "application/vnd.github+json" },
            { "X-GitHub-Api-Version", "2022-11-28" },
        }
    };

    // ── Modelo de resposta da API ─────────────────────────────────────────────

    private record GitHubRelease(
        [property: JsonPropertyName("tag_name")]  string     TagName,
        [property: JsonPropertyName("body")]      string?    Body,
        [property: JsonPropertyName("prerelease")] bool      Prerelease,
        [property: JsonPropertyName("assets")]    GitHubAsset[] Assets);

    private record GitHubAsset(
        [property: JsonPropertyName("name")]                 string Name,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl,
        [property: JsonPropertyName("size")]                 long   Size);

    /// <summary>Informações da versão mais recente disponível para download.</summary>
    public record UpdateAvailableInfo(
        string Version,
        string DownloadUrl,
        long   SizeBytes,
        string ReleaseNotes);

    // ── API pública ───────────────────────────────────────────────────────────

    /// <summary>
    /// Versão atual do executável em execução (ex: 1.0.0).
    /// </summary>
    public static Version CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);

    /// <summary>
    /// Consulta a API do GitHub e retorna os dados da update se houver uma versão
    /// mais nova. Retorna null se já atualizado, em modo offline ou em caso de erro.
    /// </summary>
    public static async Task<UpdateAvailableInfo?> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            Log.Debug("Checking for updates at {Url}", ApiUrl);
            var release = await Http.GetFromJsonAsync<GitHubRelease>(ApiUrl, ct);
            if (release is null || release.Prerelease)
                return null;

            var latestVersion = ParseVersion(release.TagName);
            if (latestVersion is null)
            {
                Log.Warning("Could not parse GitHub tag '{Tag}' as Version", release.TagName);
                return null;
            }

            if (latestVersion <= CurrentVersion)
            {
                Log.Debug("Already up-to-date: {Current}", CurrentVersion);
                return null;
            }

            // Procura asset Setup.exe no release
            var asset = Array.Find(release.Assets, a =>
                a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

            if (asset is null)
            {
                Log.Warning("Update {Version} found but has no .exe asset", latestVersion);
                return null;
            }

            Log.Information("Update available: {Current} → {Latest}", CurrentVersion, latestVersion);
            return new UpdateAvailableInfo(
                latestVersion.ToString(3),
                asset.BrowserDownloadUrl,
                asset.Size,
                release.Body ?? "");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warning(ex, "Update check failed (offline or GitHub API error)");
            return null;
        }
    }

    /// <summary>
    /// Faz download do instalador para a pasta %TEMP%.
    /// Reporta progresso via <paramref name="progress"/> (0.0–1.0).
    /// Retorna o caminho do arquivo baixado.
    /// </summary>
    public static async Task<string> DownloadInstallerAsync(
        UpdateAvailableInfo info,
        IProgress<double>?  progress = null,
        CancellationToken   ct       = default)
    {
        var filename    = $"PokeBar-Setup-{info.Version}.exe";
        var destination = Path.Combine(Path.GetTempPath(), filename);

        Log.Information("Downloading update installer to {Path}", destination);

        // Se já existe um download anterior com o mesmo nome, usar ele diretamente
        if (File.Exists(destination))
        {
            var info2 = new FileInfo(destination);
            if (info.SizeBytes <= 0 || info2.Length == info.SizeBytes)
            {
                Log.Information("Installer already cached at {Path}", destination);
                progress?.Report(1.0);
                return destination;
            }
        }

        using var response = await Http.GetAsync(
            info.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? info.SizeBytes;
        await using var srcStream  = await response.Content.ReadAsStreamAsync(ct);
        await using var dstStream  = new FileStream(destination, FileMode.Create, FileAccess.Write,
                                         FileShare.None, 81920, useAsync: true);

        var buffer     = new byte[81920];
        long downloaded = 0;
        int  read;

        while ((read = await srcStream.ReadAsync(buffer, ct)) > 0)
        {
            await dstStream.WriteAsync(buffer.AsMemory(0, read), ct);
            downloaded += read;
            if (total > 0)
                progress?.Report((double)downloaded / total);
        }

        Log.Information("Download complete: {Path} ({Bytes} bytes)", destination, downloaded);
        return destination;
    }

    /// <summary>
    /// Lança o instalador em modo silencioso com fechamento automático do app atual.
    /// O instalador do Inno Setup (/SILENT) substitui os arquivos e reinicia o app.
    /// Este método encerra o processo atual imediatamente após lançar o instalador.
    /// </summary>
    public static void InstallAndRestart(string installerPath)
    {
        Log.Information("Launching installer silently: {Path}", installerPath);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName        = installerPath,
                // /SILENT         = sem wizard, mas mostra barra de progresso
                // /CLOSEAPPLICATIONS  = fecha instâncias rodando do app
                // /RESTARTAPPLICATIONS = reabre o app após a instalação
                Arguments       = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to launch installer");
            throw;
        }

        System.Windows.Application.Current.Shutdown();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Version? ParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var clean = tag.TrimStart('v', 'V').Trim();
        return Version.TryParse(clean, out var v) ? v : null;
    }
}
