using Godot;

/// <summary>
/// 野兽苹果（Beast Apple）—— 地下洞穴关卡的新 Boss。
///
/// 战斗流程：
///   倒挂休眠（无伤害）→ 玩家进入 X 距离 250 且 Y 在战场内 → 触发
///   → 重重坠落至平台（震动）→ 落地吼叫（震动）
///   → 循环放技能 1 / 技能 2
///   → 血量 ≤ 50% 时跳回天花板倒挂，吐 4 只恐怖苹果，等 5 秒跳下来继续（只触发一次）
///   → 血量归零，爆炸（纯视觉，无伤害）
///
/// 所有本体移动都被 ArenaRect 限制，跑不出战场。
///
/// 为什么不继承父类的 _PhysicsProcess：
///   Enemy 的物理帧是「左右巡逻 + 掉头 + 接触扣血」，对 Boss 完全没用，
///   而且它每帧会覆盖 Velocity.X。Boss 的位移全部由自己的状态机给。
///   父类只保留血量、受击、死亡信号这三样东西。
/// </summary>
public partial class BeastAppleBoss : Enemy
{
    // ── 给战场管理器监听 ────────────────────────────────────────
    [Signal] public delegate void BattleStartedEventHandler();
    [Signal] public delegate void ShakeRequestedEventHandler(float strength, float duration);
    [Signal] public delegate void PhaseTwoStartedEventHandler();
    [Signal] public delegate void DefeatedEventHandler();

    // ══════════════════════════ 战斗区域

    [ExportGroup("战斗区域")]
    [Export] public Rect2 ArenaRect = new Rect2(21970f, 0f, 1580f, 500f); // 左上 (21970,0)，宽 1580 高 500
    [Export] public float FloorY = 493f;        // 地面顶面（实测 StaticBody2D5 的顶边）
    [Export] public float CeilingY = 37f;       // 天花板底面（实测 StaticBody2D5 的底边）
    [Export] public float TriggerRangeX = 250f; // 玩家水平离多近触发
    [Export] public float EdgeMargin = 46f;     // 飞扑时中心离左右边界留多少

    // ══════════════════════════ 本体

    [ExportGroup("本体")]
    [Export] public Texture2D FrontTexture;     // 正面（倒挂用）
    [Export] public Texture2D RightTexture;     // 朝右
    [Export] public Texture2D LeftTexture;      // 朝左
    [Export] public Texture2D RoarTexture;      // 吼叫帧（张嘴最大）；留空则退回 FrontTexture
    [Export] public float BodyHeight = 200f;    // 视觉高度（像素）

    // ══════════════════════════ 开场

    [ExportGroup("开场")]
    [Export] public float FallGravity = 2800f;  // 坠落时的重力（比常规大，砸得狠）
    [Export] public float RoarTime = 1.6f;      // 吼叫持续多久

    // ══════════════════════════ 节奏

    [ExportGroup("节奏")]
    [Export] public float IdleMin = 1.2f;       // 两次技能之间至少歇多久
    [Export] public float IdleMax = 2.0f;
    [Export] public float LungeChance = 0.5f;   // 选技能 1 的概率，剩下是技能 2

    // ══════════════════════════ 技能 1：横贯飞扑

    [ExportGroup("技能1 横贯飞扑")]
    [Export] public float LungeChargeTime = 2f;   // 蓄力（玩家看到地面绿圈后的反应时间）
    [Export] public float LungeSpeed = 1500f;     // 横穿速度（弧线时长 = 水平距离 ÷ 这个速度）
    [Export] public float LungeArcHeight = 70f;   // 飞扑弧线最高离地多少（太高会从玩家头顶飞过去，看着不疼）
    [Export] public float DodgeZoneRatio = 0.2f;  // 战场中心这么宽的比例是安全区（1580 × 0.2 = 316）
    [Export] public float LungeRecover = 1.0f;    // 冲完喘气

