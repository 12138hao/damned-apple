using Godot;

/// <summary>
/// 野兽苹果 · 死亡演出：碎裂成渣 → 爆开 → 重生复位。
///
/// 这是 <see cref="BeastAppleBoss"/> 的一部分（partial class），
/// 状态机主循环在 BeastAppleBoss.cs，这里只放本技能的实现。
/// </summary>
public partial class BeastAppleBoss
{
    // ══════════════════════════ 死亡

    protected override void OnDeath()
    {
        Velocity = Vector2.Zero;
        Rotation = 0f;
        _deadT = 0f;
        _exploded = false;

        // Boss 死了，它吐出来的恐怖苹果、还在飞的吐球、地面绿圈一起清掉。
        // 不然过场播完，玩家还得自己把场上那几只追人的苹果解决掉才能走。
        ClearSpawned();

        // 关掉受击判定，尸体不挡子弹
        var body = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (body != null) body.SetDeferred("disabled", true);

        EmitSignal(SignalName.Defeated);
    }

    private void UpdateDeathCustom(float dt)
    {
        _deadT += dt;

        if (!_exploded && _deadT >= 0.25f)
        {
            _exploded = true;
            PlayExplosion();
        }

        // 爆炸后本体淡出
        if (_exploded)
        {
            float k = Mathf.Clamp((_deadT - 0.25f) / Mathf.Max(0.1f, ExplodeTime - 0.25f), 0f, 1f);
            if (_sprite != null) _sprite.Modulate = new Color(1f, 1f, 1f, 1f - k);
        }

        if (_deadT >= ExplodeTime) QueueFree();
    }

    private void PlayExplosion()
    {
        // 死亡演出是纯视觉，不结算任何伤害
        AudioManager.Instance?.PlaySfx("beast_explode", 1f, 0.9f);
        EmitSignal(SignalName.ShakeRequested, 26f, 0.7f);

        // ① 本体碎裂成渣：先把自己藏掉，再放独立的碎裂序列，
        //    否则会同时看到「一个完整的苹果」和「一堆碎片」，很出戏。
        if (_sprite != null) _sprite.Visible = false;

        var shatter = new BeastShatter
        {
            Name = "beast_shatter",   // 显式命名，方便探针/调试查找
            Sheet = ShatterSheet,
            Frames = ShatterFrames,
            FrameTime = ShatterFrameTime,
            TargetHeight = BodyHeight,
        };
        GetParent().AddChild(shatter);
        shatter.GlobalPosition = GlobalPosition;

        // ② 两圈冲击波，第二圈慢半拍
        for (int i = 0; i < 2; i++)
        {
            var w = new BeastShockwave
            {
                Radius = 150f + i * 90f,
                Duration = 0.55f + i * 0.18f,
                Delay = i * 0.16f,
            };
            GetParent().AddChild(w);
            w.GlobalPosition = GlobalPosition;
        }

        // ③ 四散的碎块（比原来更多、更远，配合碎裂的感觉）
        for (int i = 0; i < 16; i++)
        {
            var chip = new BeastShockwave
            {
                Radius = 22f + GD.Randf() * 24f,
                Duration = 0.7f + GD.Randf() * 0.5f,
                Delay = GD.Randf() * 0.2f,
                IsDebris = true,
            };
            GetParent().AddChild(chip);
            chip.GlobalPosition = GlobalPosition + new Vector2(
                (GD.Randf() - 0.5f) * _halfW * 1.4f,
                (GD.Randf() - 0.5f) * _halfH * 1.4f);
        }
    }

    protected override void OnDeadFinished()
    {
        // 由 UpdateDeathCustom 接管，父类那套压扁淡出不要
    }

    // ══════════════════════════ 工具

    private void EnterDormant()
    {
        _st = St.Dormant;
        _timer = 0f;
        _animT = 0f;
        Rotation = Mathf.Pi;
        GlobalPosition = _hangPos;
        Velocity = Vector2.Zero;
        ApplyTexture(FrontTexture);
        if (_sprite != null)
        {
            _sprite.Scale = _baseScale;
            _sprite.Rotation = 0f;
            _sprite.Modulate = Colors.White;
            _sprite.Visible = true;   // 死亡时藏掉过，重生要还回来
        }
    }

    public override void Respawn()
    {
        base.Respawn();

        // 上一轮吐出来的恐怖苹果、还在飞的吐球、地上的绿圈，全部清掉。
        // 不清理的话：玩家死后重开，上一轮召唤的怪会继续追着刚复活的玩家打。
        ClearSpawned();

        _dead = false;
        _phaseTwoDone = false;
        _phaseTwoPending = false;
        _lungeHitDone = false;
        _chargeHitTop = false;
        _chargeBounces = 0;
        _flash = 0f;
        _exploded = false;
        _deadT = 0f;
        _lastHp = Hp;

        var body = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (body != null) body.SetDeferred("disabled", false);
        if (_sprite != null) _sprite.Modulate = Colors.White;

        EnterDormant();
    }
}
