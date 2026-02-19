using System;

namespace Pokebar.Core.Models;

/// <summary>
/// Manifesto de um pack/mod de sprites.
/// </summary>
public record ModManifest
{
    /// <summary>Identificador único do mod.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Nome de exibição do mod.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Autor do mod.</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>Versão do mod (semver).</summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>Descrição do mod.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Caminho relativo da pasta de sprites (relativo ao manifest).</summary>
    public string SpritePath { get; init; } = "sprites";

    /// <summary>Caminho relativo do arquivo de offsets (relativo ao manifest).</summary>
    public string? OffsetsFile { get; init; }

    /// <summary>Lista de dex numbers que este mod fornece/sobrescreve.</summary>
    public int[] DexOverrides { get; init; } = Array.Empty<int>();

    /// <summary>Se o mod é válido e pode ser carregado.</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Id) && !string.IsNullOrWhiteSpace(Name);
}