    // ══════════════════════════ 走位（技能前的「跳到边界」）

    [ExportGroup("走位")]
    [Export] public float RepositionTime = 0.6f;        // 跳到边界用多久
    [Export] public float RepositionArcHeight = 170f;   // 跳向边界时的弧线高度

    // ══════════════════════════ 技能 2：斜向穿梭冲锋

    [ExportGroup("技能2 斜向穿梭冲锋")]
    [Export] public float ChargeXSpeed = 430f;       // 冲锋时恒定的 X 速度
    [Export] public float ChargeJumpVelocity = 820f; // 斜向上的起跳初速度
    [Export] public float ChargeRecover = 1.0f;      // 冲完喘气
    [Export] public int ChargeMaxBounces = 10;       // 最多弹几段（防止速度太慢时永远到不了对面）

    // 撞击光圈：冲锋速度调高以后，光圈如果钉在原地就会「追不上 Boss」。
    // 这两个开关控制它怎么跟。
    [Export] public float ChargeRingDuration = 0.26f; // 每次撞击光圈活多久
    [Export] public bool ChargeRingFollowY = true;    // true = 整个套在身上；false = 只锁 X，贴着天花板/地面滚

    // ══════════════════════════ 半血召唤

    [ExportGroup("半血召唤")]
    [Export] public int PhaseHpPercent = 50;    // 血量降到这个百分比触发
    [Export] public int SpawnCount = 4;         // 吐几只恐怖苹果
    [Export] public float SpawnInterval = 1f;   // 每只间隔
    [Export] public float PhaseHangTime = 5f;   // 吐完等多久跳下来

    // ══════════════════════════ 死亡

    [ExportGroup("死亡")]
    [Export] public float ExplodeTime = 2.4f;   // 爆炸演出时长
    [Export] public Texture2D ExplosionSheet;   // 爆炸序列图（横向 8 帧）；留空则程序化绘制
    [Export] public Texture2D ShatterSheet;     // 本体碎裂序列图（横向 6 帧）；留空则读默认路径
    [Export] public int ShatterFrames = 6;
    [Export] public float ShatterFrameTime = 0.1f;

    // ══════════════════════════ 内部状态

    private enum St
    {
        Dormant,      // 倒挂休眠
        Falling,      // 坠落到平台
        Roar,         // 落地吼叫
        Idle,         // 站定，准备下一次技能
        Reposition,   // 技能前先跳到战场边界（不再瞬移）
        LungeCharge,  // 技能1 蓄力
        LungeDash,    // 技能1 横穿（弧线）
        ChargeUp,     // 技能2 斜向上
        ChargeDown,   // 技能2 斜向下
        PhaseJump,    // 半血：飞回天花板
        PhaseHang,    // 半血：倒挂吐怪
        PhaseWait,    // 半血：吐完等 5 秒
    }

    /// <summary>跳到边界之后要接哪个技能。</summary>
    private enum AfterRepo { Lunge, Charge }

    private St _st = St.Dormant;
    private float _timer;
    private float _animT;
    private float _flash;            // 受击闪红剩余时间
    private int _lastHp;
    private Box _player;

    private Vector2 _hangPos;        // 倒挂位置（场景里摆的）
    private float _halfW, _halfH;    // 本体半宽 / 半高
    private float _groundCenterY;    // 站在地面时的中心 Y
    private float _ceilCenterY;      // 贴天花板时的中心 Y
    private Vector2 _prevPos;
    private Vector2 _baseScale = Vector2.One;

    // 走位
    private float _repoFromX;
    private float _repoToX;
    private float _repoT;
    private float _repoDur;
    private AfterRepo _repoNext;

    // 技能 1
    private float _lungeDir;         // +1 往右飞 / -1 往左飞
    private bool _lungeHitDone;
    private BeastLungeWarning _lungeWarn;
    private float _lungeFromX;
    private float _lungeToX;
    private float _lungeDur;
    private float _lungeT;

