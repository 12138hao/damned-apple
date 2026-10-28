using Godot;

/// <summary>
/// 地下洞穴里的跳跃苹果怪。
///
/// 和第一关的苹果怪（Enemy / AppleEnemy）的区别：
///   · **不会走**，只会一下一下地蹦着移动（所以没有走路帧）
///   · 发现玩家会先蹲下蓄力（有预警），再朝玩家扑出去咬一口
///   · **死亡直接炸开**，不爆炸、不留毒圈、不给范围伤害
///
/// 帧表（assets/hop_apple_sheet.png，32 帧，每帧 160×160）：
///   0~3    呼吸（朝右）
///   4~7    呼吸（朝左）
///   8~13   蹦跳循环（朝右）
///   14~19  蹦跳循环（朝左）
///   20~25  飞扑撕咬（正视图，蓄力→前冲→张嘴→回收→站定）
///   26~31  死亡炸开
///
/// 挂法：CharacterBody2D（collision_layer=4）+ 子节点 Sprite2D（hframes=32）+ HurtArea。
/// </summary>
public partial class HopAppleEnemy : Enemy
{
    // ─────────────────────────── 帧表
    [ExportGroup("帧表")]
    [Export] public int IdleRightStart = 0;    // 呼吸朝右：0~3
    [Export] public int IdleLeftStart = 4;     // 呼吸朝左：4~7
    [Export] public int HopRightStart = 8;     // 蹦跳朝右：8~13
    [Export] public int HopLeftStart = 14;     // 蹦跳朝左：14~19
    [Export] public int LungeStart = 20;       // 飞扑：20~25
    [Export] public int DeathStart = 26;       // 死亡炸开：26~31
    [Export] public int DeathEnd = 31;
    [Export] public float IdleAnimInterval = 0.18f;   // 呼吸帧间隔
    [Export] public float HopAnimInterval = 0.07f;    // 蹦跳帧间隔
    [Export] public float LungeAnimInterval = 0.06f;  // 飞扑帧间隔
    [Export] public float DeathAnimInterval = 0.07f;  // 死亡帧间隔

    // ─────────────────────────── 蹦跳
    [ExportGroup("蹦跳")]
    [Export] public float HopInterval = 0.85f;      // 落地后停多久再跳下一次
    [Export] public float HopSpeed = 130f;          // 跳跃时的水平速度
    [Export] public float HopImpulse = 420f;        // 起跳的向上初速度

    // ─────────────────────────── 侦测与飞扑
    [ExportGroup("侦测与飞扑")]
    [Export] public float DetectRangeX = 300f;      // 水平多近能发现玩家
    [Export] public float DetectRangeY = 140f;      // 垂直高度差容忍（太小就只打同一层的）
    [Export] public float LungeChargeTime = 0.5f;   // 蓄力预警时长——玩家的反应窗口
    [Export] public float LungeSpeed = 430f;        // 扑出去的速度（玩家 300，稍快但能躲）
    [Export] public float LungeTime = 0.26f;        // 冲出去多久
    [Export] public float RecoverTime = 0.55f;      // 扑完喘气，这段时间不设防也不追
    [Export] public float LungeCooldown = 1.4f;     // 两次飞扑之间至少隔多久

    private enum St { Idle, Hop, Charge, Lunge, Recover }
    private St _st = St.Idle;

    private float _timer;          // 状态计时
    private float _lungeCd;        // 飞扑冷却
    private Box _player;
    private Vector2 _lungeDir = Vector2.Right;
    private bool _landedOnce;      // 这一跳是否已经落地过（避免起跳瞬间被判定落地）
    private int _frameOffset;      // 当前帧段内的偏移

    public override void _Ready()
    {
        base._Ready();
        _player = GetTree().Root.FindChild("box", true, false) as Box;
        _timer = HopInterval;
        // 第一关的苹果怪是走路帧在循环，这里换成 32 帧表，必须自己指定
        if (_sprite != null)
        {
            _sprite.Hframes = 32;
            _sprite.Frame = IdleRightStart;
        }
    }

