using Godot;

/// <summary>
/// 不死海盗苹果（UndeadPirateApple）。
///
/// 设定：雨林细菌把苹果果肉腐蚀得千疮百孔，它戴着一顶海盗帽、挂着两只木质骷髅手。
/// 看到玩家 → 双手转向瞄准 → 蓄力 1 秒（期间锁定方向）→ 把骷髅手射出去。
/// 打中就把玩家拖回面前打投技（双手抓住、嘴吐紫烟、扣 20 血），然后甩出去进 CD。
///
/// 美术：一张静态图被拆成「身体 / 左手 / 右手」三个 Sprite2D，
/// 所有动作都是对这三层做缩放/旋转/位移算出来的（没有逐帧图）。
/// 手绕**手腕**旋转，不是绕手心，否则手会甩出去像断肢。
/// </summary>
public partial class UndeadPirateApple : Enemy
{
    private enum State { Patrol, Notice, Aim, Charge, Firing, WaitHand, Throw, Cooldown }

    // ── 美术基准（贴图原始像素，1 单位 = 1 像素）──────────────
    private const float HandHalf = 20f;                              // 手中心相对手腕的 y 偏移
    private static readonly Vector2 BodyOff = new(0f, -61.5f);       // 身体中心相对脚底
    private static readonly Vector2 WristL0 = new(-47.5f, -29f);     // 左手手腕静止位
    private static readonly Vector2 WristR0 = new(48f, -29f);        // 右手手腕静止位

    [ExportGroup("美术")]
    [Export] public float ArtScale = 0.55f;      // 整只怪的缩放（贴图 160x159 → 屏幕上约 88 高）

    [ExportGroup("行为")]
    [Export] public float DetectRange = 520f;    // 多远发现玩家
    [Export] public float NoticeTime = 0.45f;    // 「发现」动作时长
    [Export] public float AimTime = 0.30f;       // 瞄准（手跟着玩家转）时长
    [Export] public float ChargeTime = 1.0f;     // 蓄力时长（期间锁定方向）
    [Export] public float ThrowTime = 1.25f;     // 投技总时长
    [Export] public float SkillCooldown = 2.4f;  // 技能 CD

    [ExportGroup("投技")]
    [Export] public int ThrowDamage = 20;        // 投技总伤害（不走受击无敌）
    [Export] public int DamageTicks = 10;        // 分几滴扣完（20/10 = 每滴 2 血）
    [Export] public float DamageStartAt = 0.28f; // 投技进行到几成开始扣血（0~1）
    [Export] public float DamageEndAt = 0.85f;   // 投技进行到几成扣完（0~1）
    [Export] public float ThrowKnockback = 420f; // 投技结束的横向击退
    [Export] public float ThrowKnockUp = 260f;   // 投技结束的向上击飞
    [Export] public Vector2 GrabOffset = new(70f, -22f);  // 抓取点相对脚底（贴图像素，x 会按朝向翻转）

    [ExportGroup("骷髅手")]
    [Export] public float HandFlySpeed = 620f;
    [Export] public float HandMaxRange = 390f;   // 原来 780，太长了；现在只有半程
    [Export] public float HandHitRadius = 30f;

    [ExportGroup("特效")]
    [Export] public int SmokePuffCount = 16;     // 投技嘴部紫烟的团数（越多越浓，单团尺寸不变）

    [ExportGroup("音效")]
    // 苹果怪用自己的一套名字，**不要借玩家那套**（charge / swing / hurt / slam）：
    //   ① 听着就不像怪物；② 撞名之后改一个动两个。
    // 文件不存在时 PlaySfx 会静默跳过，所以现在没文件 = 没声音。
    [Export] public string ChargeSfx = "apple_charge";
    [Export] public string FireSfx = "apple_fire";
    [Export] public string GrabSfx = "apple_grab";
    [Export] public string ThrowSfx = "apple_throw";

    // ── 运行时 ────────────────────────────────────────────────
    private State _state = State.Patrol;
    private float _stateTimer;
    private float _cooldown;
    private int _face = 1;

    private Sprite2D _body;
    private Sprite2D _handL;
    private Sprite2D _handR;
    private Box _target;
    private SkeletonHand _hand;

    private Vector2 _aimDir = Vector2.Right;   // 蓄力时锁定的发射方向
    private float _aimDegL;
    private float _aimDegR;
    private float _poseTime;                   // 动画用的连续时间

