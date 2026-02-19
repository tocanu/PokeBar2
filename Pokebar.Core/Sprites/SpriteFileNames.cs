namespace Pokebar.Core.Sprites;

/// <summary>
/// Constantes centralizadas para nomes de arquivos de sprites do SpriteCollab.
/// Usado por pipeline, editor e runtime para garantir consistência.
/// </summary>
public static class SpriteFileNames
{
    // Animações principais
    public const string Walk = "Walk-Anim.png";
    public const string Idle = "Idle-Anim.png";
    public const string Sleep = "Sleep.png";

    // Animações de ataque (ordem de prioridade)
    public static readonly string[] AttackAnimations =
    {
        "Attack-Anim.png",
        "Strike-Anim.png",
        "QuickStrike-Anim.png",
        "MultiStrike-Anim.png",
        "MultiScratch-Anim.png",
        "Scratch-Anim.png"
    };

    // Emotes e animações especiais
    public static readonly string[] EmoteAnimations =
    {
        "Hurt.png",
        "Charge.png",
        "Shoot.png",
        "Roar.png",
        "Swing.png",
        "Double.png",
        "Bite.png",
        "Pound.png",
        "Hop.png",
        "Appeal.png",
        "Dance.png",
        "EventSleep.png"
    };

    // Variantes de Sleep
    public static readonly string[] SleepVariants =
    {
        "Sleep.png",
        "Sleep-Anim.png",
        "EventSleep.png"
    };

    /// <summary>
    /// Retorna todos os nomes de arquivos de animações conhecidos.
    /// </summary>
    public static IEnumerable<string> AllAnimationFiles
    {
        get
        {
            yield return Walk;
            yield return Idle;
            yield return Sleep;
            
            foreach (var attack in AttackAnimations)
            {
                yield return attack;
            }
            
            foreach (var emote in EmoteAnimations)
            {
                yield return emote;
            }
        }
    }

    /// <summary>
    /// Verifica se um nome de arquivo é uma animação principal (Walk/Idle/Sleep).
    /// </summary>
    public static bool IsMainAnimation(string fileName)
    {
        return string.Equals(fileName, Walk, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, Idle, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, Sleep, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifica se um nome de arquivo é uma animação de ataque.
    /// </summary>
    public static bool IsAttackAnimation(string fileName)
    {
        return AttackAnimations.Any(a => string.Equals(fileName, a, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifica se um nome de arquivo é um emote.
    /// </summary>
    public static bool IsEmoteAnimation(string fileName)
    {
        return EmoteAnimations.Any(e => string.Equals(fileName, e, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Procura variantes de Sleep (Sleep.png, Sleep-Anim.png, EventSleep.png).
    /// </summary>
    public static string? FindSleepVariant(IEnumerable<string> availableFiles)
    {
        foreach (var variant in SleepVariants)
        {
            var match = availableFiles.FirstOrDefault(f => 
                string.Equals(f, variant, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return match;
            }
        }
        return null;
    }

    /// <summary>
    /// Procura a melhor animação de ataque disponível (ordem de prioridade).
    /// </summary>
    public static string? FindBestAttackAnimation(IEnumerable<string> availableFiles)
    {
        foreach (var attack in AttackAnimations)
        {
            var match = availableFiles.FirstOrDefault(f => 
                string.Equals(f, attack, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return match;
            }
        }
        return null;
    }
}