    // 技能 2
    private float _chargeDir;
    private bool _chargeHitTop;
    private int _chargeBounces;
    private float _trailTimer;

    // 半血
    private bool _phaseTwoDone;
    private bool _phaseTwoPending;
    private int _spawnLeft;
    private float _spawnTimer;
    private float _phaseWaitTimer;
    private int _spitSeq;

    /// <summary>
    /// Boss 自己生出来的「战斗对象」统一挂这个容器：召唤的恐怖苹果、飞行中的吐球、地面绿圈。
    /// 玩家死亡重开时整锅端掉 —— 否则上一轮吐出来的怪会留到下一轮，玩家一复活就被围殴。
    /// 冲击波 / 碎片 / 残影这些一次性特效不走这里，它们自己会消失。
    /// </summary>
    private Node2D _spawnRoot;

    // 死亡
    private bool _exploded;
    private float _deadT;

    // ── 派生属性
    private float MinCenterX => ArenaRect.Position.X + _halfW + EdgeMargin;
    private float MaxCenterX => ArenaRect.Position.X + ArenaRect.Size.X - _halfW - EdgeMargin;
    private float ZoneCenterX => ArenaRect.Position.X + ArenaRect.Size.X * 0.5f;
    private float ZoneHalfWidth => ArenaRect.Size.X * DodgeZoneRatio * 0.5f;

    /// <summary>当前状态名（只读，给探针和调试用）。</summary>
    public string StateName => _st.ToString();

    /// <summary>半血阶段是否已经放过了（只读，调试用）。</summary>
    public bool PhaseTwoDone => _phaseTwoDone;

    public override void _Ready()
    {
        base._Ready();   // 父类：取节点、_hp = MaxHp、_spawnPos

        // 音效全部走 beast_* 这一套，和第一关的大苹果 Boss 彻底分开。
        // 文件名清单见文件末尾的「音效清单」注释。
        HurtSfx = "beast_hurt";
        DeathSfx = "beast_die";

        _player = GetTree().Root.FindChild("box", true, false) as Box;
        _lastHp = Hp;
        _prevPos = GlobalPosition;
        ImmuneToKnockback = true;    // Boss 不吃击退

        ApplyTexture(FrontTexture);
        _halfH = BodyHeight * 0.5f;
        _halfW = _sprite != null && _sprite.Texture != null
            ? _sprite.Texture.GetWidth() * (_sprite.Scale.X) * 0.5f
            : _halfH;

        _groundCenterY = FloorY - _halfH;
        _ceilCenterY = CeilingY + _halfH;

        // 碎裂序列图默认按路径读，省得每次都要在 Inspector 里拖一次。
        // 想换素材就改这里的路径，或者直接在检查器里给 ShatterSheet 赋值覆盖。
        if (ShatterSheet == null)
            ShatterSheet = GD.Load<Texture2D>("res://assets/boss/beast/beast_shatter.png");

        // 倒挂位置：X 用场景里摆的，Y 贴着天花板往下让一点。
        // 完全贴住（中心 = 37 + 100 = 137）时，Boss 顶边会顶进屏幕顶部的血条里，
        // 视觉上像被血条压住。往下让 26px 留出呼吸空间，看起来仍然是「挂在天花板上」。
        _hangPos = new Vector2(GlobalPosition.X, CeilingY + _halfH + 46f);

        SyncBodyShape();

        // 倒挂：整只翻过来，蒂朝下
        Rotation = Mathf.Pi;
        EnterDormant();
    }

    // ══════════════════════════ 主循环

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        if (_dead) { UpdateDeathCustom(dt); return; }

        _flash = Mathf.Max(0f, _flash - dt);
        UpdateModulate();

        // 受击无敌必须自己递减：父类的递减写在 Enemy._PhysicsProcess 里，
        // 而 Boss 完全不调父类物理帧。不补这一行的话 _hitInvuln 会永远停在 0.25，
        // 表现是「Boss 只吃第一下，之后所有攻击都无效」。
        _hitInvuln = Mathf.Max(0f, _hitInvuln - dt);

