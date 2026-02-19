using System;
using System.Collections.Generic;

namespace Pokebar.Core.Models;

/// <summary>
/// Configurações globais do aplicativo (idioma, perfil ativo, etc.).
/// Separado de GameplayConfig (que é por perfil).
/// Salvo em %AppData%/Pokebar/settings.json
/// </summary>
public record AppSettings
{
    /// <summary>Código da cultura ativa (ex: "pt-BR", "en-US"). Null = detectar do sistema.</summary>
    public string? Language { get; init; }

    /// <summary>ID do perfil ativo.</summary>
    public string ActiveProfileId { get; init; } = "default";

    /// <summary>Lista de perfis configurados.</summary>
    public List<ProfileEntry> Profiles { get; init; } = new()
    {
        new ProfileEntry { Id = "default", Name = "profile.default", Icon = "🎮" }
    };
}

/// <summary>
/// Metadados de um perfil. O config real fica em gameplay_{id}.json.
/// </summary>
public record ProfileEntry
{
    /// <summary>Identificador único (slug). Ex: "default", "work", "stream".</summary>
    public string Id { get; init; } = "default";

    /// <summary>Nome para exibição. Se começa com "profile.", é chave de localização.</summary>
    public string Name { get; init; } = "profile.default";

    /// <summary>Ícone emoji para UIs futuras.</summary>
    public string Icon { get; init; } = "🎮";
}
