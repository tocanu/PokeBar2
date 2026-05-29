using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Serilog;
using WpfApp    = System.Windows.Application;
using WpfMsgBox = System.Windows.MessageBox;

namespace Pokebar.DesktopPet;

/// <summary>
/// Janela de configuração de primeira execução.
/// Baixa o repositório completo do PMD SpriteCollab do GitHub (~800 MB ZIP)
/// e extrai apenas a pasta sprite/ para {app}\SpriteCollab\sprite\.
/// Exibida automaticamente quando os sprites não são encontrados.
/// Após extração bem-sucedida, reinicia o app.
/// </summary>
public partial class SpriteSetupWindow : Window
{
    // ZIP do branch master do PMD SpriteCollab — contém todos os sprites de todas as gerações
    private const string SpritesZipUrl =
        "https://github.com/PMDCollab/SpriteCollab/archive/refs/heads/master.zip";

    // Caminho dentro do ZIP onde ficam os sprites (GitHub coloca pasta raiz "SpriteCollab-master/")
    private const string ZipSpritePrefix = "SpriteCollab-master/sprite/";

    private const string ZipFileName = "SpriteCollab-master.zip";

    private readonly CancellationTokenSource _cts = new();

    public SpriteSetupWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => _ = RunSetupAsync();
    }

    // ── Pipeline principal ────────────────────────────────────────────────────

    private async Task RunSetupAsync()
    {
        var ct         = _cts.Token;
        var spriteDest = Path.Combine(AppContext.BaseDirectory, "SpriteCollab", "sprite");
        var zipPath    = Path.Combine(Path.GetTempPath(), ZipFileName);

        try
        {
            // ── Fase 1: Download ──────────────────────────────────────────────
            SetStatus("Conectando ao GitHub (PMDCollab/SpriteCollab)...", indeterminate: true);
            Log.Information("Downloading full SpriteCollab from {Url}", SpritesZipUrl);

            using var http = new HttpClient { Timeout = TimeSpan.FromHours(2) };
            http.DefaultRequestHeaders.Add("User-Agent", "PokeBar-SpriteSetup/1.0");

            using var response = await http.GetAsync(
                SpritesZipUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            // GitHub pode não enviar Content-Length para archives grandes
            var total   = response.Content.Headers.ContentLength; // null se desconhecido
            long downloaded = 0;

            await using var src = await response.Content.ReadAsStreamAsync(ct);
            await using var dst = new FileStream(
                zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81_920, useAsync: true);

            var buffer = new byte[81_920];
            int  read;
            var  sw = Stopwatch.StartNew();

            while ((read = await src.ReadAsync(buffer, ct)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read), ct);
                downloaded += read;

                if (sw.ElapsedMilliseconds >= 300)
                {
                    sw.Restart();
                    var dlMb = downloaded / 1_048_576.0;

                    if (total.HasValue && total.Value > 0)
                    {
                        var pct   = downloaded * 100.0 / total.Value;
                        var totMb = total.Value / 1_048_576.0;
                        SetStatus($"Baixando sprites... {dlMb:F0} / {totMb:F0} MB", indeterminate: false, pct);
                    }
                    else
                    {
                        // Tamanho desconhecido — mostra só o que baixou
                        SetStatus($"Baixando sprites... {dlMb:F0} MB baixados", indeterminate: true);
                    }
                }
            }

            Log.Information("SpriteCollab ZIP downloaded ({MB:F1} MB)", downloaded / 1_048_576.0);

            // ── Fase 2: Extração ──────────────────────────────────────────────
            SetStatus("Extraindo sprites (isso pode demorar alguns minutos)...", indeterminate: true);

            await Task.Run(() => ExtractSprites(zipPath, spriteDest, ct), ct);

            // Apagar ZIP do temp (libera ~800 MB)
            try { File.Delete(zipPath); } catch { /* não crítico */ }

            Log.Information("Sprites extracted to {Dest}", spriteDest);

            // ── Fase 3: Reiniciar ─────────────────────────────────────────────
            SetStatus("Pronto! Reiniciando o PokeBar...", indeterminate: true);
            await Task.Delay(900, ct);

            var exePath = Process.GetCurrentProcess().MainModule?.FileName
                          ?? Path.Combine(AppContext.BaseDirectory, "Pokebar.DesktopPet.exe");

            Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
            WpfApp.Current.Shutdown();
        }
        catch (OperationCanceledException)
        {
            Log.Information("Sprite download cancelled by user");
            CleanupZip(zipPath);
            WpfApp.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Sprite download/extract failed");
            CleanupZip(zipPath);

            var retry = WpfMsgBox.Show(
                $"Falha ao baixar os sprites:\n{ex.Message}\n\n" +
                "Verifique a conexão e tente novamente.\n\n" +
                "Deseja tentar de novo?",
                "PokeBar — Erro",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (retry == MessageBoxResult.Yes)
                await RunSetupAsync();
            else
                WpfApp.Current.Shutdown();
        }
    }

    // ── Extração ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Extrai apenas as entradas sob "SpriteCollab-master/sprite/" do ZIP do GitHub,
    /// stripando o prefixo e colocando os arquivos diretamente em <paramref name="destDir"/>.
    /// </summary>
    private static void ExtractSprites(string zipPath, string destDir, CancellationToken ct)
    {
        Directory.CreateDirectory(destDir);
        var destRoot = Path.GetFullPath(destDir) + Path.DirectorySeparatorChar;

        using var zip = ZipFile.OpenRead(zipPath);
        var entries   = zip.Entries;
        int total     = entries.Count;
        int done      = 0;
        int extracted = 0;

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            done++;

            // Ignorar entradas fora de sprite/
            if (!entry.FullName.StartsWith(ZipSpritePrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            // Remover o prefixo "SpriteCollab-master/sprite/" para obter caminho relativo
            var relative = entry.FullName[ZipSpritePrefix.Length..];
            if (string.IsNullOrEmpty(relative))
                continue;

            // Guardar contra path traversal
            var fullDest = Path.GetFullPath(Path.Combine(destDir, relative));
            if (!fullDest.StartsWith(destRoot, StringComparison.OrdinalIgnoreCase))
                continue;

            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                Directory.CreateDirectory(fullDest);
            }
            else
            {
                var dir = Path.GetDirectoryName(fullDest);
                if (dir != null) Directory.CreateDirectory(dir);
                entry.ExtractToFile(fullDest, overwrite: true);
                extracted++;
            }

            if (done % 1000 == 0)
            {
                var pct = done * 100.0 / total;
                Log.Debug("Extracting: {Done}/{Total} ZIP entries ({Extracted} sprite files)", done, total, extracted);
            }
        }

        Log.Information("Extraction complete: {Extracted} sprite files from {Total} ZIP entries", extracted, total);
    }

    // ── Helpers UI ────────────────────────────────────────────────────────────

    private void SetStatus(string text, bool indeterminate, double value = 0)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text             = text;
            ProgressBar.IsIndeterminate = indeterminate;
            if (!indeterminate)
                ProgressBar.Value = Math.Clamp(value, 0, 100);
        });
    }

    private static void CleanupZip(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CancelButton.IsEnabled = false;
        CancelButton.Content   = "Cancelando...";
        _cts.Cancel();
    }
}
