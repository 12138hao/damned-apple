using Godot;

/// <summary>
/// 野兽苹果 · 半血阶段：跳天花板倒挂 → 循环吐 horrorapple。
///
/// 这是 <see cref="BeastAppleBoss"/> 的一部分（partial class），
/// 状态机主循环在 BeastAppleBoss.cs，这里只放本技能的实现。
/// </summary>
public partial class BeastAppleBoss
{
    // ══════════════════════════ 半血：跳回天花板吐怪

    private void StartPhaseTwo()
    {
        if (_phaseTwoDone) return;
        _phaseTwoDone = true;
        EmitSignal(SignalName.PhaseTwoStarted);

        _st = St.PhaseJump;
        _timer = 0f;
        _animT = 0f;
        Velocity = Vector2.Zero;
        if (_sprite != null) { _sprite.Position = Vector2.Zero; _sprite.Scale = _baseScale; _sprite.Rotation = 0f; }
        ApplyTexture(FrontTexture);
        AudioManager.Instance?.PlaySfx("beast_roar", 1f, 0.85f);
        EmitSignal(SignalName.ShakeRequested, 14f, 0.4f);
    }

    /// <summary>从地面飞回天花板中心，途中翻成倒挂。</summary>
    private void UpdatePhaseJump(float dt)
    {
        _animT += dt;
        float k = Mathf.Clamp(_animT / 0.75f, 0f, 1f);
        float ease = k * k * (3f - 2f * k);

        float x = Mathf.Lerp(GlobalPosition.X, ZoneCenterX, Mathf.Min(1f, dt * 8f));
        float y = Mathf.Lerp(_groundCenterY, _hangPos.Y, ease);
        GlobalPosition = new Vector2(x, y);
        Rotation = Mathf.Pi * ease;

        if (_sprite != null)
            _sprite.Scale = _baseScale * new Vector2(1f - 0.12f * (1f - ease), 1f + 0.12f * (1f - ease));

        if (k >= 1f)
        {
            GlobalPosition = new Vector2(ZoneCenterX, _hangPos.Y);
            Rotation = Mathf.Pi;
            if (_sprite != null) _sprite.Scale = _baseScale;
            EnterPhaseHang();
        }
    }

    private void EnterPhaseHang()
    {
        _st = St.PhaseHang;
        _spawnLeft = SpawnCount;
        _spawnTimer = 0.35f;   // 先挂稳 0.35 秒再吐第一只
        Velocity = Vector2.Zero;
        Rotation = Mathf.Pi;
        ApplyTexture(FrontTexture);
    }

    private void UpdatePhaseHang(float dt)
    {
        // 倒挂轻轻晃
        if (_sprite != null)
            _sprite.Rotation = Mathf.Sin(_animT * 2.2f) * 0.05f;

        if (_spawnLeft <= 0) return;

        _spawnTimer -= dt;
        if (_spawnTimer > 0f) return;

        SpitOne();
        _spawnLeft--;
        _spawnTimer = SpawnInterval;

        if (_spawnLeft <= 0)
        {
            _st = St.PhaseWait;
            _phaseWaitTimer = PhaseHangTime;
        }
    }

    private void UpdatePhaseWait(float dt)
    {
        if (_sprite != null)
            _sprite.Rotation = Mathf.Sin(_animT * 2.2f) * 0.05f;

        _phaseWaitTimer -= dt;
        if (_phaseWaitTimer > 0f) return;

        // 跳下来
        _st = St.Falling;
        _timer = 0f;
        _animT = 0f;
        Rotation = Mathf.Pi;
        Velocity = new Vector2(0f, 200f);
        AudioManager.Instance?.PlaySfx("beast_slam", 0.7f, 1.15f);
    }

    // ══════════════════════════ 召唤物容器

    /// <summary>清空所有召唤物 + 地面绿圈。重生时调用。</summary>
    private void ClearSpawned()
    {
        if (_lungeWarn != null && IsInstanceValid(_lungeWarn))
        {
            _lungeWarn.StopWarning();
            _lungeWarn = null;
        }

        if (_spawnRoot == null || !IsInstanceValid(_spawnRoot)) return;

        // QueueFree 父节点会递归释放所有子节点，不用逐个点。
        // 用 QueueFree 而不是 Free：这里可能正处在信号回调里，立即释放会崩。
        _spawnRoot.QueueFree();
        _spawnRoot = null;
    }

