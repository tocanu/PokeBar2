using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Serilog;
using WpfApp = System.Windows.Application;
using WpfMsgBox = System.Windows.MessageBox;

namespace Pokebar.DesktopPet;

/// <summary>
/// Janela de configuração de primeira execução: baixa e extrai os sprites Gen 1
/// do GitHub automaticamente. Exibida quando SpriteCollab\sprite\ não é encontrado.
///
/// URL dos sprites: GitHub release "sprites-v1" do repositório PokeBar2.
/// Após extração bem-sucedida, reinicia o app para carregar os sprites normalmente.
/// </summary>
public partial class SpriteSetupWindow : Window
{
    // URL fixa do ZIP de sprites — atualizar se uma nova versão dos sprites for publicada
    private const string SpritesZipUrl =
        "https://github.com/tocanu/PokeBar2/releases/download/sprites-v1/PokeBar-Sprites-Gen1.zip";

    private const string ZipFileName = "PokeBar-Sprites-Gen1.zip";

    private readonly CancellationTokenSource _cts = new();

    public SpriteSetupWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => _ = RunSetupAsync();
    }

    // ── Download + extração ───────────────────────────────────────────────────

    private async Task RunSetupAsync()
    {
        var ct        = _cts.Token;
        var installDir = AppContext.BaseDirectory;
        var spriteDest = Path.Combine(installDir, "SpriteCollab", "sprite");
        var zipPath   = Path.Combine(Path.GetTempPath(), ZipFileName);

        try
        {
            // ── Fase 1: Download ──────────────────────────────────────────────
            SetStatus("Conectando ao GitHub...", indeterminate: true);
            Log.Information("Downloading sprites from {Url}", SpritesZipUrl);

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            http.DefaultRequestHeaders.Add("User-Agent", "PokeBar-SpriteSetup/1.0");

            using var response = await http.GetAsync(
                SpritesZipUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var total      = response.Content.Headers.ContentLength ?? 154_000_000L;
            var totalMb    = total / 1_048_576.0;
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

                // Atualizar UI a cada ~250 ms para não sobrecarregar o dispatcher
                if (sw.ElapsedMilliseconds >= 250)
                {
                    sw.Restart();
                    var dlMb = downloaded / 1_048_576.0;
                    var pct  = downloaded * 100.0 / total;
                    SetStatus($"Baixando sprites... {dlMb:F0} / {totalMb:F0} MB", indeterminate: false, pct);
                }
            }

            Log.Information("Sprites ZIP downloaded ({Bytes} bytes)", downloaded);

            // ── Fase 2: Extração ──────────────────────────────────────────────
            SetStatus("Extraindo sprites...", indeterminate: true);

            await Task.Run(() => ExtractSprites(zipPath, spriteDest, ct), ct);

            // Limpar ZIP temporário
            try { File.Delete(zipPath); } catch { /* não crítico */ }

            Log.Information("Sprites extracted to {Dest}", spriteDest);

            // ── Fase 3: Reiniciar app ─────────────────────────────────────────
            SetStatus("Pronto! Reiniciando o PokeBar...", indeterminate: true);
            await Task.Delay(800, ct); // breve pausa para o usuário ver a mensagem

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
                await RunSetupAsync();   // nova tentativa
            else
                WpfApp.Current.Shutdown();
        }
    }

    /// <summary>Extrai o ZIP de sprites para destDir, com path-traversal guard.</summary>
    private static void ExtractSprites(string zipPath, string destDir, CancellationToken ct)
    {
        Directory.CreateDirectory(destDir);
        var destRoot = Path.GetFullPath(destDir) + Path.DirectorySeparatorChar;

        using var zip     = ZipFile.OpenRead(zipPath);
        var entries       = zip.Entries;
        int total         = entries.Count;
        int done          = 0;

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();

            // Garante que não há path traversal (../)
            var fullDest = Path.GetFullPath(Path.Combine(destDir, entry.FullName));
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
            }

            done++;
        }

        Log.Debug("Extracted {Done}/{Total} sprite entries", done, total);
    }

    // ── Helpers UI ────────────────────────────────────────────────────────────

    private void SetStatus(string text, bool indeterminate, double value = 0)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text              = text;
            ProgressBar.IsIndeterminate  = indeterminate;
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
