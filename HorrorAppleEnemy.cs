using Godot;

/// <summary>
/// 地下洞穴里的跳跃苹果怪（horror = 恐怖，指它出没的第二关地下；apple = 本体）。
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
public partial class HorrorAppleEnemy : Enemy
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
    [Export] public float HopDistance = 155f;    // 每一跳的**水平距离**（固定，px）——速度由它反算
    [Export] public float HopHeight = 81f;       // 跳跃最高离地多少像素（按重力自动算初速度）
    [Export] public float LandHoldTime = 0.12f;  // 落地压扁保持多久
    [Export] public float HopProbeDistance = 150f; // 跳跃前探测坑的距离（沿朝向前方）
    [Export] public float HopProbeDepth = 180f;    // 探测向下多深算「有地面」

    // ─────────────────────────── 侦测与飞扑
    [ExportGroup("侦测与飞扑")]
    [Export] public float DetectRangeX = 300f;   // 水平多近能发现玩家
    [Export] public float DetectRangeY = 140f;   // 垂直高度差容忍
    [Export] public float LungeChargeTime = 0.5f;  // 蓄力预警时长——玩家的反应窗口

    [Export] public float LungeDistance = 400f;  // 每一扑的**水平距离**（固定，px）——不再按玩家远近伸缩
    [Export] public float LungeDuration = 0.38f; // 整段飞扑用多久（固定，秒）——决定扑得多快
    [Export] public float LungeHeight = 24f;     // 平飞段的离地高度。低于 16 会咬到蹲下的，高于 32 连站着的都够不着
    [Export] public float LungeRiseFraction = 0.15f; // 起跳、落地各占整段的比例，中间是平飞

    [Export] public float RecoverTime = 0.5f;    // 扑完喘气
    [Export] public float LungeCooldown = 1.1f;  // 两次飞扑之间至少隔多久

    private enum St { Idle, Hop, Charge, Lunge, Recover }
    private St _st = St.Idle;

    private float _timer;
    private float _lungeCd;
    private float _landHold;
    private float _lungeT;         // 本次飞扑已经飞了多久
    private float _lungeStartY;    // 起跳时的 Y，整条弧线都以它为基准
    private float _lungeVx;        // 本次扑击的水平速度（由固定距离 ÷ 固定时长算）
    private float _hopVx;          // 本次蹦跳的水平速度（由固定距离反算）
    private bool _lungeHitDone;    // 本次飞扑是否已经结算过伤害
    private Box _player;
    private Vector2 _lungeDir = Vector2.Right;
    private bool _landedOnce;
    private Vector2 _prevPos;      // 上一物理帧的位置，扫掠判定用

    // 本体碰撞盒的尺寸，命中判定直接拿它当真身（不另开一个"感觉差不多"的判定框）
    private Vector2 _bodyHalf = new Vector2(27f, 34f);
    private float _bodyCenterY = 11f;

    public override void _Ready()
    {
        base._Ready();
        _player = GetTree().Root.FindChild("box", true, false) as Box;
        _timer = HopInterval;
        _prevPos = GlobalPosition;

        // 命中判定用本体碰撞盒的真实尺寸，改场景里的形状不用回来改代码
        var body = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (body?.Shape is RectangleShape2D rect)
        {
            _bodyHalf = rect.Size * 0.5f;
            _bodyCenterY = body.Position.Y;
        }

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

        // 飞扑的轨迹自己算（见 UpdateLungeArc），所以要在父类动手之前先把竖直速度铺好
        if (_st == St.Lunge) UpdateLungeArc(dt);

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
                    // 起跳：高度轨迹交给 UpdateLungeArc，这里只定水平速度和起点
                    _st = St.Lunge;
                    _lungeHitDone = false;
                    _lungeT = 0f;
                    _lungeStartY = GlobalPosition.Y;
                    _lungeVx = LungeDistance / Mathf.Max(0.05f, LungeDuration);
                    Velocity = new Vector2(_lungeDir.X * _lungeVx, 0f);
                }
                break;

            case St.Lunge:
                TryLungeHit();
                // 整段飞完就收，不再等「落地」——弧线是算好的，落点是确定的
                if (_lungeT >= LungeDuration)
                {
                    _st = St.Recover;
                    _timer = RecoverTime;
                    Velocity = new Vector2(Velocity.X * 0.3f, 0f);
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
            case St.Hop:   return Direction * _hopVx;
            case St.Lunge: return _lungeDir.X * _lungeVx;
            default:       return 0f;      // 蓄力时站定，给玩家看清信号
        }
    }

    /// <summary>
    /// 飞扑的弧线：不是自由落体，是「快速抬起 → 平飞 → 落下」的梯形。
    ///
    /// 为什么要自己算轨迹、不交给重力：
    ///   · 想「咬到站着的、又让蹲下的躲开」，怪的身体必须待在离地 16~32px 这条窄带里
    ///     （16 = 蹲下后的身高，32 = 站着的身高）。
    ///   · 抛物线穿过这条窄带只有一瞬间，蹲下的闪避窗口会窄到没法玩（实测只有 4 成）。
    ///   · 梯形能在这条带里「悬」住七成路程，窗口才够到 6 成。
    ///
    /// 做法：按进度算出这一帧该在的高度，再反推需要的竖直速度。
    /// 父类稍后还会再叠一次重力，所以这里先把那一份扣掉，否则会越飘越高。
    /// </summary>
    private void UpdateLungeArc(float dt)
    {
        _lungeT += dt;
        float p = Mathf.Clamp(_lungeT / Mathf.Max(0.05f, LungeDuration), 0f, 1f);
        float wantY = _lungeStartY - LungeHeight * ArcShape(p);
        // 限一下修正速度：飞扑途中被打退时，位置会被推歪，不限的话下一帧会「啪」地弹回来
        float vy = Mathf.Clamp((wantY - GlobalPosition.Y) / Mathf.Max(dt, 0.0001f) - Gravity * dt,
                               -1800f, 1800f);
        Velocity = new Vector2(_lungeDir.X * _lungeVx, vy);
    }

    /// <summary>梯形的高度比例：0 = 贴地，1 = 到平飞高度。起落用 smoothstep 抹圆，别像折线。</summary>
    private float ArcShape(float p)
    {
        float r = Mathf.Clamp(LungeRiseFraction, 0.01f, 0.49f);
        if (p < r) return SmoothStep(p / r);
        if (p > 1f - r) return SmoothStep((1f - p) / r);
        return 1f;
    }

    private static float SmoothStep(float x)
    {
        x = Mathf.Clamp(x, 0f, 1f);
        return x * x * (3f - 2f * x);
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

        _st = St.Charge;
        _timer = LungeChargeTime;
        _lungeCd = LungeChargeTime + LungeDuration + RecoverTime + LungeCooldown;
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
        // 由「跳跃高度」反算初速度：v = √(2gh)；再由「固定距离」反算水平速度
        float hopImpulse = Mathf.Sqrt(2f * Gravity * Mathf.Max(1f, HopHeight));
        float hopAir = 2f * hopImpulse / Mathf.Max(1f, Gravity);
        _hopVx = HopDistance / Mathf.Max(0.05f, hopAir);
        Velocity = new Vector2(Direction * _hopVx, -hopImpulse);
    }

    /// <summary>
    /// 飞扑命中判定：拿怪物**身体的真实占位**（54×68 那个碰撞盒）沿这一帧走过的轨迹扫一遍，
    /// 和玩家**当前的占位**求交。
    ///
    /// 为什么不用「一个固定的大判定框」：
    ///   · 飞扑是抛物线，怪在半空中是真的从玩家头顶飞过去的。
    ///     用一个固定高度的判定框，蹲下的玩家照样被咬到——这就是「没碰到也扣血」。
    ///   · 只看这一帧的矩形又会漏判：1000 px/s 时每帧走 18px，可能整帧跨过玩家。
    /// 所以沿轨迹采样几个点、每点都用贴身的盒子去撞，两个问题一起解决。
    ///
    /// 判定宽严完全由「碰撞盒尺寸 + 玩家碰撞盒尺寸」决定，和弧线高度配合出这个手感：
    ///   弧线最高 25px 时——站着必中（25 &lt; 32），蹲下只在路径两头 20% 会中，中间 60% 躲得掉。
    /// </summary>
    private void TryLungeHit()
    {
        if (_lungeHitDone) return;
        if (_player == null || !IsInstanceValid(_player)) return;

        Rect2 player = _player.BodyRect();

        const int Steps = 6;   // 一帧最多走 18px，切 6 段约 3px 一个采样点，够密
        for (int i = 0; i <= Steps; i++)
        {
            Vector2 p = _prevPos.Lerp(GlobalPosition, i / (float)Steps);
            if (!BodyRectAt(p).Intersects(player)) continue;

            _lungeHitDone = true;
            _player.TakeDamage(Damage, GlobalPosition);
            return;
        }
    }

    /// <summary>怪物身体（碰撞盒）在节点位于 nodePos 时覆盖的世界矩形。</summary>
    private Rect2 BodyRectAt(Vector2 nodePos)
    {
        Vector2 center = nodePos + new Vector2(0f, _bodyCenterY);
        return new Rect2(center - _bodyHalf, _bodyHalf * 2f);
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
                // 前半段用「腾空张嘴」那帧，后半段换成「咬下去」那帧。
                // 不能看 Velocity.Y 判断升降——平飞段的竖直速度只是为了抵消重力，一直是负的
                _sprite.Frame = LungeStartFor(_lungeT < LungeDuration * 0.5f ? 2 : 3);
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
