using Godot;

/// <summary>
/// 野兽苹果 · 技能 2：之字形穿梭冲锋（天花板 ↔ 地面来回弹，直到抵达对面边界）。
///
/// 这是 <see cref="BeastAppleBoss"/> 的一部分（partial class），
/// 状态机主循环在 BeastAppleBoss.cs，这里只放本技能的实现。
/// </summary>
public partial class BeastAppleBoss
{
    // ══════════════════════════ 技能 2：斜向穿梭冲锋

    /// <summary>技能 2 第一步：先跳到「出发那一侧的边界」，再从边界斜着冲出去。</summary>
    private void StartCharge()
    {
        _chargeDir = GD.Randf() < 0.5f ? -1f : 1f;     // -1 = 往左冲，+1 = 往右冲
        float startX = _chargeDir > 0f ? MinCenterX : MaxCenterX;
        StartReposition(startX, AfterRepo.Charge);
    }

    /// <summary>已经站到边界上了，正式起跳。</summary>
    private void BeginCharge()
    {
        _chargeBounces = 0;
        BounceUp(true);
    }

    /// <summary>
    /// 一次斜向上的起跳。技能 2 会连着弹好几次，所以单独抽出来。
    /// </summary>
    private void BounceUp(bool first)
    {
        _st = St.ChargeUp;
        _chargeHitTop = false;

        ApplyTexture(_chargeDir > 0f ? RightTexture : LeftTexture);
        Rotation = 0f;
        if (_sprite != null) { _sprite.Position = Vector2.Zero; _sprite.Scale = _baseScale; _sprite.Rotation = 0f; }

        Velocity = new Vector2(_chargeDir * ChargeXSpeed, -ChargeJumpVelocity);
        AudioManager.Instance?.PlaySfx("beast_jump", first ? 0.9f : 0.75f, first ? 0.7f : 0.85f);
    }

    /// <summary>冲锋有没有横穿到对面边界。</summary>
    private bool ChargeReachedEnd()
        => _chargeDir > 0f ? GlobalPosition.X >= MaxCenterX - 6f
                           : GlobalPosition.X <= MinCenterX + 6f;

    private void UpdateChargeUp(float dt)
    {
        Velocity = new Vector2(_chargeDir * ChargeXSpeed, Velocity.Y + Gravity * dt);
        MoveAndSlide();
        SpawnChargeTrail(dt);

        // 撞到天花板：换向下落
        bool hitTop = IsOnCeiling() || GlobalPosition.Y <= _ceilCenterY;
        if (hitTop)
        {
            if (GlobalPosition.Y < _ceilCenterY) GlobalPosition = new Vector2(GlobalPosition.X, _ceilCenterY);
            Velocity = new Vector2(_chargeDir * ChargeXSpeed, 60f);
            if (!_chargeHitTop)
            {
                _chargeHitTop = true;
                ChargeImpact(1.25f);
            }
            _st = St.ChargeDown;
        }

        ClampToArena();
    }

    private void UpdateChargeDown(float dt)
    {
        Velocity = new Vector2(_chargeDir * ChargeXSpeed, Velocity.Y + Gravity * dt);
        MoveAndSlide();
        SpawnChargeTrail(dt);

        bool hitBottom = IsOnFloor() || GlobalPosition.Y >= _groundCenterY;
        if (hitBottom)
        {
            GlobalPosition = new Vector2(GlobalPosition.X, _groundCenterY);
            Velocity = Vector2.Zero;
            ChargeImpact(1.0f);

            // 一上一下算「一段」。以前只弹一段就收工，看起来像跳了两下就没了。
            // 现在只要还没横穿到对面边界，就接着再弹一次 —— 一路之字形冲到另一边。
            _chargeBounces++;
            if (ChargeReachedEnd() || _chargeBounces >= ChargeMaxBounces)
            {
                _st = St.Idle;
                _timer = ChargeRecover;
            }
            else
            {
                BounceUp(false);
            }
        }

        ClampToArena();
    }

    /// <summary>
    /// 斜向冲锋的拖影：每隔一小段时间在身后留一个半透明的自己，很快淡掉。
    /// 用贴图副本而不是粒子，是因为拖影要和 Boss 本体形状一致才看得出「同一个东西在飞」。
    /// </summary>
    private void SpawnChargeTrail(float dt)
    {
        if (_sprite == null || _sprite.Texture == null) return;

        _trailTimer -= dt;
        if (_trailTimer > 0f) return;
        _trailTimer = 0.055f;

        var ghost = new Sprite2D
        {
            Texture = _sprite.Texture,
            Scale = _sprite.Scale,
            Rotation = Rotation + _sprite.Rotation,
            GlobalPosition = GlobalPosition,
            Modulate = new Color(1f, 0.42f, 0.38f, 0.38f),
            ZIndex = 6,
        };
        GetParent().AddChild(ghost);

        var tw = CreateTween();
        tw.TweenProperty(ghost, "modulate:a", 0f, 0.26f);
        tw.TweenCallback(Callable.From(ghost.QueueFree));
    }

    /// <summary>
    /// 斜向冲锋撞到天花板 / 地面时的冲击波。
    ///
    /// 和 LandImpact 的区别：这个光圈**整个贴着 Boss 走**（X 和 Y 都跟）。
    /// 冲锋的跳跃初速度可以调到 1200，Boss 一秒能横甩几百像素、上下也极快，
    /// 光圈要是钉死在撞击点，等它扩到最大时 Boss 早飞走了 ——
    /// 看起来就是「波动光圈追不上 Boss」。
    ///
    /// 现在它变成「每撞一次就在身上炸开一圈、跟着身体一起飞」的脉冲，
    /// 速度再快也不会脱节。
    /// 想让光圈改成只锁 X、贴着天花板 / 地面滚，把 FollowXOnly 打开即可。
    /// </summary>
    private void ChargeImpact(float pitch)
    {
        EmitSignal(SignalName.ShakeRequested, 16f, 0.35f);
        AudioManager.Instance?.PlaySfx("beast_slam", 1f, pitch);
        if (_sprite != null)
            _sprite.Scale = _baseScale * new Vector2(1.12f, 0.88f);

        var w = new BeastShockwave
        {
            Radius = _halfW * 1.3f,
            Duration = ChargeRingDuration,
            FollowTarget = this,
            FollowXOnly = !ChargeRingFollowY,
        };
        GetParent().AddChild(w);
        w.GlobalPosition = GlobalPosition;
    }
}