        // 受击检测（父类的 _hitInvuln 是 private，这里靠血量变化反推）
        if (Hp != _lastHp) { _lastHp = Hp; _flash = 0.14f; }

        CheckPhaseTwo();

        _prevPos = GlobalPosition;

        if (_st == St.Dormant) { CheckTrigger(); return; }

        _timer -= dt;
        _animT += dt;

        switch (_st)
        {
            case St.Falling:     UpdateFalling(dt); break;
            case St.Roar:        UpdateRoar(dt); break;
            case St.Idle:        UpdateIdle(dt); break;
            case St.Reposition:  UpdateReposition(dt); break;
            case St.LungeCharge: UpdateLungeCharge(dt); break;
            case St.LungeDash:   UpdateLungeDash(dt); break;
            case St.ChargeUp:    UpdateChargeUp(dt); break;
            case St.ChargeDown:  UpdateChargeDown(dt); break;
            case St.PhaseJump:   UpdatePhaseJump(dt); break;
            case St.PhaseHang:   UpdatePhaseHang(dt); break;
            case St.PhaseWait:   UpdatePhaseWait(dt); break;
        }

        // 接触伤害：站着的时候碰到就掉血（Box 自己有 1 秒受击冷却，不会连扣）
        // 飞扑期间必须关掉：Boss 从玩家身上压过去时，接触判定会绕过「圈内蹲下可躲」的规则，
        // 变成「蹲下也照样掉血」。那一段的命中只由 CheckLungeHit 负责。
        // 走位（Reposition）也关掉：那只是换位置，不是攻击，不该顺手把人撞死。
        if (_st != St.Falling && _st != St.PhaseJump && _st != St.LungeDash
            && _st != St.Reposition)
            CheckContactDamage();

