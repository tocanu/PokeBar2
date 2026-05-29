namespace Pokebar.Core.Models;

/// <summary>
/// Mapeamento de Dex number → PokemonType primário.
/// Cobre a Geração 1 (Dex 1–151) com fallback Normal para dex desconhecidos.
/// </summary>
public static class PokemonTypeData
{
    // Indexado por Dex - 1 (0-based). Cobre dex 1–151.
    private static readonly PokemonType[] _gen1Types =
    {
        // 001 Bulbasaur      002 Ivysaur         003 Venusaur
        PokemonType.Grass,   PokemonType.Grass,   PokemonType.Grass,
        // 004 Charmander     005 Charmeleon       006 Charizard
        PokemonType.Fire,    PokemonType.Fire,    PokemonType.Fire,
        // 007 Squirtle       008 Wartortle         009 Blastoise
        PokemonType.Water,   PokemonType.Water,   PokemonType.Water,
        // 010 Caterpie       011 Metapod           012 Butterfree
        PokemonType.Bug,     PokemonType.Bug,     PokemonType.Bug,
        // 013 Weedle         014 Kakuna            015 Beedrill
        PokemonType.Bug,     PokemonType.Bug,     PokemonType.Bug,
        // 016 Pidgey         017 Pidgeotto          018 Pidgeot
        PokemonType.Normal,  PokemonType.Normal,  PokemonType.Normal,
        // 019 Rattata        020 Raticate
        PokemonType.Normal,  PokemonType.Normal,
        // 021 Spearow        022 Fearow
        PokemonType.Normal,  PokemonType.Normal,
        // 023 Ekans          024 Arbok
        PokemonType.Poison,  PokemonType.Poison,
        // 025 Pikachu        026 Raichu
        PokemonType.Electric, PokemonType.Electric,
        // 027 Sandshrew      028 Sandslash
        PokemonType.Ground,  PokemonType.Ground,
        // 029 Nidoran♀       030 Nidorina          031 Nidoqueen
        PokemonType.Poison,  PokemonType.Poison,  PokemonType.Poison,
        // 032 Nidoran♂       033 Nidorino          034 Nidoking
        PokemonType.Poison,  PokemonType.Poison,  PokemonType.Poison,
        // 035 Clefairy       036 Clefable
        PokemonType.Fairy,   PokemonType.Fairy,
        // 037 Vulpix         038 Ninetales
        PokemonType.Fire,    PokemonType.Fire,
        // 039 Jigglypuff     040 Wigglytuff
        PokemonType.Normal,  PokemonType.Normal,
        // 041 Zubat          042 Golbat
        PokemonType.Poison,  PokemonType.Poison,
        // 043 Oddish         044 Gloom            045 Vileplume
        PokemonType.Grass,   PokemonType.Grass,   PokemonType.Grass,
        // 046 Paras          047 Parasect
        PokemonType.Bug,     PokemonType.Bug,
        // 048 Venonat        049 Venomoth
        PokemonType.Bug,     PokemonType.Bug,
        // 050 Diglett        051 Dugtrio
        PokemonType.Ground,  PokemonType.Ground,
        // 052 Meowth         053 Persian
        PokemonType.Normal,  PokemonType.Normal,
        // 054 Psyduck        055 Golduck
        PokemonType.Water,   PokemonType.Water,
        // 056 Mankey         057 Primeape
        PokemonType.Fighting, PokemonType.Fighting,
        // 058 Growlithe      059 Arcanine
        PokemonType.Fire,    PokemonType.Fire,
        // 060 Poliwag        061 Poliwhirl        062 Poliwrath
        PokemonType.Water,   PokemonType.Water,   PokemonType.Water,
        // 063 Abra           064 Kadabra           065 Alakazam
        PokemonType.Psychic, PokemonType.Psychic, PokemonType.Psychic,
        // 066 Machop         067 Machoke           068 Machamp
        PokemonType.Fighting, PokemonType.Fighting, PokemonType.Fighting,
        // 069 Bellsprout     070 Weepinbell        071 Victreebel
        PokemonType.Grass,   PokemonType.Grass,   PokemonType.Grass,
        // 072 Tentacool      073 Tentacruel
        PokemonType.Water,   PokemonType.Water,
        // 074 Geodude        075 Graveler          076 Golem
        PokemonType.Rock,    PokemonType.Rock,    PokemonType.Rock,
        // 077 Ponyta         078 Rapidash
        PokemonType.Fire,    PokemonType.Fire,
        // 079 Slowpoke       080 Slowbro
        PokemonType.Water,   PokemonType.Water,
        // 081 Magnemite      082 Magneton
        PokemonType.Electric, PokemonType.Electric,
        // 083 Farfetch'd     084 Doduo             085 Dodrio
        PokemonType.Normal,  PokemonType.Normal,  PokemonType.Normal,
        // 086 Seel           087 Dewgong
        PokemonType.Water,   PokemonType.Water,
        // 088 Grimer         089 Muk
        PokemonType.Poison,  PokemonType.Poison,
        // 090 Shellder       091 Cloyster
        PokemonType.Water,   PokemonType.Water,
        // 092 Gastly         093 Haunter           094 Gengar
        PokemonType.Ghost,   PokemonType.Ghost,   PokemonType.Ghost,
        // 095 Onix
        PokemonType.Rock,
        // 096 Drowzee        097 Hypno
        PokemonType.Psychic, PokemonType.Psychic,
        // 098 Krabby         099 Kingler
        PokemonType.Water,   PokemonType.Water,
        // 100 Voltorb        101 Electrode
        PokemonType.Electric, PokemonType.Electric,
        // 102 Exeggcute      103 Exeggutor
        PokemonType.Grass,   PokemonType.Grass,
        // 104 Cubone         105 Marowak
        PokemonType.Ground,  PokemonType.Ground,
        // 106 Hitmonlee      107 Hitmonchan
        PokemonType.Fighting, PokemonType.Fighting,
        // 108 Lickitung
        PokemonType.Normal,
        // 109 Koffing        110 Weezing
        PokemonType.Poison,  PokemonType.Poison,
        // 111 Rhyhorn        112 Rhydon
        PokemonType.Ground,  PokemonType.Ground,
        // 113 Chansey
        PokemonType.Normal,
        // 114 Tangela
        PokemonType.Grass,
        // 115 Kangaskhan
        PokemonType.Normal,
        // 116 Horsea         117 Seadra
        PokemonType.Water,   PokemonType.Water,
        // 118 Goldeen        119 Seaking
        PokemonType.Water,   PokemonType.Water,
        // 120 Staryu         121 Starmie
        PokemonType.Water,   PokemonType.Water,
        // 122 Mr. Mime
        PokemonType.Psychic,
        // 123 Scyther
        PokemonType.Bug,
        // 124 Jynx
        PokemonType.Ice,
        // 125 Electabuzz
        PokemonType.Electric,
        // 126 Magmar
        PokemonType.Fire,
        // 127 Pinsir
        PokemonType.Bug,
        // 128 Tauros
        PokemonType.Normal,
        // 129 Magikarp       130 Gyarados
        PokemonType.Water,   PokemonType.Water,
        // 131 Lapras
        PokemonType.Water,
        // 132 Ditto
        PokemonType.Normal,
        // 133 Eevee
        PokemonType.Normal,
        // 134 Vaporeon       135 Jolteon          136 Flareon
        PokemonType.Water,   PokemonType.Electric, PokemonType.Fire,
        // 137 Porygon
        PokemonType.Normal,
        // 138 Omanyte        139 Omastar
        PokemonType.Rock,    PokemonType.Rock,
        // 140 Kabuto         141 Kabutops
        PokemonType.Rock,    PokemonType.Rock,
        // 142 Aerodactyl
        PokemonType.Rock,
        // 143 Snorlax
        PokemonType.Normal,
        // 144 Articuno
        PokemonType.Ice,
        // 145 Zapdos
        PokemonType.Electric,
        // 146 Moltres
        PokemonType.Fire,
        // 147 Dratini        148 Dragonair         149 Dragonite
        PokemonType.Dragon,  PokemonType.Dragon,  PokemonType.Dragon,
        // 150 Mewtwo
        PokemonType.Psychic,
        // 151 Mew
        PokemonType.Psychic,
    };

    /// <summary>
    /// Retorna o tipo primário de um Pokémon pelo Dex number.
    /// Cobre Gen 1 (1–151). Dex fora desse intervalo retorna Normal.
    /// </summary>
    public static PokemonType GetPrimaryType(int dex)
    {
        var index = dex - 1;
        if ((uint)index < (uint)_gen1Types.Length)
            return _gen1Types[index];
        return PokemonType.Normal;
    }
}
