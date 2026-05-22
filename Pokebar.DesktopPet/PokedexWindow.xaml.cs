using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfColor = System.Windows.Media.Color;
using System.Windows.Media.Imaging;
using Pokebar.Core.Models;
using Pokebar.DesktopPet.Animation;
using Pokebar.DesktopPet.Services;
using Serilog;

namespace Pokebar.DesktopPet;

public partial class PokedexWindow : Window
{
    private const int TOTAL_POKEMON = 1025;

    private readonly PokedexService _pokedex;
    private readonly SpriteCache _spriteCache;
    private readonly GameplayConfig _config;
    private List<DexEntry> _allEntries = new();

    public PokedexWindow(PokedexService pokedex, SpriteCache spriteCache, GameplayConfig config)
    {
        _pokedex = pokedex;
        _spriteCache = spriteCache;
        _config = config;

        InitializeComponent();
        BuildEntries();
        RefreshStats();
        ApplyFilter("all");
    }

    // ── Construção dos dados ──────────────────────────────────────────────

    private void BuildEntries()
    {
        _allEntries = new List<DexEntry>(TOTAL_POKEMON);

        for (int dex = 1; dex <= TOTAL_POKEMON; dex++)
        {
            bool seen     = _pokedex.Seen.Contains(dex);
            bool captured = _pokedex.Captured.Contains(dex);

            BitmapSource? sprite = null;
            if (seen)
            {
                try
                {
                    var anims = _spriteCache.GetAnimations(dex, "0000", _config);
                    // Preferir Idle; fallback para Walk ou Fight
                    var clip = anims.Idle ?? anims.WalkFallback ?? anims.WalkRight ?? anims.Fight;
                    sprite = clip?.Frames?.FirstOrDefault();
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Pokédex: could not load sprite for #{Dex}", dex);
                }
            }

            _allEntries.Add(new DexEntry(dex, seen, captured, sprite));
        }
    }

    private void RefreshStats()
    {
        var seen     = _pokedex.Seen.Count;
        var captured = _pokedex.Captured.Count;

        SeenCount.Text     = seen.ToString();
        CapturedCount.Text = captured.ToString();
        ProgressBar.Value  = captured;
        ProgressText.Text  = $"{captured} / {TOTAL_POKEMON}";

        FooterText.Text = captured switch
        {
            0        => "Capture seu primeiro Pokémon para começar!",
            < 10     => "Boa sorte na sua jornada, treinador!",
            < 50     => "Você está pegando o jeito!",
            < 151    => "Pokédex da Geração 1 quase completa!",
            < 251    => "Geração 2 na mira!",
            < 386    => "Hoenn aguarda!",
            < 493    => "Sinnoh te chama!",
            >= 1000  => "Incrível! Quase mestre Pokémon!",
            _        => $"{captured} capturados — continue explorando!"
        };
    }

    private void ApplyFilter(string filter)
    {
        IEnumerable<DexEntry> entries = filter switch
        {
            "seen"     => _allEntries.Where(e => e.Seen),
            "captured" => _allEntries.Where(e => e.Captured),
            _          => _allEntries
        };

        DexList.ItemsSource = entries.ToList();
    }

    // ── Event handlers ────────────────────────────────────────────────────

    private void OnFilterChanged(object sender, RoutedEventArgs e)
    {
        if (FilterAll == null) return; // ainda inicializando

        var filter = FilterAll.IsChecked == true ? "all"
                   : FilterSeen.IsChecked == true ? "seen"
                   : "captured";

        ApplyFilter(filter);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnWindowDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            DragMove();
    }
}

// ── ViewModel de cada entrada da Pokédex ─────────────────────────────────

public sealed class DexEntry
{
    public int Dex       { get; }
    public bool Seen     { get; }
    public bool Captured { get; }

    public BitmapSource? Sprite      { get; }
    public double        SpriteOpacity => Seen ? 1.0 : 0.18;

    public string DexLabel  => $"#{Dex:000}";
    public string StatusIcon => Captured ? "⬛" : Seen ? "👁" : "❓";
    public string StatusLabel => Captured ? "Capturado" : Seen ? "Visto" : "Não descoberto";
    public string Tooltip     => Captured ? $"#{Dex:000} — Capturado"
                               : Seen     ? $"#{Dex:000} — Visto"
                               :            $"#{Dex:000} — ???";

    // Cores do card
    public WpfBrush CardBackground => Captured
        ? new WpfSolidColorBrush(WpfColor.FromArgb(255, 200, 232, 180))   // verde claro
        : Seen
        ? new WpfSolidColorBrush(WpfColor.FromArgb(255, 232, 240, 200))   // verde pálido
        : new WpfSolidColorBrush(WpfColor.FromArgb(255, 180, 190, 200));  // cinza azulado

    public WpfBrush CardBorder => Captured
        ? new WpfSolidColorBrush(WpfColor.FromArgb(255, 120, 180, 80))    // borda verde
        : Seen
        ? new WpfSolidColorBrush(WpfColor.FromArgb(255, 150, 180, 130))   // borda verde clara
        : new WpfSolidColorBrush(WpfColor.FromArgb(255, 100, 120, 140));  // borda cinza

    public WpfBrush DexColor => Captured
        ? new WpfSolidColorBrush(WpfColor.FromArgb(255, 40, 120, 40))
        : Seen
        ? new WpfSolidColorBrush(WpfColor.FromArgb(255, 60, 90, 60))
        : new WpfSolidColorBrush(WpfColor.FromArgb(255, 80, 100, 110));

    public DexEntry(int dex, bool seen, bool captured, BitmapSource? sprite)
    {
        Dex      = dex;
        Seen     = seen;
        Captured = captured;
        Sprite   = sprite;
    }
}
