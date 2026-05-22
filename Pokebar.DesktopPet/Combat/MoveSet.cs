using System;
using Pokebar.Core.Models;

namespace Pokebar.DesktopPet.Combat;

/// <summary>
/// Gerencia o conjunto de moves disponíveis durante um combate,
/// rastreando cooldowns e selecionando o próximo move.
/// </summary>
public sealed class MoveSet
{
    private readonly MoveDefinition[] _moves;
    private readonly int[] _cooldowns;
    private readonly Random _random;

    public MoveSet(MoveDefinition[] moves, Random? random = null)
    {
        _moves = moves.Length > 0 ? moves : MoveDefinition.DefaultMoves();
        _cooldowns = new int[_moves.Length];
        _random = random ?? new Random();
    }

    /// <summary>
    /// Seleciona o melhor move disponível (não em cooldown).
    /// Prioriza moves com maior dano, mas adiciona variação aleatória.
    /// Fallback: sempre retorna pelo menos o move com CooldownRounds=0 (ex: Tackle).
    /// </summary>
    public MoveDefinition PickMove()
    {
        // Coletar moves disponíveis (cooldown == 0)
        MoveDefinition? best = null;
        var bestScore = -1.0;

        for (int i = 0; i < _moves.Length; i++)
        {
            if (_cooldowns[i] > 0)
                continue;

            // Score = dano base + bônus aleatório (para variação)
            var score = _moves[i].BaseDamage + (_random.NextDouble() * 10);

            // Moves com efeito de status ganham bônus na seleção
            if (_moves[i].StatusEffect != StatusEffectType.None)
                score += 5;

            if (score > bestScore)
            {
                bestScore = score;
                best = _moves[i];
            }
        }

        // Fallback: primeiro move sem cooldown restante (deve ser Tackle com CooldownRounds=0)
        if (best == null)
        {
            for (int i = 0; i < _moves.Length; i++)
            {
                if (_moves[i].CooldownRounds == 0)
                    return _moves[i];
            }
            return _moves[0]; // último recurso
        }

        return best;
    }

    /// <summary>
    /// Registra uso de um move, aplicando seu cooldown.
    /// </summary>
    public void UseMove(MoveDefinition move)
    {
        for (int i = 0; i < _moves.Length; i++)
        {
            if (ReferenceEquals(_moves[i], move) || _moves[i].Name == move.Name)
            {
                _cooldowns[i] = move.CooldownRounds;
                break;
            }
        }
    }

    /// <summary>
    /// Avança um turno — reduz todos os cooldowns em 1.
    /// </summary>
    public void TickCooldowns()
    {
        for (int i = 0; i < _cooldowns.Length; i++)
        {
            if (_cooldowns[i] > 0)
                _cooldowns[i]--;
        }
    }
}
