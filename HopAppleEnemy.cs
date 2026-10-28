using Godot;

/// <summary>
/// 地下洞穴里的跳跃苹果怪。
///
/// 和第一关苹果怪（Enemy / AppleEnemy）的区别：
///   · **不会走**，只会一下一下地蹦（没有走路帧）
///   · 发现玩家先蹲下蓄力（有预警），再朝玩家扑出去咬一口
///   · 飞扑用**左右侧视图**：往右扑就用朝右的图，往左就用朝左的
///   · **死亡直接炸开**，不爆炸、不留毒圈
///
/// 帧表（assets/hop_apple_sheet.png，32 帧，每帧 160×160）：
///   0~3    呼吸（朝右）      4~7    呼吸（朝左）
///   8~12   蹦跳（朝右）      13~17  蹦跳（朝左）
///   18~21  飞扑（朝右）      22~25  飞扑（朝左）
///   26~31  死亡炸开
///
/// 挂法：CharacterBody2D（layer=4）+ Sprite2D（hframes=32）+ HurtArea
///      + RayLeft / RayRight（基类硬取，缺了会抛异常）。
/// </summary>
public partial class HopAppleEnemy : Enemy
{
    // ─────────────────────────── 帧表
    [ExportGroup("帧表")]
    [Export] public int IdleRightStart = 0;      // 呼吸朝右：0~3
    [Export] public int IdleLeftStart = 4;       // 呼吸朝左：4~7
    [Export] public int HopRightStart = 8;       // 蹦跳朝右：8~12
    [Export] public int HopLeftStart = 13;       // 蹦跳朝左：13~17
    [Export] public int LungeRightStart = 18;    // 飞扑朝右：18~21
    [Export] public int LungeLeftStart = 22;     // 飞扑朝左：22~25
    [Export] public int DeathStart = 26;         // 死亡炸开：26~31
    [Export] public int DeathEnd = 31;
    [Export] public float IdleAnimInterval = 0.18f;
    [Export] public float DeathAnimInterval = 0.07f;

    // ─────────────────────────── 蹦跳
    [ExportGroup("蹦跳")]
    [Export] public float HopInterval = 0.55f;   // 落地后停多久再跳下一次
    [Export] public float HopSpeed = 190f;       // 跳跃时的水平速度
    [Export] public float HopImpulse = 440f;     // 起跳的向上初速度（越大跳越高）
    [Export] public float LandHoldTime = 0.12f;  // 落地压扁保持多久
    [Export] public float HopProbeDistance = 150f; // 跳跃前探测坑的距离（沿朝向前方）
    [Export] public float HopProbeDepth = 180f;    // 探测向下多深算「有地面」

    // ─────────────────────────── 侦测与飞扑
    [ExportGroup("侦测与飞扑")]
    [Export] public float DetectRangeX = 300f;   // 水平多近能发现玩家
    [Export] public float DetectRangeY = 140f;   // 垂直高度差容忍
    [Export] public float LungeChargeTime = 0.5f;  // 蓄力预警时长——玩家的反应窗口
    [Export] public float LungeSpeed = 1600f;    // 扑出去的水平速度上限（原来是 800，翻倍）
    [Export] public float LungeMinSpeed = 300f;  // 水平速度下限（玩家贴脸时也不至于慢吞吞）
    [Export] public float LungeSpeedBias = 1.65f; // 在「正好落到玩家身上」的基础上再加速（会略微冲过头）
    [Export] public float LungeJumpImpulse = 286f; // 向上初速度：405×√0.5，高度再降 50%（现 34px）
    [Export] public float LungeTime = 1.2f;      // 最长滞空时间（落地就提前结束，要比空中时间留余量）
    [Export] public float RecoverTime = 0.5f;    // 扑完喘气
    [Export] public float LungeHitWidth = 43f;    // 命中判定半宽 = 怪半宽27 + 玩家半宽16
    [Export] public float LungeHitHeight = 50f;   // 命中判定半高 = 怪半高34 + 玩家半高16
    [Export] public float LungeHitCenterY = 11f;  // 怪物身体中心相对节点的偏移（和碰撞体一致）
    [Export] public float LungeCooldown = 1.1f;  // 两次飞扑之间至少隔多久

    private enum St { Idle, Hop, Charge, Lunge, Recover }
    private St _st = St.Idle;

    private float _timer;
    private float _lungeCd;
    private float _landHold;
    private bool _lungeAirborne;   // 飞扑时是否已经离地（避免起跳当帧就被判定落地）
    private float _lungeVx;        // 本次扑击的水平速度（按距离算，保证落在玩家身上）
    private float _targetDx;       // 起跳瞬间和玩家的水平距离
    private bool _lungeHitDone;    // 本次飞扑是否已经结算过伤害
    private Box _player;
    private Vector2 _lungeDir = Vector2.Right;
    private bool _landedOnce;
    private Vector2 _prevPos;      // 上一物理帧的位置，扫掠判定用