        // 半血动作挂起中：技能一结束就补上，绝不打断正在放的技能
        if (_phaseTwoPending && _st == St.Idle)
        {
            _phaseTwoPending = false;
            StartPhaseTwo();
        }
    }

    /// <summary>
    /// 血量掉到半血线时排队进入召唤阶段。
    /// 正在放技能 1 / 2 就挂起（_phaseTwoPending），等技能放完回 Idle 再执行。
    /// </summary>
    private void CheckPhaseTwo()
    {
        if (_phaseTwoDone || _phaseTwoPending) return;
        if (_st == St.Dormant) return;          // 还没开战，不算

        float percent = Hp * 100f / Mathf.Max(1, MaxHp);
        if (percent > PhaseHpPercent) return;

        bool busy = _st == St.Reposition
                    || _st == St.LungeCharge || _st == St.LungeDash
                    || _st == St.ChargeUp || _st == St.ChargeDown;

        if (busy) { _phaseTwoPending = true; return; }
        StartPhaseTwo();
    }

    // ══════════════════════════ 触发

    private void CheckTrigger()
    {
        if (_player == null || !IsInstanceValid(_player)) return;

        // 玩家躺在地上 / 正在复活 → 不开战。
        // 不加这一条的话，玩家被打死、Boss 被重置回倒挂状态后，
        // 下一帧会发现「尸体还在 250 以内」又开一次战，镜头每帧在 0.52 和 1.0 之间来回切 = 闪屏。
        if (!_player.IsAlive) return;

        float dx = Mathf.Abs(_player.GlobalPosition.X - GlobalPosition.X);
        float py = _player.GlobalPosition.Y;
        bool inArenaY = py >= ArenaRect.Position.Y - 40f
                        && py <= ArenaRect.Position.Y + ArenaRect.Size.Y + 40f;

        if (dx > TriggerRangeX || !inArenaY) return;

        EmitSignal(SignalName.BattleStarted);
        StartFall();
    }

    private void StartFall()
    {
        _st = St.Falling;
        _timer = 0f;
        _animT = 0f;
        _sprite?.SetDeferred("rotation", 0f);
        Rotation = 0f;
        ApplyTexture(FrontTexture);
        Velocity = Vector2.Zero;
        AudioManager.Instance?.PlaySfx("beast_roar", 0.7f, 0.8f);
    }

    // ══════════════════════════ 坠落

    private void UpdateFalling(float dt)
    {
        Velocity += new Vector2(0f, FallGravity * dt);
        GlobalPosition += Velocity * dt;

        // 从倒挂翻回正立：越接近地面转得越正
        float k = Mathf.Clamp((GlobalPosition.Y - _hangPos.Y) / Mathf.Max(1f, _groundCenterY - _hangPos.Y), 0f, 1f);
        Rotation = Mathf.Pi * (1f - k);

        // 坠落中纵向拉长
        if (_sprite != null)
            _sprite.Scale = _baseScale * new Vector2(1f - 0.10f * k, 1f + 0.16f * k);

        if (GlobalPosition.Y >= _groundCenterY)
        {
            GlobalPosition = new Vector2(GlobalPosition.X, _groundCenterY);
            Velocity = Vector2.Zero;
            Rotation = 0f;
            LandImpact(22f, 0.55f, 1.0f);
            StartRoar();
        }
    }

    // ══════════════════════════ 吼叫

    private void StartRoar()
    {
        _st = St.Roar;
        _timer = RoarTime;
        _animT = 0f;
        ApplyTexture(RoarTexture != null ? RoarTexture : FrontTexture);
        Rotation = 0f;
        AudioManager.Instance?.PlaySfx("beast_roar", 1f, 0.9f);
    }

    private void UpdateRoar(float dt)
    {
        // 张嘴咆哮：整只脉动放大 + 左右微抖
        float p = 1f - Mathf.Max(0f, _timer) / Mathf.Max(0.01f, RoarTime);
        float pulse = 1f + 0.10f * Mathf.Sin(p * Mathf.Pi * 5f) * (1f - p * 0.35f);
        if (_sprite != null)
        {
            _sprite.Scale = _baseScale * new Vector2(pulse, pulse);
            _sprite.Rotation = Mathf.Sin(_animT * 26f) * 0.02f;
        }

        // 吼叫期间持续低频震屏
        if (Mathf.PosMod(_animT, 0.22f) < dt)
            EmitSignal(SignalName.ShakeRequested, 9f, 0.16f);

        if (_timer <= 0f)
        {
            if (_sprite != null) { _sprite.Scale = _baseScale; _sprite.Rotation = 0f; }
            EnterIdle();
        }
    }

    // ══════════════════════════ 待机 / 选技能

    private void EnterIdle()
    {
        _st = St.Idle;
        _timer = (float)GD.RandRange(IdleMin, IdleMax);
        Velocity = Vector2.Zero;
        Rotation = 0f;
        if (_sprite != null) { _sprite.Scale = _baseScale; _sprite.Rotation = 0f; }
        FacePlayer();
    }

    private void UpdateIdle(float dt)
    {
        // 呼吸
        if (_sprite != null)
        {
            float b = 1f + 0.025f * Mathf.Sin(_animT * 3.2f);
            _sprite.Scale = _baseScale * new Vector2(b, 2f - b);
        }
        FacePlayer();

        // 站在地面上（被击飞或坠落过就拉回来）
        if (GlobalPosition.Y < _groundCenterY - 4f)
            GlobalPosition += new Vector2(0f, 300f * dt);

        if (_timer <= 0f)
        {
            if (GD.Randf() < LungeChance) StartLunge();
            else StartCharge();
        }
    }

    /// <summary>
    /// 拿到（必要时新建）召唤物容器。
    /// 用 TopLevel 是为了让它的子节点不受父节点变换影响 —— 这样 Setup() 里传的全局坐标
    /// 和直接挂到场景根下完全等价，将来就算 BeastArena 被挪了位置也不会错位。
    /// </summary>
    private Node2D SpawnRoot
    {
        get
        {
            if (_spawnRoot != null && IsInstanceValid(_spawnRoot)) return _spawnRoot;
            _spawnRoot = new Node2D { Name = "BossSpawned", TopLevel = true };
            GetParent().AddChild(_spawnRoot);
            return _spawnRoot;
        }
    }

    /// <summary>换贴图，同时按 BodyHeight 重算缩放（三张图尺寸不同，统一高度）。</summary>
    private void ApplyTexture(Texture2D tex)
    {
        if (_sprite == null || tex == null) return;

        _sprite.Texture = tex;
        _sprite.Hframes = 1;
        _sprite.Frame = 0;

        float s = BodyHeight / Mathf.Max(1f, tex.GetHeight());
        _sprite.Scale = new Vector2(s, s);
        _baseScale = _sprite.Scale;

        _halfH = BodyHeight * 0.5f;
        _halfW = tex.GetWidth() * s * 0.5f;
        _groundCenterY = FloorY - _halfH;
        _ceilCenterY = CeilingY + _halfH;
        SyncBodyShape();
    }

    /// <summary>碰撞体和受击区跟着贴图尺寸走。</summary>
    private void SyncBodyShape()
    {
        var body = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (body != null)
            body.Shape = new RectangleShape2D { Size = new Vector2(_halfW * 1.7f, _halfH * 1.9f) };

        var hurt = GetNodeOrNull<Area2D>("HurtArea");
        if (hurt != null)
        {
            var cs = hurt.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
            if (cs != null)
                cs.Shape = new RectangleShape2D { Size = new Vector2(_halfW * 1.9f, _halfH * 2.05f) };
        }
    }

    private void FacePlayer()
    {
        if (_player == null || !IsInstanceValid(_player)) return;
        bool right = _player.GlobalPosition.X >= GlobalPosition.X;
        ApplyTexture(right ? RightTexture : LeftTexture);
    }

    private void UpdateModulate()
    {
        if (_sprite == null) return;
        _sprite.Modulate = _flash > 0f ? new Color(1f, 0.55f, 0.55f) : Colors.White;
    }

    /// <summary>落地 / 撞击：震屏 + 音效 + 压扁。</summary>
    private void LandImpact(float shake, float shakeTime, float pitch)
    {
        EmitSignal(SignalName.ShakeRequested, shake, shakeTime);
        AudioManager.Instance?.PlaySfx("beast_slam", 1f, pitch);
        if (_sprite != null)
            _sprite.Scale = _baseScale * new Vector2(1.12f, 0.88f);

        var w = new BeastShockwave { Radius = _halfW * 1.6f, Duration = 0.4f };
        GetParent().AddChild(w);
        w.GlobalPosition = new Vector2(GlobalPosition.X, FloorY);
    }

    /// <summary>把 Boss 中心夹在战场里，绝不让它跑出去。</summary>
    private void ClampToArena()
    {
        float x = Mathf.Clamp(GlobalPosition.X, MinCenterX, MaxCenterX);
        float y = Mathf.Clamp(GlobalPosition.Y, _ceilCenterY, _groundCenterY);
        if (x != GlobalPosition.X || y != GlobalPosition.Y)
            GlobalPosition = new Vector2(x, y);
    }

    private Rect2 BodyRectAt(Vector2 nodePos)
    {
        var size = new Vector2(_halfW * 1.7f, _halfH * 1.9f);
        return new Rect2(nodePos - size * 0.5f, size);
    }

    private void CheckContactDamage()
    {
        if (_player == null || !IsInstanceValid(_player)) return;
        if (!BodyRectAt(GlobalPosition).Intersects(_player.BodyRect())) return;
        _player.TakeDamage(Damage, GlobalPosition);
    }
}
