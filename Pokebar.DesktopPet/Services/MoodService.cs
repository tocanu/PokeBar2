using Pokebar.Core.Models;
using Pokebar.DesktopPet.Entities;
using Serilog;

namespace Pokebar.DesktopPet.Services;

/// <summary>
/// Gerencia o humor e amizade do Pokémon do jogador.
/// Atualiza o MoodType baseado no valor de Friendship e tempo idle.
/// </summary>
public class MoodService
{
    private readonly MoodConfig _config;
    private double _idleTimer;
    private double _petCooldownTimer;

    public MoodService(MoodConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Atualiza o humor baseado no friendship e tempo idle.
    /// Chamado a cada frame do game loop.
    /// </summary>
    public void Update(PlayerPet player, double deltaTime)
    {
        // Cooldown de carícia
        if (_petCooldownTimer > 0)
            _petCooldownTimer -= deltaTime;

        // Contar tempo ocioso
        if (player.State == EntityState.Idle || player.State == EntityState.SpecialIdle)
            _idleTimer += deltaTime;
        else
            _idleTimer = 0;

        // Calcular humor baseado na amizade
        var mood = CalculateMood(player.Friendship);

        // Se ficou muito tempo parado, fica sonolento
        if (_idleTimer >= _config.SleepyIdleSeconds && mood != MoodType.Sad)
            mood = MoodType.Sleepy;

        player.Mood = mood;
    }

    /// <summary>
    /// Registra uma vitória de batalha — aumenta amizade.
    /// </summary>
    public void OnBattleWon(PlayerPet player)
    {
        player.AddFriendship(_config.FriendshipOnBattleWin);
        _idleTimer = 0; // Resetar timer idle após evento
        Log.Debug("Mood: Battle won. Friendship +{Amount} = {Total}", _config.FriendshipOnBattleWin, player.Friendship);
    }

    /// <summary>
    /// Registra uma derrota de batalha — diminui amizade.
    /// </summary>
    public void OnBattleLost(PlayerPet player)
    {
        player.LoseFriendship(_config.FriendshipOnBattleLoss);
        Log.Debug("Mood: Battle lost. Friendship -{Amount} = {Total}", _config.FriendshipOnBattleLoss, player.Friendship);
    }

    /// <summary>
    /// Registra uma captura — aumenta amizade.
    /// </summary>
    public void OnCapture(PlayerPet player)
    {
        player.AddFriendship(_config.FriendshipOnCapture);
        _idleTimer = 0;
        Log.Debug("Mood: Capture! Friendship +{Amount} = {Total}", _config.FriendshipOnCapture, player.Friendship);
    }

    /// <summary>
    /// Registra uma carícia (clique) — aumenta amizade com cooldown.
    /// Retorna true se a carícia foi aceita (cooldown ok).
    /// </summary>
    public bool OnPet(PlayerPet player)
    {
        if (_petCooldownTimer > 0)
            return false;

        player.AddFriendship(_config.FriendshipOnPet);
        _petCooldownTimer = _config.PetCooldownSeconds;
        _idleTimer = 0;
        Log.Debug("Mood: Pet! Friendship +{Amount} = {Total}", _config.FriendshipOnPet, player.Friendship);
        return true;
    }

    /// <summary>Reseta o timer idle (quando o pet volta a se mover).</summary>
    public void ResetIdleTimer()
    {
        _idleTimer = 0;
    }

    private MoodType CalculateMood(int friendship)
    {
        if (friendship >= _config.HappyThreshold)
            return MoodType.Happy;
        if (friendship <= _config.SadThreshold)
            return MoodType.Sad;
        return MoodType.Neutral;
    }
}
