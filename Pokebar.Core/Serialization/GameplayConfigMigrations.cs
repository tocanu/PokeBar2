using System.Diagnostics;
using Pokebar.Core.Models;

namespace Pokebar.Core.Serialization;

/// <summary>
/// Migrações automáticas de GameplayConfig.
/// Quando um default é alterado ou um campo novo é adicionado, incremente
/// <see cref="CurrentVersion"/> e adicione um caso em <see cref="Migrate"/>.
/// A migração é aplicada uma única vez por perfil e o arquivo é salvo em seguida.
/// </summary>
public static class GameplayConfigMigrations
{
    /// <summary>
    /// Versão mais recente do schema. Incrementar a cada conjunto de migrações.
    /// </summary>
    public const int CurrentVersion = 2;

    /// <summary>
    /// Aplica todas as migrações pendentes à config carregada do disco.
    /// Retorna a config (possivelmente modificada) junto com um bool indicando
    /// se houve mudança (para que o chamador salve o arquivo atualizado).
    /// </summary>
    public static (GameplayConfig config, bool changed) Migrate(GameplayConfig config)
    {
        if (config.ConfigVersion >= CurrentVersion)
            return (config, false);

        var current = config;
        var fromVersion = current.ConfigVersion;

        // ── v0 → v1 ────────────────────────────────────────────────────────────
        // Defaults alterados: SFX desligado, balões de fala desligados,
        // notificações toast desligadas por padrão na primeira execução.
        if (current.ConfigVersion < 1)
        {
            current = current with
            {
                Sfx     = current.Sfx     with { Enabled = false },
                Mood    = current.Mood    with { SpeechBubblesEnabled = false },
                Windows = current.Windows with { ToastNotificationsEnabled = false },
                ConfigVersion = 1,
            };
            Trace.TraceInformation(
                "GameplayConfigMigrations: Applied v0→v1 (SFX off, SpeechBubbles off, Toast off)");
        }

        // ── v1 → v2 ────────────────────────────────────────────────────────────
        // Phase 8: LevelConfig, DesktopIconConfig e campos extras de SfxConfig foram
        // adicionados. Os defaults dos records já são corretos (features opt-in, off por
        // padrão), então esta migração é apenas um bump de versão que confirma que o
        // config do usuário foi revisado contra o schema v2.
        if (current.ConfigVersion < 2)
        {
            current = current with { ConfigVersion = 2 };
            Trace.TraceInformation(
                "GameplayConfigMigrations: Applied v1→v2 (Phase 8 schema — LevelConfig, DesktopIconConfig, SfxConfig)");
        }

        // ── Adicionar migrações futuras aqui ───────────────────────────────────
        // if (current.ConfigVersion < 3) { ... current = current with { ConfigVersion = 3 }; }

        Trace.TraceInformation(
            "GameplayConfigMigrations: Migrated config from v{0} to v{1}", fromVersion, CurrentVersion);

        return (current, true);
    }
}