    // 投技「一滴一滴」扣血
    private int _ticksDone;                    // 已经扣了几滴
    private int _damageDealt;                  // 已经扣掉的总血量
    private float _tickTimer;                  // 距离下一滴的计时

    // 特效
    private Sprite2D _aura;
    private Sprite2D[] _puffs;

    public override void _Ready()
    {
        base._Ready();

        // 父类 Enemy 的受击 / 死亡音默认是 "enemy_hurt"，那是第一关小怪在用的。
        // 苹果怪换成自己的一套；检查器里手动改过就不覆盖。
        if (HurtSfx == "enemy_hurt") HurtSfx = "apple_hurt";
        if (DeathSfx == "enemy_hurt") DeathSfx = "apple_die";

        _body = GetNodeOrNull<Sprite2D>("Visual/Sprite2D");
        _handL = GetNodeOrNull<Sprite2D>("Visual/HandL");
        _handR = GetNodeOrNull<Sprite2D>("Visual/HandR");

        _target = GetTree().Root.FindChild("box", true, false) as Box;
        if (_target == null) GD.PushWarning("[UndeadPirateApple] 没找到玩家 box");

        BuildEffects();
        _cooldown = 0f;
        ApplyPose(0f);
    }

    // ---------------------------------------------------------- 状态机

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (!_dead) TickState(dt);
        base._PhysicsProcess(delta);
    }

    private void TickState(float dt)
    {
        _poseTime += dt;
        _cooldown = Mathf.Max(0f, _cooldown - dt);
        _stateTimer += dt;

        switch (_state)
        {
            case State.Patrol:
                if (_cooldown <= 0f && PlayerInRange()) Enter(State.Notice);
                break;

            case State.Notice:
                FaceTarget();
                if (_stateTimer >= NoticeTime) Enter(State.Aim);
                break;

            case State.Aim:
                FaceTarget();
                UpdateAimAngles();                       // 瞄准期间实时跟手
                if (_stateTimer >= AimTime)
                {
                    LockAim();                           // 锁死方向，之后不再修正
                    Enter(State.Charge);
                }
                break;

            case State.Charge:
                if (_stateTimer >= ChargeTime) FireHand();
                break;

            case State.Firing:
            case State.WaitHand:
                // 等骷髅手飞出去的结果，站着不动
                break;

            case State.Throw:
                TickThrow(dt);
                break;

            case State.Cooldown:
                if (_stateTimer >= SkillCooldown) Enter(State.Patrol);
                break;
        }
    }

    private void Enter(State s)
    {
        _state = s;
        _stateTimer = 0f;

        if (s == State.Charge) AudioManager.Instance?.PlaySfx(ChargeSfx, 0.6f);
        if (s == State.Throw)
        {
            _ticksDone = 0;
            _damageDealt = 0;
            _tickTimer = 0f;
            AudioManager.Instance?.PlaySfx(GrabSfx, 0.8f);
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

    /// <summary>瞄准：两只手各自转向玩家，角度限制在 ±78° 内（再转就翻到手背了）。</summary>
    private void UpdateAimAngles()
    {
        if (_target == null || !IsInstanceValid(_target)) return;
        Vector2 aimAt = _target.BodyCenter;             // 瞄胸口，不是脚底
        _aimDegR = AimDegAt(WristR0, aimAt, 78f);
        _aimDegL = AimDegAt(WristL0, aimAt, 62f);
    }

    /// <summary>锁定方向：发射方向 = 从手心指向玩家的单位向量。</summary>
    private void LockAim()
    {
        UpdateAimAngles();
        if (_target != null && IsInstanceValid(_target))
        {
            _aimDir = (_target.BodyCenter - HandWorldPosition).Normalized();
        }
        else _aimDir = new Vector2(_face, 0f);
    }

    /// <summary>算出「手腕转向某点」需要的旋转角（度），并夹在 ±maxAbs 内。</summary>
    private float AimDegAt(Vector2 wristArt, Vector2 targetWorld, float maxAbs)
    {
        Vector2 wristWorld = ArtToWorld(wristArt);
        Vector2 d = targetWorld - wristWorld;
        d.X *= _face;                                   // 换算到美术空间（被 Visual 镜像过）
        float deg = Mathf.RadToDeg(Mathf.Atan2(d.Y, d.X)) - 90f;
        deg = Mathf.Wrap(deg, -180f, 180f);
        return Mathf.Clamp(deg, -maxAbs, maxAbs);
    }

    private void FireHand()
    {
        _state = State.Firing;
        _stateTimer = 0f;
        AudioManager.Instance?.PlaySfx(FireSfx, 0.8f);

        // 射出去的是右手 —— 手上那只右手要立刻消失，
        // 否则会看到「一只手飞走了，身上还挂着一只」。
        // 等它飞回来（或抓完人进入投技）再显示。
        if (_handR != null) _handR.Visible = false;

        var scene = GD.Load<PackedScene>("res://SkeletonHand.tscn");
        if (scene == null)
        {
            GD.PushError("[UndeadPirateApple] 缺 SkeletonHand.tscn");
            Enter(State.Cooldown);
            return;
        }

        _hand = scene.Instantiate<SkeletonHand>();
        GetParent().AddChild(_hand);
        _hand.SourceApple = this;
        _hand.Target = _target;
        _hand.FlySpeed = HandFlySpeed;
        _hand.MaxRange = HandMaxRange;
        _hand.HitRadius = HandHitRadius;
        _hand.GlobalPosition = HandWorldPosition;
        _hand.Launch(_aimDir);

        _state = State.WaitHand;
    }

    // ---------------------------------------------------------- 投技

    private void TickThrow(float dt)
    {
        if (_target == null || !IsInstanceValid(_target)) return;

        // 玩家狂按空格挣脱了：立刻收手，技能进 CD。
        // 不检查这一条的话，会继续把人钉在原地 —— 挣脱等于没挣脱。
        if (!_target.Grabbed)
        {
            OnTargetBrokeFree();
            return;
        }

        // 玩家被钉在苹果面前（GrabOffset 已经是「脚底该站的位置」，不要再额外抬高，
        // 否则人会飘在怪头顶，看起来不像被抓着）
        // GrabSource 给玩家挣脱时算「往哪边弹」用
        _target.GrabSource = GlobalPosition;
        _target.SetGrabbedPosition(GrabPoint);

        // ── 一滴一滴扣血 ──────────────────────────────────
        // 不是「到点扣 20」，而是在 DamageStartAt~DamageEndAt 这段里分 DamageTicks 次扣，
        // 每滴的血量按「剩余血量 ÷ 剩余滴数」现算，所以不管怎么配都刚好扣满 ThrowDamage。
        float startT = ThrowTime * Mathf.Clamp(DamageStartAt, 0f, 1f);
        float endT = ThrowTime * Mathf.Clamp(DamageEndAt, 0.01f, 1f);
        float interval = Mathf.Max(0.01f, (endT - startT) / Mathf.Max(1, DamageTicks));

        if (_ticksDone < DamageTicks && _stateTimer >= startT)
        {
            _tickTimer += dt;
            while (_tickTimer >= interval && _ticksDone < DamageTicks)
            {
                _tickTimer -= interval;
                DealDamageTick();
            }
        }

        if (_stateTimer >= ThrowTime)
        {
            if (IsInstanceValid(_target) && _target.Grabbed)
            {
                _target.SetGrabHurt(false);             // 松手，停止红闪
                _target.ReleaseFromGrab(GlobalPosition, ThrowKnockback, ThrowKnockUp);
                AudioManager.Instance?.PlaySfx(ThrowSfx, 0.9f);   // 甩出去的那一下
            }

            _cooldown = SkillCooldown;
            Enter(State.Cooldown);
        }
    }

    /// <summary>扣一滴血。第一滴开红闪 + 响声大一点，后面的滴答声轻一些。</summary>
    private void DealDamageTick()
    {
        _ticksDone++;

        int left = DamageTicks - _ticksDone + 1;                    // 含本次还剩几滴
        int amount = Mathf.Max(1, Mathf.CeilToInt((ThrowDamage - _damageDealt) / (float)left));
        _damageDealt += amount;

        if (!IsInstanceValid(_target)) return;

        // 第一滴开始持续红闪（投技全程，松手才停）
        if (_ticksDone == 1) _target.SetGrabHurt(true);

        _target.TakeGrabDamage(amount);

        float vol = _ticksDone == 1 ? 0.9f : 0.32f;
        float pitch = _ticksDone == 1 ? 1f : 0.92f + GD.Randf() * 0.2f;
        AudioManager.Instance?.PlaySfx(GrabSfx, vol, pitch);
    }

    /// <summary>玩家挣脱了投技：手收回身上，技能进 CD（挣脱是有代价的，它得缓一下）。</summary>
    private void OnTargetBrokeFree()
    {
        if (_handR != null) _handR.Visible = true;
        _cooldown = SkillCooldown;
        Enter(State.Cooldown);
    }

    /// <summary>骷髅手抓到人并拖回来了。</summary>
    public void OnHandArrivedWithPlayer()
    {
        _hand = null;
        if (_handR != null) _handR.Visible = true;      // 手回到身上，投技要两只手抓人
        if (_state == State.WaitHand || _state == State.Firing)
        {
            FaceTarget();
            Enter(State.Throw);
        }
    }

    /// <summary>骷髅手没抓到人，飞回来了。</summary>
    public void OnHandMissed()
    {
        _hand = null;
        if (_handR != null) _handR.Visible = true;      // 手飞回来了，重新挂上
        if (_state == State.WaitHand || _state == State.Firing)
        {
            _cooldown = SkillCooldown * 0.5f;
            Enter(State.Cooldown);
        }
    }

    // ---------------------------------------------------------- 位置换算

    private Vector2 ArtToWorld(Vector2 art) =>
        GlobalPosition + new Vector2(art.X * _face, art.Y) * ArtScale;

    /// <summary>玩家在投技里被固定的位置（世界坐标）。</summary>
    public Vector2 GrabPoint => ArtToWorld(GrabOffset);

    /// <summary>右手手心在世界里的位置（骷髅手飞回来的目标）。</summary>
    public Vector2 HandWorldPosition => ArtToWorld(WristR0 + new Vector2(0f, HandHalf));

    // ---------------------------------------------------------- 移动

    protected override float ComputeHorizontalVelocity(float dt)
    {
        // 只有巡逻和 CD 期间会走，其余状态站定
        if (_state == State.Patrol || _state == State.Cooldown)
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
        Vector2 wl = WristL0, wr = WristR0;
        float wlr = 0f, wrr = 0f;
        float aura = 0f, smoke = 0f;

        float t = _poseTime;

        switch (_state)
        {
            case State.Patrol:
            case State.Cooldown:
                if (Mathf.Abs(Velocity.X) > 1f)
                {
                    // 走：弹跳 + 摇摆 + 双手前后摆
                    float ph = t * 9f;
                    bp = new Vector2(0f, -Mathf.Abs(Mathf.Sin(ph)) * 3f);
                    br = Mathf.Sin(ph) * 3.5f;
                    bs = new Vector2(1f + 0.02f * Mathf.Sin(ph * 2f), 1f - 0.02f * Mathf.Sin(ph * 2f));
                    wlr = Mathf.Sin(ph) * 9f;
                    wrr = -Mathf.Sin(ph) * 9f;
                    wl += new Vector2(0f, -Mathf.Sin(ph) * 2f);
                    wr += new Vector2(0f, Mathf.Sin(ph) * 2f);
                }
                else
                {
                    // 站着呼吸：纵向 squash/stretch，手跟着起伏
                    float s = Mathf.Sin(t * 2.6f);
                    bs = new Vector2(1f - 0.018f * s, 1f + 0.026f * s);
                    bp = new Vector2(0f, -s * 1.2f);
                    wl += new Vector2(0f, -s * 2f);
                    wr += new Vector2(0f, -s * 2f);
                }
                break;

            case State.Notice:
            {
                // 先蹲压扁，再弹起拉长
                float k = Mathf.Clamp(_stateTimer / NoticeTime, 0f, 1f);
                float s = k < 0.35f ? Mathf.Lerp(0f, 1f, k / 0.35f)
                                    : Mathf.Lerp(1f, 0f, (k - 0.35f) / 0.65f);
                bs = new Vector2(1f + 0.16f * s, 1f - 0.12f * s);
                bp = new Vector2(0f, 2f * s);
                wl += new Vector2(-4f * s, 4f * s);
                wr += new Vector2(4f * s, 4f * s);
                wlr = 12f * s;
                wrr = -12f * s;
                break;
            }

            case State.Aim:
            case State.Charge:
            {
                float k = _state == State.Aim
                    ? Mathf.Clamp(_stateTimer / Mathf.Max(0.01f, AimTime), 0f, 1f)
                    : 1f;

                // 手转向玩家（Charge 期间角度已锁死，不再更新）
                wrr = _aimDegR;
                wlr = _aimDegL;

                if (_state == State.Charge)
                {
                    float c = Mathf.Clamp(_stateTimer / Mathf.Max(0.01f, ChargeTime), 0f, 1f);
                    aura = c;

                    // 蓄力颤抖：越到后面抖得越厉害
                    float shake = Mathf.Sin(t * 46f) * (1.2f + 2.2f * c);
                    bp = new Vector2(shake, 0f);
                    br = 2f + 3f * c + Mathf.Sin(t * 31f) * 0.8f;
                    bs = new Vector2(1f + 0.06f * c, 1f - 0.06f * c);

                    // 手往回收一点，像拉弓
                    wr += new Vector2(-12f * c, -6f * c);
                    wl += new Vector2(-3f * c, -8f * c);
                }
                else
                {
                    br = 3f * k;
                    bp = new Vector2(1f * k, 0f);
                    wr += new Vector2(2f * k, -3f * k);
                    wl += new Vector2(0f, -4f * k);
                }
                break;
            }

            case State.Firing:
            case State.WaitHand:
                // 手已经飞出去了，右手收回一点，身体前倾
                wrr = _aimDegR * 0.4f;
                wlr = _aimDegL * 0.6f;
                br = 4f;
                break;

            case State.Throw:
            {
                float k = Mathf.Clamp(_stateTimer / Mathf.Max(0.01f, ThrowTime), 0f, 1f);

                // 0~0.2 前冲抓住，0.2~0.75 举着（紫烟最浓），0.75~1 甩开回弹
                if (k < 0.2f)
                {
                    float a = k / 0.2f;
                    br = Mathf.Lerp(0f, 9f, a);
                    bp = new Vector2(Mathf.Lerp(0f, 5f, a), Mathf.Lerp(0f, -2f, a));
                    bs = new Vector2(1f - 0.06f * a, 1f + 0.06f * a);
                    wrr = Mathf.Lerp(_aimDegR, -80f, a);
                    wlr = Mathf.Lerp(_aimDegL, -50f, a);
                    smoke = a * 0.85f;
                }
                else if (k < 0.75f)
                {
                    float a = (k - 0.2f) / 0.55f;
                    br = 9f + Mathf.Sin(t * 26f) * 1.6f;   // 用力时的抖动
                    bp = new Vector2(5f, -2f);
                    bs = new Vector2(0.94f, 1.06f);
                    wrr = -80f;
                    wlr = -50f;
                    smoke = 0.85f + 0.15f * a;
                    aura = 0.25f;
                }
                else
                {
                    float a = (k - 0.75f) / 0.25f;
                    br = Mathf.Lerp(-4f, 0f, a);
                    bp = new Vector2(Mathf.Lerp(-3f, 0f, a), 0f);
                    bs = new Vector2(1f + 0.04f * (1f - a), 1f - 0.03f * (1f - a));
                    wrr = Mathf.Lerp(30f, 0f, a);
                    wlr = Mathf.Lerp(-20f, 0f, a);
                    smoke = 0.45f * (1f - a);
                }
                break;
            }
        }

        // 应用到节点
        _body.Position = BodyOff + bp;
        _body.Scale = bs;
        _body.Rotation = Mathf.DegToRad(br);

        SetHand(_handL, wl, wlr);
        SetHand(_handR, wr, wrr);

        // 受击闪红跟着手一起闪（父类只改 _sprite 的 Modulate）
        if (_handL != null) _handL.Modulate = _body.Modulate;
        if (_handR != null) _handR.Modulate = _body.Modulate;

        UpdateEffects(aura, smoke);
    }

    /// <summary>把手摆到「手腕在某点 + 绕手腕转 rot」的位置。</summary>
    private static void SetHand(Sprite2D s, Vector2 wrist, float rotDeg)
    {
        if (s == null) return;
        float r = Mathf.DegToRad(rotDeg);
        s.Position = wrist + new Vector2(0f, HandHalf).Rotated(r);
        s.Rotation = r;
    }

    // ---------------------------------------------------------- 特效

    private void BuildEffects()
    {
        var visual = GetNodeOrNull<Node2D>("Visual");
        if (visual == null) return;

        Texture2D glow = MakeGlowTexture();

        // 蓄力光环：垫在身体后面
        _aura = new Sprite2D
        {
            Texture = glow,
            Position = new Vector2(0f, -62f),
            Scale = new Vector2(2.6f, 2.6f),
            Modulate = new Color(0.62f, 0.30f, 0.95f, 0f),
            ZIndex = -1,
        };
        visual.AddChild(_aura);

        // 嘴部紫烟：投技时从嘴（帽檐下沿）一股一股往外冒。
        // 团数不能太少 —— 6 团在 1.25 秒里只够冒两三轮，看着像「偶尔咳一口」。
        _puffs = new Sprite2D[Mathf.Max(1, SmokePuffCount)];
        for (int i = 0; i < _puffs.Length; i++)
        {
            _puffs[i] = new Sprite2D
            {
                Texture = glow,
                Position = new Vector2(0f, -70f),
                Scale = Vector2.One,
                Modulate = new Color(0.72f, 0.42f, 0.96f, 0f),
                ZIndex = 1,
            };
            visual.AddChild(_puffs[i]);
        }
    }

    /// <summary>生成一张 64x64 的径向渐变光斑，用来做光环和烟。</summary>
    private static Texture2D MakeGlowTexture()
    {
        var g = new Gradient();
        g.SetColor(0, new Color(1f, 1f, 1f, 0.85f));
        g.SetColor(1, new Color(1f, 1f, 1f, 0f));
        return new GradientTexture2D
        {
            Gradient = g,
            Width = 64,
            Height = 64,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
        };
    }

    private void UpdateEffects(float aura, float smoke)
    {
        if (_aura != null)
        {
            float pulse = 1f + 0.10f * Mathf.Sin(_poseTime * 9f);
            _aura.Scale = new Vector2(2.6f, 2.6f) * pulse * (0.55f + 0.45f * aura);
            _aura.Modulate = new Color(0.62f, 0.30f, 0.95f, 0.55f * aura);
            _aura.Visible = aura > 0.02f;
        }

        if (_puffs == null) return;

        for (int i = 0; i < _puffs.Length; i++)
        {
            var p = _puffs[i];
            if (p == null) continue;

            if (smoke <= 0.02f) { p.Modulate = new Color(0.72f, 0.42f, 0.96f, 0f); continue; }

            // 每团烟有自己的相位，绕着「嘴 → 前方」的扇形往外飘。
            // 相位差要小（0.13）才够密：差太大就变成几缕稀薄的烟柱。
            float ph = _poseTime * 2.1f + i * 0.13f;
            float cycle = ph - Mathf.Floor(ph);              // 0~1 循环
            // 扇形偏前方（美术空间 +x 就是怪的正脸方向），别整片糊在帽子上
            float ang = Mathf.Lerp(-2.4f, -0.5f, (i + 1f) / _puffs.Length);
            float dist = 6f + 40f * cycle;

            p.Position = new Vector2(Mathf.Cos(ang) * dist + 14f, -64f + Mathf.Sin(ang) * dist);
            // 团数多、单团小：要的是「一股一股冒出来」，不是糊一大坨紫云把怪盖住
            p.Scale = Vector2.One * (0.30f + 0.55f * cycle) * (1f + 0.45f * smoke);
            p.Modulate = new Color(0.72f, 0.42f, 0.96f, 0.55f * smoke * (1f - cycle));
        }
    }

    // ---------------------------------------------------------- 死亡 / 重生

    protected override void OnDeath()
    {
        base.OnDeath();

        // 死的时候必须放开玩家，否则玩家会被永远钉在原地
        if (_target != null && IsInstanceValid(_target) && _target.Grabbed)
            _target.EndGrab();

        if (_hand != null && IsInstanceValid(_hand)) _hand.QueueFree();
        _hand = null;
        if (_handR != null) _handR.Visible = true;

        if (_aura != null) _aura.Visible = false;
        if (_puffs != null)
            foreach (var p in _puffs) if (p != null) p.Modulate = new Color(0.72f, 0.42f, 0.96f, 0f);
    }

    public override void Respawn()
    {
        base.Respawn();
        _state = State.Patrol;
        _stateTimer = 0f;
        _cooldown = 0f;
        _ticksDone = 0;
        _damageDealt = 0;
        _tickTimer = 0f;
        _face = 1;
        if (_handR != null) _handR.Visible = true;
        ApplyPose(0f);
    }
}
