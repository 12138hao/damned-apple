using Godot;

/// <summary>
/// 海盗苹果（PirateApple）：不死海盗苹果的加强版，多一把海盗弯刀。
///
/// 设定：比普通海盗苹果怪**大 30%**，一手握着锈迹斑斑的海盗弯刀，另一手是浮着的木质骷髅手。
/// 攻击是一套**两段连招**，每段都先往前突进一小段再劈：
///   ① 下砍：举刀过顶 → 突进 50 像素 → 抡下来（30 伤害）
///   ② 横砍：刀收到身后 → 再突进 50 像素 → 横扫（30 伤害）
/// 每段独立判定，判定范围是身前 150 像素、纵向 ±70 的长方形。
///
/// 美术：三层 Sprite2D —— 本体 / 握刀的手 / 空手。
///   · 刀绕**握把**旋转（不是绕刀身中心，否则刀会像风车一样乱转）
///   · 三层一起跟着 Visual 的 Scale 镜像，所以朝左时整套动作自动反相
///   · 剑气用 SwingTrail（和玩家挥武器同一套），角度直接由刀的角度推出来，
///     所以「刀在哪、剑气就在哪」永远不会对不上
/// </summary>
public partial class PirateApple : Enemy
{
    private enum State { Patrol, Notice, Windup, Dash, Strike, Recover, Cooldown }

    // ── 美术基准（贴图原始像素，1 单位 = 1 像素）──────────────
    private const float BodyH = 160f;                                // 本体贴图高
    private static readonly Vector2 BodyOff = new(0f, -80f);         // 本体中心相对脚底（= 贴图高的一半）
    private static readonly Vector2 SwordGrip = new(62f, -38f);      // 握刀的手相对脚底（放 +x：镜像后才会落在朝向那一侧）
    private static readonly Vector2 SwordPivot = new(27f, 126f);     // 刀贴图里「握把」的像素坐标
    private static readonly Vector2 FreeHandPos = new(-62f, -38f);   // 空手相对脚底（和刀相反的一侧）
    private const float FreeHandScale = 1.3f;                        // 空手沿用老怪的贴图，放大 30%

    // 刀的角度（度，0 = 刀尖朝正上，正数 = 顺时针 / 朝前）
    // 刀尖方向 = (sin θ, -cos θ)：θ=0 朝上、θ=90 朝前、θ=180 朝下
    private const float RestDeg = 0f;        // 站着：刀竖直
    private const float Windup1Deg = -25f;   // 下砍蓄力：刀举到头顶偏后（再往后就压到果肉上了）
    private const float Strike1Deg = 110f;   // 下砍收势：刀抡到身前偏下，刀尖刚离地
    private const float Windup2Deg = -58f;   // 横砍蓄力：刀斜收到身后上方
    private const float Strike2Deg = 100f;   // 横砍收势：刀扫到身前近水平

    [ExportGroup("美术")]
    [Export] public float ArtScale = 0.55f;      // 和普通海盗苹果怪一样，贴图大 30% 就等于整只大 30%

    [ExportGroup("行为")]
    [Export] public float DetectRange = 460f;    // 多远发现玩家
    [Export] public float NoticeTime = 0.35f;    // 「发现」的压身动作
    [Export] public float CooldownTime = 1.10f;  // 一套连招打完歇多久
    [Export] public float ChaseSpeed = 95f;      // 发现玩家但还没进刀程时，走过去的速度

    [ExportGroup("第一段：下砍")]
    [Export] public float Windup1Time = 0.20f;   // 举刀时间（越短越难躲）
    [Export] public float Strike1Time = 0.20f;   // 抡下来的时间

    [ExportGroup("第二段：横砍")]
    [Export] public float Windup2Time = 0.20f;
    [Export] public float Strike2Time = 0.24f;

    [ExportGroup("突进")]
    [Export] public float DashDistance = 50f;    // 每次劈砍前向前挪多少像素
    [Export] public float DashTime = 0.14f;      // 挪完用多久（越短越像突进）

