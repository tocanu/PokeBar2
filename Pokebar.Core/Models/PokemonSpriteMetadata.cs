namespace Pokebar.Core.Models;

/// <summary>
/// Dados brutos detectados pelo pipeline para um Pokémon específico.
/// Mantém o mínimo necessário para salvar em JSON "raw" e alimentar o editor.
/// </summary>
public record PokemonSpriteMetadata(
    int DexNumber,
    string Species,
    string? Form,
    SpriteSheetInfo Walk,
    SpriteSheetInfo Idle,
    SpriteSheetInfo Sleep,
    AnimationSummary Animations,
    SpriteOffsets Offsets,
    BodyTypeSuggestion BodyType,
    IReadOnlyList<string> Notes);

/// <summary>
/// Informações de uma spritesheet (arquivo + grid + tamanho do frame).
/// </summary>
public record SpriteSheetInfo(
    string? FileName,
    FrameSize? Frame,
    SpriteGrid? Grid);

/// <summary>
/// Quantidade de colunas e linhas de um sprite sheet detectado.
/// </summary>
public record SpriteGrid(int Columns, int Rows);

/// <summary>
/// Dimensões de um frame individual.
/// </summary>
public record FrameSize(int Width, int Height);

/// <summary>
/// Resumo das animações disponíveis e arquivos extras (emotes).
/// </summary>
public record AnimationSummary(bool HasWalk, bool HasIdle, bool HasSleep, IReadOnlyList<string> Emotes);

/// <summary>
/// Offsets calculados automaticamente; podem ser ajustados no editor.
/// </summary>
public record SpriteOffsets(int GroundOffsetY, int CenterOffsetX);

public enum BodyTypeSuggestion
{
    Unknown = 0,
    Small,
    Medium,
    Tall,
    Long,
    Flying
}