    public override void _Ready()
    {
        base._Ready();
        _player = GetTree().Root.FindChild("box", true, false) as Box;
        _timer = HopInterval;
        _prevPos = GlobalPosition;
        if (_sprite != null)
        {
            _sprite.Hframes = 32;
            _sprite.Frame = IdleRightStart;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        if (_dead)
        {
            base._PhysicsProcess(delta);
            return;
        }

        _prevPos = GlobalPosition;     // 记录移动前的位置，扫掠判定要用这一段
        base._PhysicsProcess(delta);   // 重力、移动、接触伤害都交给父类


        _timer -= dt;
        _lungeCd = Mathf.Max(0f, _lungeCd - dt);
        _landHold = Mathf.Max(0f, _landHold - dt);

        bool onFloor = IsOnFloor();

        switch (_st)
        {
            case St.Idle:
                if (TryStartLunge()) break;
                if (_timer <= 0f) StartHop();
                break;

            case St.Hop:
                if (!onFloor) _landedOnce = true;
                else if (_landedOnce)
                {
                    _landedOnce = false;
                    _st = St.Idle;
                    _timer = HopInterval;
                    _landHold = LandHoldTime;
                    Velocity = new Vector2(0f, Velocity.Y);
                }
                break;

            case St.Charge:
                if (_timer <= 0f)
                {
                    // 起跳：水平 + 向上一起给，之后交给重力画抛物线
                    _st = St.Lunge;
                    _timer = LungeTime;
                    _lungeAirborne = false;
                    _lungeHitDone = false;
                    // 按滞空时间反算水平速度，让落点大致压在玩家身上；
                    // 再乘 LungeSpeedBias 让它更凶（会略微冲过头，这是「更快」的代价）
                    float air = 2f * LungeJumpImpulse / Mathf.Max(1f, Gravity);
                    float wantDx = Mathf.Abs(_targetDx);
                    float ideal = wantDx / Mathf.Max(0.05f, air) * LungeSpeedBias;
                    _lungeVx = Mathf.Clamp(ideal, LungeMinSpeed, LungeSpeed);
                    Velocity = new Vector2(_lungeDir.X * _lungeVx, -LungeJumpImpulse);
                }
                break;

            case St.Lunge:
                TryLungeHit();
                if (!onFloor) _lungeAirborne = true;
                // 落地 或 超时就收
                if (_timer <= 0f || (_lungeAirborne && onFloor))
                {
                    _st = St.Recover;
                    _timer = RecoverTime;
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

    /// <summary>父类每帧用这个覆盖 Velocity.X。</summary>
    protected override float ComputeHorizontalVelocity(float dt)
    {
        switch (_st)
        {
            case St.Hop:   return Direction * HopSpeed;
            case St.Lunge: return _lungeDir.X * _lungeVx;
            default:       return 0f;      // 蓄力时站定，给玩家看清信号
        }
    }

    private bool TryStartLunge()
    {
        if (_lungeCd > 0f) return false;
        if (_player == null || !IsInstanceValid(_player)) return false;

        float dx = _player.GlobalPosition.X - GlobalPosition.X;
        float dy = Mathf.Abs(_player.GlobalPosition.Y - GlobalPosition.Y);
        if (Mathf.Abs(dx) > DetectRangeX) return false;
        if (dy > DetectRangeY) return false;

        Direction = dx >= 0f ? 1 : -1;
        _lungeDir = new Vector2(Direction, 0f);

        _targetDx = dx;
        _st = St.Charge;
        _timer = LungeChargeTime;
        _lungeCd = LungeChargeTime + LungeTime + RecoverTime + LungeCooldown;
        Velocity = new Vector2(0f, Velocity.Y);
        return true;
    }

    private void StartHop()
    {
        if (!IsOnFloor())
        {
            _timer = 0.1f;
            return;
        }

        // 前方是坑就别蹦了。能掉头就掉头，两边都是坑就原地等。
        // 只有「蹦」受这个限制；飞扑是主动攻击，允许往坑里扑。
        if (!GroundAhead(Direction))
        {
            if (GroundAhead(-Direction))
            {
                Direction = -Direction;
            }
            else
            {
                _timer = 0.25f;      // 两边都没地，等一会儿再试
                return;
            }
        }

        _st = St.Hop;
        _landedOnce = false;
        _landHold = 0f;
        Velocity = new Vector2(Direction * HopSpeed, -HopImpulse);
    }

    /// <summary>
    /// 飞扑命中判定：把「上一帧位置 → 本帧位置」连成线段，算玩家到线段的最近距离。
    ///
    /// 为什么不是简单的圆/矩形判定：
    ///   · 圆/矩形判定框开大一点就会「没碰到也扣血」
    ///   · 框贴紧体积又会在 1000 px/s 穿过去时漏判（每帧走 17px，可能直接跨过玩家）
    /// 用扫掠线段 + 贴着体积的框，两个问题一起解决。
    /// </summary>
    private void TryLungeHit()
    {
        if (_lungeHitDone) return;
        if (_player == null || !IsInstanceValid(_player)) return;

        Vector2 off = new Vector2(0f, LungeHitCenterY);
        Vector2 a = _prevPos + off;
        Vector2 b = GlobalPosition + off;
        Vector2 c = _player.GlobalPosition;

        Vector2 ab = b - a;
        float len2 = ab.LengthSquared();
        float t = len2 <= 0.0001f ? 0f : Mathf.Clamp((c - a).Dot(ab) / len2, 0f, 1f);
        Vector2 closest = a + ab * t;

        Vector2 d = c - closest;
        if (Mathf.Abs(d.X) > LungeHitWidth) return;
        if (Mathf.Abs(d.Y) > LungeHitHeight) return;

        _lungeHitDone = true;
        _player.TakeDamage(Damage, GlobalPosition);
    }

    /// <summary>朝 dir 方向 HopProbeDistance 处往下探，有地面返回 true，悬空（坑）返回 false。</summary>
    private bool GroundAhead(int dir)
    {
        var space = GetWorld2D().DirectSpaceState;
        float x = GlobalPosition.X + dir * HopProbeDistance;
        var from = new Vector2(x, GlobalPosition.Y);
        var to = new Vector2(x, GlobalPosition.Y + HopProbeDepth);
        // layer 1 = 地形；怪物本体在 layer 4，不会打到自己
        var q = PhysicsRayQueryParameters2D.Create(from, to, 1);
        return space.IntersectRay(q).Count > 0;
    }

    // ─────────────────────────── 动画
    protected override void UpdateAnimation(float dt)
    {
        if (_sprite == null) return;

        if (_dead)
        {
            if (_sprite.Frame < DeathStart) { _sprite.Frame = DeathStart; _animTimer = 0f; return; }
            _animTimer += dt;
            if (_animTimer < DeathAnimInterval) return;
            _animTimer = 0f;
            if (_sprite.Frame < DeathEnd) _sprite.Frame++;
            return;
        }

        _sprite.FlipH = false;   // 贴图自带左右两套，不需要镜像

        switch (_st)
        {
            case St.Hop:
                UpdateHopFrame();
                break;

            case St.Charge:
                // 蓄力：前半段后仰，后半段压紧，给玩家一个「要来了」的读条感
                _sprite.Frame = LungeStartFor(0) + (_timer < LungeChargeTime * 0.5f ? 1 : 0);
                break;

            case St.Lunge:
                // 上升时张嘴（拉伸最狠那帧），下落时换成回收帧，看着像扑完要咬住
                _sprite.Frame = LungeStartFor(Velocity.Y < 0f ? 2 : 3);
                break;

            case St.Recover:
                _sprite.Frame = LungeStartFor(3);
                break;

            default:
                if (_landHold > 0f)
                    _sprite.Frame = HopStartFor() + 4;   // 落地压扁
                else
                    PlayIdleBreath(dt);
                break;
        }
    }

    /// <summary>蹦跳帧按物理状态选，不循环播——循环播放会像在扑腾翅膀。</summary>
    private void UpdateHopFrame()
    {
        int start = HopStartFor();
        if (Velocity.Y < -220f)      _sprite.Frame = start + 1;   // 刚起跳：拉长
        else if (Velocity.Y < 0f)    _sprite.Frame = start + 2;   // 接近顶点
        else                         _sprite.Frame = start + 3;   // 下落
    }

    private void PlayIdleBreath(float dt)
    {
        int start = Direction > 0 ? IdleRightStart : IdleLeftStart;
        _animTimer += dt;
        if (_animTimer < IdleAnimInterval) return;
        _animTimer = 0f;

        int f = _sprite.Frame;
        if (f < start || f >= start + 4) f = start;
        else f = start + (f - start + 1) % 4;
        _sprite.Frame = f;
    }

    private int HopStartFor() => Direction > 0 ? HopRightStart : HopLeftStart;

    /// <summary>飞扑帧段按「往哪扑」选：往右用朝右的图，往左用朝左的图。</summary>
    private int LungeStartFor(int offset)
    {
        int start = _lungeDir.X >= 0f ? LungeRightStart : LungeLeftStart;
        return start + offset;
    }

    // ─────────────────────────── 死亡
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
        _landHold = 0f;
        _animTimer = 0f;
        if (_sprite != null) _sprite.Frame = IdleRightStart;
    }
}