    [ExportGroup("伤害")]
    [Export] public int SlashDamage = 30;        // 每段劈砍的伤害
    // SlashRange 现在只用来决定 AI 什么时候起手 / 突进到多近，不再当命中框。
    // 真正的命中判定是「玩家身体中心到刀身这条线段的距离」，见 BladeTouchesTarget()。
    [Export] public float SlashRange = 150f;
    [Export] public float BladeHitRadius = 10f;  // 刀身两侧各留多少容差（刀贴图半宽约 8）
    [Export] public float TargetHitRadius = 16f; // 玩家身体半径

    [ExportGroup("收招")]
    [Export] public float RecoverTime = 0.45f;

    [ExportGroup("音效")]
    // 和苹果怪一样用自己的一套名字，别借玩家的 swing/hurt
    [Export] public string WindupSfx = "apple_charge";
    [Export] public string SlashSfx = "apple_swing";

    // ── 运行时 ────────────────────────────────────────────────
    private State _state = State.Patrol;
    private float _stateTimer;
    private float _cooldown;
    private int _face = 1;
    private int _stage;              // 0 = 下砍，1 = 横砍
    private bool _struck;            // 这一段的伤害判定放过了没有
    private float _dashStartX;
    private float _poseTime;
    private bool _chasing;           // 发现了玩家但还没进刀程，正在走过去

    private Sprite2D _body;
    private Sprite2D _freeHand;
    private Node2D _swordArm;
    private Sprite2D _sword;
    private SwingTrail _trail;
    private Box _target;

    public override void _Ready()
    {
        base._Ready();

        if (HurtSfx == "enemy_hurt") HurtSfx = "apple_hurt";
        if (DeathSfx == "enemy_hurt") DeathSfx = "apple_die";

        _body = GetNodeOrNull<Sprite2D>("Visual/Sprite2D");
        _freeHand = GetNodeOrNull<Sprite2D>("Visual/FreeHand");
        _swordArm = GetNodeOrNull<Node2D>("Visual/SwordArm");
        _sword = GetNodeOrNull<Sprite2D>("Visual/SwordArm/Sword");
        _trail = GetNodeOrNull<SwingTrail>("Visual/SlashTrail");

        SetupSwordPivot();
        if (_freeHand != null) _freeHand.Scale = Vector2.One * FreeHandScale;

        _target = GetTree().Root.FindChild("box", true, false) as Box;
        if (_target == null) GD.PushWarning("[PirateApple] 没找到玩家 box");

        ApplyPose(0f);
    }

    /// <summary>
    /// 把刀的旋转轴挪到「握把」上。
    /// Sprite2D 是以 offset 为中心画的，所以要让贴图里的 SwordPivot 落在节点原点，
    /// offset 得填 (半宽 - pivotX, 半高 - pivotY)。这样转 SwordArm 就是绕握把转。
    ///
    /// 注意：贴图必须 flip_h —— 原始素材是「刀在身体左侧、护手朝右、护指弯条朝外」画的，
    /// 直接摆到 +x 侧会变成护手朝外、弯条朝身体，看着就是「拿反了」。
    /// 翻转后 offset.x 要跟着取反，否则握把会偏 2 像素。
    /// </summary>
    private void SetupSwordPivot()
    {
        if (_sword == null || _sword.Texture == null) return;
        Vector2 size = _sword.Texture.GetSize();
        float sign = _sword.FlipH ? -1f : 1f;
        _sword.Offset = new Vector2(sign * (size.X * 0.5f - SwordPivot.X),
                                    size.Y * 0.5f - SwordPivot.Y);
    }