    /// <summary>父类每帧会拿这个覆盖 Velocity.X，所以水平速度必须在这里给。</summary>
    protected override float ComputeHorizontalVelocity(float dt)
    {
        switch (_st)
        {
            case St.Hop:
                return Direction * HopSpeed;
            case St.Lunge:
                return _lungeDir.X * LungeSpeed;
            case St.Charge:
                return 0f;                 // 蓄力时站定，给玩家看清楚的信号
            default:
                return 0f;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        if (_dead)
        {
            base._PhysicsProcess(delta);   // 父类负责死亡计时与隐藏
            return;
        }

        base._PhysicsProcess(delta);       // 重力、移动、接触伤害都交给父类

        _timer -= dt;
        _lungeCd = Mathf.Max(0f, _lungeCd - dt);

        bool onFloor = IsOnFloor();

        switch (_st)
        {
            case St.Idle:
                // 站定的时候才找玩家，跳在半空中不改变主意
                if (TryStartLunge()) break;
                if (_timer <= 0f)
                {
                    StartHop();
                }
                break;

            case St.Hop:
                // 起跳那一帧可能还在上一帧的地面状态，先等离地再判断落地
                if (!onFloor) _landedOnce = true;
                else if (_landedOnce)
                {
                    _st = St.Idle;
                    _timer = HopInterval;
                    _landedOnce = false;
                    Velocity = new Vector2(0f, Velocity.Y);
                }
                break;

            case St.Charge:
                if (_timer <= 0f)
                {
                    _st = St.Lunge;
                    _timer = LungeTime;
                    _frameOffset = 0;
                }
                break;

            case St.Lunge:
                if (_timer <= 0f)
                {
                    _st = St.Recover;
                    _timer = RecoverTime;
                    _frameOffset = 0;
                    Velocity = new Vector2(Velocity.X * 0.3f, Velocity.Y);
                }
                break;

            case St.Recover:
                if (_timer <= 0f)
                {
                    _st = St.Idle;
                    _timer = HopInterval;
                }
                break;
        }
    }

    /// <summary>发现玩家就蹲下蓄力。返回 true 表示已经进入蓄力。</summary>
    private bool TryStartLunge()
    {
        if (_lungeCd > 0f) return false;
        if (_player == null || !IsInstanceValid(_player)) return false;

        float dx = _player.GlobalPosition.X - GlobalPosition.X;
        float dy = Mathf.Abs(_player.GlobalPosition.Y - GlobalPosition.Y);
        if (Mathf.Abs(dx) > DetectRangeX) return false;
        if (dy > DetectRangeY) return false;

        // 只有背对玩家时才转身，免得面对玩家还先转一圈
        Direction = dx >= 0f ? 1 : -1;
        _lungeDir = new Vector2(Mathf.Sign(dx), 0f);
        if (_lungeDir.X == 0f) _lungeDir = Vector2.Right;

        _st = St.Charge;
        _timer = LungeChargeTime;
        _lungeCd = LungeChargeTime + LungeTime + RecoverTime + LungeCooldown;
        _frameOffset = 0;
        Velocity = new Vector2(0f, Velocity.Y);
        return true;
    }

    private void StartHop()
    {
        if (!IsOnFloor())
        {
            _timer = 0.1f;      // 还没落地，稍后再试
            return;
        }
        _st = St.Hop;
        _landedOnce = false;
        Velocity = new Vector2(Direction * HopSpeed, -HopImpulse);
    }

    // ─────────────────────────── 动画
    protected override void UpdateAnimation(float dt)
    {
        if (_sprite == null) return;

        // 死亡优先，帧率单独控制
        if (_dead)
        {
            if (_sprite.Frame < DeathStart) { _sprite.Frame = DeathStart; _animTimer = 0f; return; }
            _animTimer += dt;
            if (_animTimer < DeathAnimInterval) return;
            _animTimer = 0f;
            if (_sprite.Frame < DeathEnd) _sprite.Frame++;
            return;
        }

        switch (_st)
        {
            case St.Hop:
                PlaySegment(HopRightStart, HopLeftStart, 6, HopAnimInterval, dt);
                break;

            case St.Charge:
                // 蓄力：用飞扑段的前两帧（后仰 → 压紧）
                _animTimer += dt;
                _frameOffset = _timer > LungeChargeTime * 0.5f ? 0 : 1;
                _sprite.Frame = LungeStart + _frameOffset;
                break;

            case St.Lunge:
                // 冲出去的三帧：拉伸 → 张嘴 → 咬合
                _animTimer += dt;
                if (_animTimer >= LungeAnimInterval)
                {
                    _animTimer = 0f;
                    _frameOffset = Mathf.Min(_frameOffset + 1, 2);
                }
                _sprite.Frame = LungeStart + 2 + _frameOffset;
                break;

            case St.Recover:
                _sprite.Frame = LungeStart + 4;
                _animTimer = 0f;
                break;

            default:
                PlaySegment(IdleRightStart, IdleLeftStart, 4, IdleAnimInterval, dt);
                break;
        }
    }

    /// <summary>按朝向播一段循环帧。</summary>
    private void PlaySegment(int rightStart, int leftStart, int count, float interval, float dt)
    {
        int start = Direction > 0 ? rightStart : leftStart;
        _sprite.FlipH = false;                 // 贴图自带左右，不需要镜像
        _animTimer += dt;
        if (_animTimer < interval) return;
        _animTimer = 0f;

        int f = _sprite.Frame;
        if (f < start || f >= start + count) f = start;
        else f = start + (f - start + 1) % count;
        _sprite.Frame = f;
    }

    // ─────────────────────────── 死亡
    /// <summary>
    /// 死亡瞬间关掉碰撞并进入 Idle，让炸开动画自己播完。
    /// 父类要等动画播完才关碰撞，那样碎片还在飞的时候子弹会被挡住。
    /// </summary>
    protected override void OnDeath()
    {
        base.OnDeath();
        _st = St.Idle;
        var body = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (body != null) body.SetDeferred("disabled", true);
        if (_sprite != null) _sprite.Frame = DeathStart;
    }

    /// <summary>重写父类：不压扁不淡出，直接播炸开帧。</summary>
    protected override void UpdateDead(float dt)
    {
        _deadTimer -= dt;
        if (_deadTimer <= 0f) OnDeadFinished();
    }

    public override void Respawn()
    {
        base.Respawn();
        _st = St.Idle;
        _timer = HopInterval;
        _lungeCd = 0f;
        _landedOnce = false;
        _animTimer = 0f;
        if (_sprite != null) _sprite.Frame = IdleRightStart;
    }
}