    /// <summary>吐一只恐怖苹果：从嘴里抛物线下落到战场内的随机位置。</summary>
    private void SpitOne()
    {
        // 落点：战场内随机，留出边界，避免掉出平台。
        // Y 要抬起来：HorrorAppleEnemy 的碰撞盒是 54×68、中心在节点下方 11px，
        // 节点直接放在地面高度会让它下半截陷进地里。
        float pad = _halfW + 90f;
        float landX = (float)GD.RandRange(ArenaRect.Position.X + pad,
                                          ArenaRect.Position.X + ArenaRect.Size.X - pad);
        var landPos = new Vector2(landX, FloorY - 34f);

        // 起点：倒挂时的「嘴」，在 Boss 中心偏下一点
        var origin = GlobalPosition + new Vector2((GD.Randf() - 0.5f) * _halfW * 0.6f, _halfH * 0.55f);

        var ball = new BeastSpitBall();
        SpawnRoot.AddChild(ball);   // 挂进容器，玩家死了能一起清掉
        ball.Setup(origin, landPos, 0.72f, () =>
        {
            SpawnHorrorApple(landPos);
        });

        AudioManager.Instance?.PlaySfx("beast_spit", 1.1f, 1f);
    }

    /// <summary>在落点生成一只恐怖苹果（代码拼节点，不依赖场景）。</summary>
    private void SpawnHorrorApple(Vector2 pos)
    {
        var apple = new HorrorAppleEnemy
        {
            // 必须带序号：同名节点入树时 Godot 会直接改成 "@CharacterBody2D@59" 这种自动名，
            // 之后就再也认不出哪只是吐出来的了。
            Name = $"spit_apple_{++_spitSeq}",
            CollisionLayer = 4,
            CollisionMask = 1,
            Damage = 15,
            MaxHp = 100,

            // 只有 Boss 吐出来的这四只会追人。
            // 场景里原有的 horrorapple 这个字段是 false（默认值），行为一点不变。
            ChasePlayer = true,
        };
        apple.Position = pos;

        var shape = new CollisionShape2D
        {
            Name = "CollisionShape2D",
            Position = new Vector2(0f, 11f),
            Shape = new RectangleShape2D { Size = new Vector2(54f, 68f) },
        };
        apple.AddChild(shape);

        // 名字必须显式给：代码 new 出来的节点默认名是空的，
        // 入树后 Godot 会分配 "@Sprite2D@xx" 这种自动名，
        // 而 Enemy._Ready 里是 GetNode("Sprite2D") 硬取的，找不到就变 null。
        var sheet = GD.Load<Texture2D>("res://assets/hop_apple_sheet.png");
        var sp = new Sprite2D
        {
            Name = "Sprite2D",
            Scale = new Vector2(0.65f, 0.65f),
            Texture = sheet,
            Hframes = 32,
        };
        apple.AddChild(sp);

        // 基类硬取这三个节点，缺了会抛异常
        apple.AddChild(new RayCast2D { Position = new Vector2(-22f, 10f), TargetPosition = new Vector2(0f, 30f), Name = "RayLeft" });
        apple.AddChild(new RayCast2D { Position = new Vector2(22f, 10f), TargetPosition = new Vector2(0f, 30f), Name = "RayRight" });

        var hurt = new Area2D { Name = "HurtArea", CollisionMask = 2, CollisionLayer = 0 };
        hurt.AddChild(new CollisionShape2D
        {
            Position = new Vector2(0f, 8.5f),
            Shape = new RectangleShape2D { Size = new Vector2(64f, 73f) },
        });
        apple.AddChild(hurt);

        // 血条：Enemy._Ready 里是 GetNodeOrNull<TextureProgressBar>("HpBar") 硬取的，
        // 不挂这个节点它就拿不到 _bar，表现出来就是「吐出来的苹果没有血条」。
        // 尺寸 / 位置照抄 underground.tscn 里 horrorapple 的那个（44×8，浮在头顶）。
        var bar = new TextureProgressBar
        {
            Name = "HpBar",
            Position = new Vector2(-22f, -52f),
            Size = new Vector2(44f, 8f),
            MaxValue = 100,
            Value = 100,
            TextureUnder = GD.Load<Texture2D>("res://assets/ui/enemy_bar_bg.png"),
            TextureProgress = GD.Load<Texture2D>("res://assets/ui/enemy_bar_fill.png"),
            TextureOver = GD.Load<Texture2D>("res://assets/ui/enemy_bar_border.png"),
        };
        apple.AddChild(bar);

        SpawnRoot.AddChild(apple);
        apple.GlobalPosition = pos;

        // 落地烟尘（贴地面画，不跟着苹果抬起来）
        var fx = new BeastShockwave { Radius = 54f, Duration = 0.35f };
        GetParent().AddChild(fx);
        fx.GlobalPosition = new Vector2(pos.X, FloorY);
    }
}