    // ---------------------------------------------------------- 状态机

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (!_dead) TickState(dt);
        base._PhysicsProcess(delta);
    }

    private float StageWindupTime => _stage == 0 ? Windup1Time : Windup2Time;
    private float StageStrikeTime => _stage == 0 ? Strike1Time : Strike2Time;
    private float StageWindupDeg => _stage == 0 ? Windup1Deg : Windup2Deg;
    private float StageStrikeDeg => _stage == 0 ? Strike1Deg : Strike2Deg;

    /// <summary>蓄力时手被拉到哪（相对握把的额外位移，单位是美术像素）。下砍往上抬，横砍往后拉。</summary>
    private static Vector2 ArmWindup(int stage)
        => stage == 0 ? new Vector2(-6f, -26f) : new Vector2(-16f, -12f);

    /// <summary>砍出去时手甩到哪。下砍往下砸，横砍往前送。</summary>
    private static Vector2 ArmStrike(int stage)
        => stage == 0 ? new Vector2(16f, 16f) : new Vector2(20f, 4f);

    private void TickState(float dt)
    {
        _poseTime += dt;
        _stateTimer += dt;
        _cooldown = Mathf.Max(0f, _cooldown - dt);

        switch (_state)
        {
            case State.Patrol:
                // 发现玩家：先走过去，走到「突进一次就够得着」的距离才起手，
                // 否则会站在 460 外空挥第一刀（突进只有 50，砍不到 100 外的人）
                _chasing = false;
                if (_cooldown <= 0f && PlayerInRange())
                {
                    // 起手距离要按「刀真正够得着」来定，不能用 SlashRange（那是给 AI 用的名义值）。
                    // 刀身从握把往外约 103，起手 105 再突进一下就正好进刀程。
                    if (AttackGapX() <= SlashRange * 0.7f) Enter(State.Notice);
                    else _chasing = true;
                }
                break;

            case State.Notice:
                FaceTarget();
                if (_stateTimer >= NoticeTime) { _stage = 0; Enter(State.Windup); }
                break;

            case State.Windup:
                FaceTarget();                       // 蓄力期间还能微调朝向
                if (_stateTimer >= StageWindupTime)
                {
                    _dashStartX = GlobalPosition.X;
                    Enter(State.Dash);
                }
                break;

            case State.Dash:
                // 走够距离就开砍；贴到玩家面前也停（再冲就冲过头，第二段会砍到空气）；
                // 万一被墙挡住，超时也照样往下走，不能卡死
                if (Mathf.Abs(GlobalPosition.X - _dashStartX) >= DashDistance
                    || _stateTimer >= DashTime * 2.5f
                    || CloseEnoughToStop())
                    Enter(State.Strike);
                break;

            case State.Strike:
                // 命中判定跟着刀走：每帧量「玩家身体中心到刀身线段」的距离，
                // 刀真的扫到才扣血。挥空就一次都不扣（声音在起手时已经放过了）。
                if (!_struck && BladeTouchesTarget())
                {
                    _struck = true;
                    ApplySlashDamage();
                }
                if (_stateTimer >= StageStrikeTime)
                {
                    if (_stage == 0) { _stage = 1; Enter(State.Windup); }
                    else Enter(State.Recover);
                }
                break;

            case State.Recover:
                if (_stateTimer >= RecoverTime)
                {
                    _cooldown = CooldownTime;
                    Enter(State.Cooldown);
                }
                break;

            case State.Cooldown:
                if (_stateTimer >= CooldownTime) Enter(State.Patrol);
                break;
        }
    }

    private void Enter(State s)
    {
        _state = s;
        _stateTimer = 0f;

        if (s == State.Windup)
        {
            _struck = false;
            AudioManager.Instance?.PlaySfx(WindupSfx, 0.55f, 0.9f + _stage * 0.15f);
        }
        if (s == State.Strike)
        {
            // 挥砍的破风声放在「起手」这一刻，而不是命中那一刻 ——
            // 不然挥空了就一点声音都没有，看起来像卡住了。
            AudioManager.Instance?.PlaySfx(SlashSfx, 0.85f, 0.94f + GD.Randf() * 0.12f);
        }
        if (s == State.Recover)
        {
            _trail?.Clear();
        }
    }

    private bool PlayerInRange()
    {
        if (_target == null || !IsInstanceValid(_target) || !_target.IsAlive) return false;
        if (Mathf.Abs(_target.GlobalPosition.X - GlobalPosition.X) > DetectRange) return false;
        if (Mathf.Abs(_target.GlobalPosition.Y - GlobalPosition.Y) > DetectRange) return false;
        return true;
    }

    private void FaceTarget()
    {
        if (_target == null || !IsInstanceValid(_target)) return;
        _face = _target.GlobalPosition.X >= GlobalPosition.X ? 1 : -1;
    }

    /// <summary>
    /// 突进够不够近了。玩家本来就贴着的时候必须停 —— 不然一下冲过去，
    /// 第二段横砍就变成「人已经在背后」，砍空气。
    /// </summary>
    private bool CloseEnoughToStop()
        => AttackGapX() <= SlashRange * 0.5f;

    /// <summary>和玩家的横向距离。没有目标时返回一个很大的数。</summary>
    private float AttackGapX()
    {
        if (_target == null || !IsInstanceValid(_target)) return float.MaxValue;
        return Mathf.Abs(_target.GlobalPosition.X - GlobalPosition.X);
    }

    // ---------------------------------------------------------- 伤害

    /// <summary>刀尖离握把多远（贴图像素）。SwordPivot.Y 就是轴心到刀尖的长度。</summary>
    private float BladeLength => SwordPivot.Y;

    /// <summary>
    /// 刀现在到底有没有砍到玩家：把刀身当成一条「握把 → 刀尖」的线段，
    /// 量玩家身体中心到这条线的距离。
    ///
    /// 这么判的原因：以前用「身前 150 × 纵向 ±70」的矩形，比刀长出一大截，
    /// 而且判定时机是挥砍进行到 45%（那时刀还举在脑后，因为挥砍是 k² 缓动），
    /// 于是出现「刀明明没碰到人，血却掉了」。
    /// 现在判定完全跟着刀走：刀没扫到就一次都不扣。
    /// </summary>
    private bool BladeTouchesTarget()
    {
        if (_target == null || !IsInstanceValid(_target) || !_target.IsAlive) return false;
        if (_swordArm == null) return false;

        Vector2 grip = _swordArm.GlobalPosition;
        // SwordArm 的局部 -Y 就是刀尖方向；GlobalTransform.Y 已经带上了 Visual 的镜像和缩放
        Vector2 dir = -_swordArm.GlobalTransform.Y.Normalized();

        // 刀还没转到朝向那一侧（还举在脑后）就别判，
        // 否则抬手那几帧刀竖在头顶也会把人打了。
        if (dir.Dot(new Vector2(_face, 0f)) < 0.3f) return false;

        Vector2 tip = grip + dir * (BladeLength * ArtScale);

        return DistToSegment(_target.BodyCenter, grip, tip) <= BladeHitRadius + TargetHitRadius;
    }

    /// <summary>点到线段的距离。</summary>
    private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.LengthSquared();
        if (len2 < 0.0001f) return p.DistanceTo(a);
        float t = Mathf.Clamp((p - a).Dot(ab) / len2, 0f, 1f);
        return p.DistanceTo(a + ab * t);
    }

    /// <summary>真的砍中了，扣血。</summary>
    private void ApplySlashDamage()
    {
        if (_target == null || !IsInstanceValid(_target) || !_target.IsAlive) return;

        // 玩家受击后有 1 秒无敌，而两段间隔只有 0.6 秒左右；再加上贴身时身体接触的 10 点
        // 会先吃掉无敌，劈砍的 30 点就全白打了。所以每段劈砍都先把无敌清掉，保证 30 一定落地。
        _target.ResetHurtCooldown();

        _target.TakeDamage(SlashDamage, GlobalPosition);
    }

    // ---------------------------------------------------------- 移动

    protected override float ComputeHorizontalVelocity(float dt)
    {
        // 突进：匀速冲一段，距离由 TickState 里的 _dashStartX 控制
        if (_state == State.Dash)
        {
            if (CloseEnoughToStop()) return 0f;
            return _face * (DashDistance / Mathf.Max(0.01f, DashTime));
        }

        // 只有巡逻和 CD 期间会走，出招全程站定
        if (_state == State.Patrol)
        {
            // 已经看见玩家但还没进刀程 → 直着走过去（不再左右晃）
            if (_chasing)
            {
                FaceTarget();
                return _face * ChaseSpeed;
            }
            return base.ComputeHorizontalVelocity(dt);
        }

        if (_state == State.Cooldown)
            return base.ComputeHorizontalVelocity(dt);

        return 0f;
    }

    // ---------------------------------------------------------- 动画

    protected override void UpdateAnimation(float dt)
    {
        if (_dead) { base.UpdateAnimation(dt); return; }
        ApplyPose(dt);
    }

    private void ApplyPose(float dt)
    {
        if (_body == null) return;

        var visual = _body.GetParent<Node2D>();
        if (visual != null) visual.Scale = new Vector2(_face * ArtScale, ArtScale);

        // 默认姿势
        Vector2 bs = Vector2.One;
        float br = 0f;
        Vector2 bp = Vector2.Zero;
        float swordDeg = RestDeg;
        Vector2 armOff = Vector2.Zero;
        float handOff = 0f;
        float trailFrom = 0f, trailTo = 0f, trailAlpha = 0f;

        float t = _poseTime;

        switch (_state)
        {
            case State.Patrol:
            case State.Cooldown:
                if (Mathf.Abs(Velocity.X) > 1f)
                {
                    // 走：弹跳 + 摇摆，刀跟着身体晃
                    float ph = t * 8f;
                    bp = new Vector2(0f, -Mathf.Abs(Mathf.Sin(ph)) * 3f);
                    br = Mathf.Sin(ph) * 3f;
                    bs = new Vector2(1f + 0.02f * Mathf.Sin(ph * 2f), 1f - 0.02f * Mathf.Sin(ph * 2f));
                    swordDeg = Mathf.Sin(ph) * 5f;
                    handOff = Mathf.Sin(ph + Mathf.Pi) * 3f;
                }
                else
                {
                    // 站着呼吸：刀跟着微微起伏
                    float s = Mathf.Sin(t * 2.4f);
                    bs = new Vector2(1f - 0.016f * s, 1f + 0.024f * s);
                    bp = new Vector2(0f, -s * 1.2f);
                    swordDeg = s * 1.6f;
                    handOff = -s * 1.5f;
                }
                break;

            case State.Notice:
            {
                // 发现玩家：先压扁再弹起，刀顺势抬一点
                float k = Mathf.Clamp(_stateTimer / NoticeTime, 0f, 1f);
                float s = k < 0.4f ? Mathf.Lerp(0f, 1f, k / 0.4f)
                                   : Mathf.Lerp(1f, 0f, (k - 0.4f) / 0.6f);
                bs = new Vector2(1f + 0.14f * s, 1f - 0.11f * s);
                bp = new Vector2(0f, 2f * s);
                br = -3f * s;
                swordDeg = -8f * s;
                break;
            }

            case State.Windup:
            {
                // 举刀：从上一段的收势位置甩到蓄力位置，末尾加点抖（憋劲）
                float k = Mathf.Clamp(_stateTimer / Mathf.Max(0.01f, StageWindupTime), 0f, 1f);
                float e = k * k * (3f - 2f * k);
                float from = _stage == 0 ? RestDeg : Strike1Deg;
                swordDeg = Mathf.Lerp(from, StageWindupDeg, e);

                // 身体后仰蓄力，越到后面抖得越厉害
                float shake = Mathf.Sin(t * 42f) * 1.6f * k;
                br = Mathf.Lerp(0f, _stage == 0 ? -7f : 9f, e);
                bp = new Vector2(shake + Mathf.Lerp(0f, -3f, e), Mathf.Lerp(0f, -2f, e));
                bs = new Vector2(1f + 0.05f * e, 1f - 0.05f * e);
                handOff = Mathf.Lerp(0f, 6f, e);
                armOff = ArmWindup(_stage) * e;
                break;
            }

            case State.Dash:
            {
                // 突进：身体前倾拉长，刀保持蓄力角度
                float k = Mathf.Clamp(_stateTimer / Mathf.Max(0.01f, DashTime), 0f, 1f);
                swordDeg = StageWindupDeg;
                br = Mathf.Lerp(_stage == 0 ? -7f : 9f, _stage == 0 ? -12f : 4f, k);
                bs = new Vector2(1f + 0.08f * k, 1f - 0.06f * k);
                bp = new Vector2(Mathf.Lerp(-3f, 4f, k), 0f);
                handOff = 6f;
                armOff = ArmWindup(_stage);
                break;
            }

            case State.Strike:
            {
                // 挥砍：k*k 让刀越抡越快，像真的用上力
                float k = Mathf.Clamp(_stateTimer / Mathf.Max(0.01f, StageStrikeTime), 0f, 1f);
                float e = k * k;
                swordDeg = Mathf.Lerp(StageWindupDeg, StageStrikeDeg, e);

                // 身体跟着甩：下砍往前压，横砍从后往前拧
                br = _stage == 0
                    ? Mathf.Lerp(-12f, 10f, e)
                    : Mathf.Lerp(4f, -14f, Mathf.Sin(k * Mathf.Pi) * 0.8f + 0.2f * k);
                bp = new Vector2(Mathf.Lerp(4f, 9f, e), 0f);
                bs = new Vector2(1f + 0.06f * (1f - e), 1f - 0.05f * (1f - e));
                handOff = Mathf.Lerp(6f, -8f, e);
                armOff = ArmWindup(_stage).Lerp(ArmStrike(_stage), e);

                // 剑气：起止角度直接由刀的角度推出来，永远跟刀对齐
                trailFrom = -90f + StageWindupDeg;
                trailTo = -90f + swordDeg;
                trailAlpha = 1f - 0.45f * k;
                break;
            }

            case State.Recover:
            {
                // 收招：刀慢慢放回竖直，身体回弹
                float k = Mathf.Clamp(_stateTimer / Mathf.Max(0.01f, RecoverTime), 0f, 1f);
                float e = 1f - (1f - k) * (1f - k);
                swordDeg = Mathf.Lerp(Strike2Deg, RestDeg, e);
                br = Mathf.Lerp(-14f, 0f, e);
                bp = new Vector2(Mathf.Lerp(9f, 0f, e), 0f);
                bs = new Vector2(1f + 0.03f * (1f - e), 1f - 0.02f * (1f - e));
                handOff = Mathf.Lerp(-8f, 0f, e);
                armOff = ArmStrike(1).Lerp(Vector2.Zero, e);
                break;
            }
        }

        // 应用到节点
        _body.Position = BodyOff + bp;
        _body.Scale = bs;
        _body.Rotation = Mathf.DegToRad(br);

        if (_swordArm != null)
        {
            _swordArm.Position = SwordGrip + armOff;
            _swordArm.Rotation = Mathf.DegToRad(swordDeg);
        }
        if (_freeHand != null)
            _freeHand.Position = FreeHandPos + new Vector2(handOff * 0.6f, handOff * 0.4f);

        // 受击闪红要连刀和手一起闪（父类只改 _sprite 的 Modulate）
        if (_sword != null) _sword.Modulate = _body.Modulate;
        if (_freeHand != null) _freeHand.Modulate = _body.Modulate;

        // 剑气
        if (_trail != null)
        {
            if (trailAlpha > 0.02f) _trail.Show(trailFrom, trailTo, trailAlpha);
            else _trail.Clear();
        }
    }

    // ---------------------------------------------------------- 死亡 / 重生

    protected override void OnDeath()
    {
        base.OnDeath();
        _trail?.Clear();
        _state = State.Patrol;
    }

    public override void Respawn()
    {
        base.Respawn();
        _state = State.Patrol;
        _stateTimer = 0f;
        _cooldown = 0f;
        _stage = 0;
        _struck = false;
        _face = 1;
        _trail?.Clear();
        ApplyPose(0f);
    }

    /// <summary>给探针看的内部状态（GDScript 读不到 C# 私有字段）。</summary>
    public string DebugPirate()
        => $"state={_state} t={_stateTimer:F2} stage={_stage} face={_face}"
         + $" struck={_struck} hp={Hp} dead={_dead} vel={Velocity.X:F0}";
}
