using Pokebar.Core.Models;
using Pokebar.DesktopPet.Animation;
using Serilog;

namespace Pokebar.DesktopPet.Services;

/// <summary>
/// Gera o spawn pool dinamicamente a partir de todos os Pokémon com sprites disponíveis.
/// Raridade é baseada em tiers: comum, incomum, raro, muito raro, lendário.
/// </summary>
public static class SpawnPoolBuilder
{
    /// <summary>
    /// Constrói spawn weights para todos os Pokémon base (formId "0000") que existem nos offsets.
    /// Exclui dex 0 (MissingNo) e o dex do player.
    /// </summary>
    public static EnemySpawnWeight[] BuildFromOffsets(SpriteLoader loader, int playerDex)
    {
        var weights = new List<EnemySpawnWeight>();

        // Iterar de 1 a 1025 (todas as gerações conhecidas)
        for (int dex = 1; dex <= 1025; dex++)
        {
            if (dex == playerDex)
                continue;

            var uniqueId = dex.ToString("D4");
            if (!loader.TryGetOffset(uniqueId, out _))
                continue;

            var (weight, tier) = GetWeightForDex(dex);
            weights.Add(new EnemySpawnWeight(dex, weight, tier));
        }

        Log.Information("SpawnPoolBuilder: {Count} Pokémon no spawn pool (excluindo player #{Player})",
            weights.Count, playerDex);

        return weights.ToArray();
    }

    /// <summary>
    /// Retorna (peso, comentário/tier) baseado no dex number.
    /// Pokémon mais comuns (rotas iniciais) têm peso maior,
    /// lendários/míticos têm peso bem menor.
    /// </summary>
    private static (int Weight, string Tier) GetWeightForDex(int dex)
    {
        // Lendários e Míticos — raridade muito baixa
        if (IsLegendary(dex) || IsMythical(dex))
            return (1, "Legendary/Mythical");

        // Pseudo-lendários (Dragonite line, Tyranitar line, etc.)
        if (IsPseudoLegendary(dex))
            return (3, "Pseudo-Legendary");

        // Starters (todas as gerações) — incomum
        if (IsStarter(dex))
            return (8, "Starter");

        // Estágio final evoluído (3º estágio) — raro
        if (IsFinalStage(dex))
            return (10, "Final Evolution");

        // Estágio intermediário — incomum
        if (IsMiddleStage(dex))
            return (20, "Mid Evolution");

        // Pokémon comuns de rota (1º estágio, base) — default
        if (IsCommonRoute(dex))
            return (50, "Common");

        // Default para Pokémon base não categorizados
        return (30, "Uncommon");
    }

    private static bool IsLegendary(int dex) => dex switch
    {
        // Gen 1
        144 or 145 or 146 or 150 => true,
        // Gen 2
        243 or 244 or 245 or 249 or 250 => true,
        // Gen 3
        377 or 378 or 379 or 380 or 381 or 382 or 383 or 384 => true,
        // Gen 4
        480 or 481 or 482 or 483 or 484 or 485 or 486 or 487 or 488 or 491 => true,
        // Gen 5
        638 or 639 or 640 or 641 or 642 or 643 or 644 or 645 or 646 => true,
        // Gen 6
        716 or 717 or 718 => true,
        // Gen 7
        772 or 773 or 785 or 786 or 787 or 788 or 789 or 790 or 791 or 792 or 800 => true,
        // Gen 8
        888 or 889 or 890 or 891 or 892 or 895 or 896 or 897 or 898 => true,
        // Gen 9
        1001 or 1002 or 1003 or 1004 or 1007 or 1008 or 1014 or 1015 or 1016 or 1017 or 1024 or 1025 => true,
        _ => false
    };

    private static bool IsMythical(int dex) => dex switch
    {
        151 or 251 or 385 or 386 or 489 or 490 or 492 or 493 or 494 or 647 or 648 or 649
        or 719 or 720 or 721 or 801 or 802 or 807 or 808 or 809 or 893 or 1025 => true,
        _ => false
    };

    private static bool IsPseudoLegendary(int dex) => dex switch
    {
        149 or 248 or 373 or 376 or 445 or 635 or 706 or 784 or 887 or 998 => true,
        _ => false
    };

