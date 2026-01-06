using Pokebar.Core.Models;
using Pokebar.DesktopPet.Animation;

namespace Pokebar.DesktopPet.Entities;

public class PokemonPet : BaseEntity
{
    private AnimationClip? _walkRightClip;
    private AnimationClip? _walkLeftClip;
    private AnimationClip? _walkFallbackClip;
    private AnimationClip? _idleClip;
    private AnimationClip? _fightFallbackClip;
    private bool _isWalking;
    private bool _useDirectionalWalk;

    public PokemonPet(int dex, string formId = "0000") : base(dex, formId)
    {
    }

    public bool ShouldFlip
    {
        get
        {
            // Durante combate, sempre usar flip (não tem animações direcionais de fight)
            if (State == EntityState.Fighting)
                return true;
            
            // Durante walk, só usa flip se não tiver animações direcionais
            if (_useDirectionalWalk && _isWalking)
                return false;
            
            return true;
        }
    }

    public void LoadAnimations(SpriteLoader loader, GameplayConfig config)
    {
        var walkRowRight = config.Sprite.WalkRowRight;
        var walkRowLeft = config.Sprite.WalkRowLeft;
        
        _walkRightClip = loader.LoadAnimation(Dex, FormId, AnimationType.Walk, new[] { walkRowRight }, requireSelection: true);
        _walkLeftClip = loader.LoadAnimation(Dex, FormId, AnimationType.Walk, new[] { walkRowLeft }, requireSelection: true);
        _useDirectionalWalk = _walkRightClip != null && _walkLeftClip != null;

        if (!_useDirectionalWalk)
        {
            _walkFallbackClip = loader.LoadAnimation(Dex, FormId, AnimationType.Walk);
            _walkRightClip = null;
            _walkLeftClip = null;
        }

        _idleClip = loader.LoadAnimation(Dex, FormId, AnimationType.Idle);
        
        // Para fight, não tentar carregar direcional - sempre usar fallback com flip
        _fightFallbackClip = loader.LoadAnimation(Dex, FormId, AnimationType.Fight);
        
        if (loader.TryGetOffset(UniqueId, out var offset))
            SetHitbox(offset.HitboxX, offset.HitboxY, offset.HitboxWidth, offset.HitboxHeight);

        if (_walkFallbackClip == null && !_useDirectionalWalk)
            _walkFallbackClip = _idleClip;
        _idleClip ??= _walkFallbackClip ?? _walkRightClip ?? _walkLeftClip;

        var startClip = _idleClip ?? _walkFallbackClip ?? _walkRightClip ?? _walkLeftClip;
        if (startClip != null)
            AnimationPlayer.Play(startClip);
    }

    public void StartWalking()
    {
        _isWalking = true;
        State = EntityState.Walking;
        ApplyWalkClip();
    }

    public void StartIdle(bool updateState = true)
    {
        _isWalking = false;
        if (updateState)
            State = EntityState.Idle;
        if (_idleClip != null)
            AnimationPlayer.Play(_idleClip);
    }

    public void StartFighting()
    {
        _isWalking = false;
        State = EntityState.Fighting;
        var fightClip = ResolveFightClip();
        if (fightClip != null)
            AnimationPlayer.Play(fightClip, restart: true);
        else
            StartIdle(false);
    }

    public override void Update(double deltaTime)
    {
        base.Update(deltaTime);

        // Durante combate, nÃ£o atualizar animaÃ§Ã£o nem direÃ§Ã£o
        if (State == EntityState.Fighting)
            return;

        if (_isWalking)
            ApplyWalkClip();
    }

    private void ApplyWalkClip()
    {
        if (_useDirectionalWalk)
        {
            var target = FacingRight ? _walkRightClip : _walkLeftClip;
            if (target != null && AnimationPlayer.CurrentClip != target)
                AnimationPlayer.Play(target);
        }
        else if (_walkFallbackClip != null && AnimationPlayer.CurrentClip != _walkFallbackClip)
        {
            AnimationPlayer.Play(_walkFallbackClip);
        }
    }

    private AnimationClip? ResolveFightClip()
    {
        // Sempre usa fallback para fight (sem animaÃ§Ãµes direcionais)
        return _fightFallbackClip ?? _idleClip;
    }
}
