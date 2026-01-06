namespace Pokebar.Core.Models;

/// <summary>
/// Informação estendida de um Pokémon incluindo forma/variante.
/// </summary>
public record PokemonVariant(
    int DexNumber,
    string FormId)
{
    /// <summary>
    /// Identificador único: "0025" ou "0025_0006"
    /// </summary>
    public string UniqueId => FormId == "0000" 
        ? $"{DexNumber:D4}" 
        : $"{DexNumber:D4}_{FormId}";

    /// <summary>
    /// Nome legível: "Pikachu" ou "Pikachu (Partner)"
    /// </summary>
    public string DisplayName => GetDisplayName(DexNumber, FormId);

    private static string GetDisplayName(int dex, string formId)
    {
        // Mapeamento básico de formas conhecidas
        var formName = formId switch
        {
            "0000" => null,
            "0001" => "Mega",
            "0002" => "Mega X",
            "0003" => "Mega Y",
            "0004" => "Alolan",
            "0005" => "Galarian",
            "0006" => "Partner",
            "0007" => "Female",
            "0008" => "Gigantamax",
            _ => $"Form {formId}"
        };

        var species = GetSpeciesName(dex);
        return formName == null ? species : $"{species} ({formName})";
    }

    private static string GetSpeciesName(int dex)
    {
        // TODO: carregar de arquivo de dados quando implementar i18n
        return dex switch
        {
            1 => "Bulbasaur",
            25 => "Pikachu",
            133 => "Eevee",
            _ => $"#{dex:D3}"
        };
    }
}

/// <summary>
/// Helper para trabalhar com estrutura de pastas do SpriteCollab.
/// </summary>
public static class SpriteDirectoryHelper
{
    /// <summary>
    /// Procura todos os diretórios de sprites (incluindo formas).
    /// Retorna tuplas (dexNumber, formId, fullPath).
    /// </summary>
    public static IEnumerable<(int DexNumber, string FormId, string Path)> EnumerateSpriteFolders(string spriteRoot)
    {
        if (!Directory.Exists(spriteRoot))
            yield break;

        var dexDirs = Directory.GetDirectories(spriteRoot)
            .Select(Path.GetFileName)
            .Where(name => name != null && int.TryParse(name, out var dex) && DexConstants.IsValid(dex))
            .Cast<string>();

        foreach (var dexStr in dexDirs)
        {
            var dex = int.Parse(dexStr);
            var dexPath = Path.Combine(spriteRoot, dexStr);

            // Verifica se tem subpastas (formas)
            var formDirs = Directory.GetDirectories(dexPath);

            if (formDirs.Length > 0)
            {
                // Tem formas: varre cada subpasta
                foreach (var formPath in formDirs)
                {
                    var formId = Path.GetFileName(formPath);
                    if (formId != null && formId.All(char.IsDigit))
                    {
                        yield return (dex, formId, formPath);
                    }
                }
            }
            else
            {
                // Sem formas: usa a pasta principal como forma "0000"
                yield return (dex, "0000", dexPath);
            }
        }
    }

    /// <summary>
    /// Resolve o caminho para os sprites de um Pokémon específico.
    /// </summary>
    public static string? ResolvePokemonPath(string spriteRoot, int dex, string? formId = null)
    {
        var dexStr = $"{dex:D4}";
        var dexPath = Path.Combine(spriteRoot, dexStr);

        if (!Directory.Exists(dexPath))
            return null;

        // Se não especificou forma, tenta forma padrão primeiro
        if (string.IsNullOrEmpty(formId))
        {
            var defaultForm = Path.Combine(dexPath, "0000");
            if (Directory.Exists(defaultForm))
                return defaultForm;

            // Se não tem subpastas, a pasta principal é a forma padrão
            var subdirs = Directory.GetDirectories(dexPath);
            if (subdirs.Length == 0)
                return dexPath;

            // Se tem subpastas, pega a primeira
            return subdirs.FirstOrDefault();
        }

        // Forma específica
        var formPath = Path.Combine(dexPath, formId);
        return Directory.Exists(formPath) ? formPath : null;
    }

    /// <summary>
    /// Lista todas as formas disponíveis para um Pokémon.
    /// </summary>
    public static IEnumerable<string> GetAvailableForms(string spriteRoot, int dex)
    {
        var dexStr = $"{dex:D4}";
        var dexPath = Path.Combine(spriteRoot, dexStr);

        if (!Directory.Exists(dexPath))
            return Enumerable.Empty<string>();

        var formDirs = Directory.GetDirectories(dexPath);

        if (formDirs.Length == 0)
        {
            // Sem subpastas = forma única "0000"
            return new[] { "0000" };
        }

        return formDirs
            .Select(Path.GetFileName)
            .Where(name => name != null && name.All(char.IsDigit))
            .Cast<string>()
            .OrderBy(f => f);
    }
}
