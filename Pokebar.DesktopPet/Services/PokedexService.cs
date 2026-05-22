using System;
using System.Collections.Generic;
using Pokebar.Core.Models;
using Serilog;

namespace Pokebar.DesktopPet.Services;

/// <summary>
/// Gerencia a Pokédex do jogador: registra Pokémon vistos (spawn) e capturados.
/// </summary>
public class PokedexService
{
    /// <summary>Dex numbers de Pokémon vistos no mapa.</summary>
    public HashSet<int> Seen { get; private set; } = new();

    /// <summary>Dex numbers de Pokémon capturados.</summary>
    public HashSet<int> Captured { get; private set; } = new();

    /// <summary>Disparado quando um novo Pokémon é visto pela primeira vez.</summary>
    public event Action<int>? NewSeen;

    /// <summary>Disparado quando um novo Pokémon é capturado pela primeira vez.</summary>
    public event Action<int>? NewCaptured;

    /// <summary>Restaura estado da Pokédex do SaveData.</summary>
    public void RestoreFromSave(SaveData save)
    {
        Seen = new HashSet<int>(save.PokedexSeen);
        Captured = new HashSet<int>(save.PokedexCaptured);

        // Party members are implicitly captured
        foreach (var dex in save.Party)
        {
            Seen.Add(dex);
            Captured.Add(dex);
        }
    }

    /// <summary>Registra um Pokémon como visto (apareceu no mapa).</summary>
    public void OnSeen(int dex)
    {
        if (dex <= 0) return;
        if (Seen.Add(dex))
        {
            Log.Debug("Pokédex: #{Dex} visto pela primeira vez! Total vistos: {Total}", dex, Seen.Count);
            NewSeen?.Invoke(dex);
        }
    }

    /// <summary>Registra um Pokémon como capturado.</summary>
    public void OnCaptured(int dex)
    {
        if (dex <= 0) return;
        Seen.Add(dex); // garante que capturado também está em vistos
        if (Captured.Add(dex))
        {
            Log.Debug("Pokédex: #{Dex} capturado pela primeira vez! Total capturados: {Total}", dex, Captured.Count);
            NewCaptured?.Invoke(dex);
        }
    }

    /// <summary>Aplica dados da Pokédex ao SaveData.</summary>
    public SaveData ApplyToSave(SaveData save)
    {
        return save with
        {
            PokedexSeen = new HashSet<int>(Seen),
            PokedexCaptured = new HashSet<int>(Captured)
        };
    }
}