    private static bool IsStarter(int dex) => dex switch
    {
        // Gen 1-9 starters (todas as formas base)
        1 or 4 or 7 or 152 or 155 or 158 or 252 or 255 or 258 or 387 or 390 or 393
        or 495 or 498 or 501 or 650 or 653 or 656 or 722 or 725 or 728
        or 810 or 813 or 816 or 906 or 909 or 912 => true,
        _ => false
    };

    /// <summary>
    /// Pokémon comuns encontrados em rotas iniciais (Gen 1-4 como referência).
    /// </summary>
    private static bool IsCommonRoute(int dex) => dex switch
    {
        // Gen 1 comuns
        10 or 11 or 13 or 14 or 16 or 17 or 19 or 20 or 21 or 22 or 23 or 27 or 29 or 32
        or 35 or 39 or 41 or 42 or 43 or 46 or 48 or 50 or 52 or 54 or 56 or 60 or 63
        or 66 or 69 or 72 or 74 or 77 or 79 or 81 or 84 or 86 or 88 or 90 or 92 or 95
        or 96 or 98 or 100 or 102 or 104 or 108 or 109 or 111 or 114 or 116 or 118
        or 120 or 129 or 132 or 133 or 147 => true,
        // Gen 2 comuns
        161 or 163 or 165 or 167 or 170 or 177 or 179 or 183 or 187 or 190 or 191
        or 193 or 194 or 198 or 200 or 201 or 202 or 203 or 204 or 206 or 207 or 209
        or 211 or 213 or 215 or 216 or 218 or 220 or 222 or 223 or 225 or 226 or 227
        or 228 or 231 or 234 or 235 or 238 or 239 or 240 or 241 or 246 => true,
        // Gen 3 comuns
        261 or 263 or 265 or 270 or 273 or 276 or 278 or 280 or 283 or 285 or 287
        or 290 or 293 or 296 or 298 or 299 or 300 or 302 or 303 or 304 or 307 or 309
        or 311 or 312 or 313 or 314 or 315 or 316 or 318 or 320 or 322 or 325 or 327
        or 328 or 331 or 333 or 335 or 336 or 337 or 338 or 339 or 341 or 343 or 345
        or 347 or 349 or 351 or 352 or 353 or 355 or 357 or 358 or 359 or 360 or 361
        or 363 or 366 or 369 or 370 or 371 => true,
        _ => false
    };

    /// <summary>
    /// Heurística: se o dex está num range de "middle evolutions" conhecidos.
    /// Usa padrão simples: muitos Pokémon de estágio intermediário estão entre evoluções.
    /// </summary>
    private static bool IsMiddleStage(int dex) => dex switch
    {
        // Gen 1 mid-evolutions
        2 or 3 or 5 or 6 or 8 or 9 or 11 or 12 or 14 or 15 or 17 or 18 or 20 or 22
        or 24 or 25 or 28 or 30 or 31 or 33 or 34 or 36 or 37 or 38 or 40 or 42 or 44
        or 45 or 47 or 49 or 51 or 53 or 55 or 57 or 61 or 62 or 64 or 65 or 67 or 68
        or 70 or 71 or 73 or 75 or 76 or 78 or 80 or 82 or 83 or 85 or 87 or 89 or 91
        or 93 or 94 or 97 or 99 or 101 or 103 or 105 or 110 or 112 or 113 or 117
        or 119 or 121 or 122 or 124 or 125 or 126 or 128 or 130 or 131 or 134 or 135
        or 136 or 137 or 139 or 141 or 142 or 143 or 148 => true,
        _ => false
    };

    /// <summary>
    /// Heurística para final stage: estágio final de 3-stage lines.
    /// </summary>
    private static bool IsFinalStage(int dex) => dex switch
    {
        // Gen 1 final evolutions
        3 or 6 or 9 or 12 or 15 or 18 or 31 or 34 or 36 or 38 or 45 or 49 or 51 or 53
        or 55 or 57 or 62 or 65 or 68 or 71 or 73 or 76 or 78 or 80 or 82 or 83 or 85
        or 87 or 89 or 91 or 94 or 97 or 99 or 101 or 103 or 105 or 106 or 107 or 110
        or 112 or 113 or 115 or 117 or 119 or 121 or 122 or 123 or 124 or 125 or 126
        or 127 or 128 or 130 or 131 or 134 or 135 or 136 or 137 or 139 or 141 or 142
        or 143 => true,
        // Gerações posteriores terão default weight
        _ => false
    };
}
