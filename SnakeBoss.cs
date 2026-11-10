using Godot;

/// <summary>
/// 伊甸园 Boss：缠在苹果树上的黑蛇（血 5000）。
///
/// 站位：节点原点 = 树干中心的地面（世界 6964, 700）。蛇身整条盘在树干下段，
/// 头不垂地；受击判定是一片覆盖树干下段的矩形，砍到哪儿都算。
///
/// 状态机：Dormant →（对话完）Idle → 随机 1/2/3 → Idle → … →（半血狂暴）→ … → Dead
/// 技能实现在 SnakeBoss.Skills.cs，死亡在 SnakeBoss.Death.cs。
/// </summary>
public partial class SnakeBoss : Enemy
{
    public enum SState { Dormant, Intro, Idle, Spit, Tail, Summon, Dead }

    [ExportGroup("1 血量与节奏")]
    // 血量用 Enemy 的 MaxHp（在 SnakeBossArena.tscn 里填 5000），这里只放节奏参数
    [Export] public int EnragePercent = 50;         // 血量降到这个百分比进入狂暴
    [Export] public float IdleTime = 1.35f;         // 两次技能之间歇多久
    [Export] public float EnragedIdleScale = 0.68f; // 狂暴后间隔缩短到几成

    [ExportGroup("2 技能一：蛇毒")]
    [Export] public int SpitCount = 6;              // 一次吐几滩
    [Export] public float SpitGap = 0.18f;          // 每滩间隔
    [Export] public int SpitDamage = 10;            // 毒弹飞行中砸到人
    [Export] public float SpitFlightTime = 0.85f;   // 毒弹飞多久落地
    [Export] public float SpitSpreadNear = 80f;     // 第一滩离玩家多远
    [Export] public float SpitSpreadStep = 125f;    // 每两滩往外多铺多远
    [Export] public float SpitJitter = 26f;         // 每滩落点的随机抖动
    [Export] public int PoolDamage = 10;            // 毒池每跳伤害
    [Export] public float PoolRadius = 70f;         // 毒池半径
    [Export] public float PoolDuration = 4f;        // 毒池存在多久
    [Export] public float PoolTickInterval = 1f;    // 毒池每隔多久扣一次

    [ExportGroup("3 技能二：甩尾")]
    [Export] public int TailDamage = 20;            // 扫中一下扣多少
    [Export] public float TailRange = 560f;         // 从树干量到墙的距离（= 一边的整个限制区）
    [Export] public float TailWarn = 1.5f;          // 抬尾蓄力时长
    [Export] public float TailSwing = 0.3f;         // 扫出去用多久（5→8 帧）。和 TailRecover 加起来别超 0.5s
    [Export] public float TailRecover = 0.15f;      // 收回去用多久（9→11 帧）
    [Export] public float TailGroundGap = 6f;       // 尾巴下沿离地面多高
    [Export] public float TailHitHeight = 100f;     // 玩家跳起超过这个高度就躲过去了
    [Export] public float TailHitPad = 26f;         // 判定带两端各放宽多少（防高速漏判）
    [Export] public float TailShakeAmp = 13f;       // 扫中时的震屏强度
    [Export] public float TailShakeTime = 0.42f;

    [ExportGroup("4 技能二附带：震落苹果")]
    [Export] public int ApplePerWave = 7;           // 每波几颗
    [Export] public int AppleWaves = 3;             // 一共几波
    [Export] public float AppleWaveGap = 1f;        // 波与波间隔
    [Export] public int AppleDamage = 12;           // 砸中一下扣多少
    [Export] public float AppleStartHeight = 520f;  // 从地面上方多高开始落
    [Export] public float AppleGravity = 820f;      // 下落重力（越小掉得越慢）
    [Export] public float AppleSpin = 1.7f;         // 自转角速度（弧度/秒）
    [Export] public float AppleWarnTime = 0.85f;    // 落点红圈预警多久
    [Export] public float AppleShakeAmp = 9f;       // 每波震树的震屏强度
    [Export] public float AppleShakeTime = 0.34f;

    [ExportGroup("5 技能三：召唤")]
    [Export] public float SummonCharge = 1f;        // 法阵蓄力 1 秒
    [Export] public int SummonCount = 2;            // 平时一次召几只
    [Export] public float SummonCooldown = 9f;      // 召唤冷却，免得一直召
    [Export] public float SummonRadius = 80f;       // 法阵半径
    [Export] public float SummonSpreadNear = 200f;  // 第一只在树旁多远
    [Export] public float SummonSpreadStep = 110f;  // 每两只往外多铺多远

    [ExportGroup("6 狂暴")]
    [Export] public int EnrageSummonCount = 5;      // 进狂暴那一下召几只
    [Export] public float EnrageShakeAmp = 10f;
    [Export] public float EnrageShakeTime = 0.4f;

    [ExportGroup("7 锚点（相对树干中心）")]
    [Export] public Vector2 MouthAnchor = new Vector2(-26f, -296f);
    [Export] public float BodyWidth = 186f;         // 蛇身世界宽度（贴图 172 宽，这里放大一点压住树干）

    [ExportGroup("8 音效名")]
    // 全部对应 res://assets/audio/sfx/<名字>.ogg / .wav / .mp3，文件不存在就静默跳过。
    // 想给某个动作单独配音：把文件丢进 sfx 目录，再把这里的字符串改成文件名（不带扩展名）。
    // 留空 = 这个动作不发声。受击 / 死亡用的是 Enemy 里的 HurtSfx、DeathSfx（也在检查器里）。
    [Export] public string SfxSpit = "enemy_hurt";      // 吐毒：每吐一滩
    [Export] public string SfxTailWind = "enemy_hurt";  // 甩尾：抬尾起手
    [Export] public string SfxTailHit = "";             // 甩尾：扫到玩家（默认不播）
    [Export] public string SfxTreeShake = "collapse";   // 震树：每波苹果
    [Export] public string SfxSummon = "collapse";      // 召唤：小怪落地
    [Export] public string SfxEnrage = "enemy_hurt";    // 狂暴：进半血那一下
    [Export] public string SfxAppleLand = "collapse";   // 苹果砸到地上（每颗一次）
    [Export] public float SfxAppleLandGap = 0.12f;      // 落地音效最短间隔（秒），防止一串苹果叠成噪音
    [Export] public string SfxPoolLand = "collapse";    // 毒池落地
    [Export] public string SfxCircle = "collapse";      // 召唤法阵成型

    // ── 运行时
    public SState State { get; private set; } = SState.Dormant;
    public bool Enraged { get; private set; }
    public bool BattleStarted => State != SState.Dormant && State != SState.Intro;

    private Sprite2D _art;
    private Sprite2D _deadArt;
    private Sprite2D _tailArt;
    private Texture2D _texIdle, _texOpen;
    private Vector2 _bodyScale = Vector2.One;
    private float _hurtT;
    private Node2D _tail;
    private Box _player;
    private SnakeArena _arena;
    private float _idleTimer;
    private float _summonCd;
    private int _lastSkill = -1;
    private int _repeat;
    private int _lastHp = -1;
    private float _flash;
    private float _t;
    private float _tailAnim;      // 0..1 抬尾/甩击进度
    private bool _tailSwinging;
    private float _bodyShake;     // 震树时整个身子抖一下
    private int _fightGen;        // 战斗代次：重置一次 +1，异步技能靠它判断"这一场已经作废"

    public override void _Ready()
    {
        base._Ready();

        Damage = 0;                  // 不靠身体碰撞扣血，全靠技能
        ImmuneToKnockback = true;
        Invulnerable = true;         // 开战前免疫
        _player = GetTree().Root.FindChild("box", true, false) as Box;
        _arena = GetParent() as SnakeArena;

        _art = GetNodeOrNull<Sprite2D>("Art");
        _deadArt = GetNodeOrNull<Sprite2D>("ArtDead");
        _tailArt = GetNodeOrNull<Sprite2D>("TailArt");
        _tail = GetNodeOrNull<Node2D>("Tail");

        _texIdle = GD.Load<Texture2D>("res://assets/eden/snake_body_idle.png");
        _texOpen = GD.Load<Texture2D>("res://assets/eden/snake_body_open.png");
        if (_art != null && _texIdle != null) _art.Texture = _texIdle;
        if (_deadArt != null) { _deadArt.Visible = false; _deadArt.Modulate = new Color(1, 1, 1, 0); }
        if (_tailArt != null) _tailArt.Visible = false;

        if (_art != null && _art.Texture != null)
        {
            float s = BodyWidth / _art.Texture.GetWidth();
            _bodyScale = Vector2.One * s;
            _art.Scale = _bodyScale;
            _art.Offset = new Vector2(0f, -_art.Texture.GetHeight() * 0.5f);
            _art.Frame = 0;
        }

        _idleTimer = 1.2f;
        _lastHp = Hp;
    }

    // ────────────────────────────────────────── 主循环

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _t += dt;

        if (State == SState.Dead)
        {
            TickDeath(dt);
            return;
        }
        if (_flash > 0f) { _flash -= dt; ApplyFlash(); }

        // 待机呼吸 + 受击形变（被打时横向撑开、纵向压扁一下）
        if (_hurtT > 0f) _hurtT -= dt;
        float jx = 0f, jy = 0f;
        if (_bodyShake > 0f)
        {
            // 震树：整条蛇跟着抖，和呼吸叠加，不会因为呼吸每帧重置而丢
            _bodyShake -= dt;
            float s = Mathf.Clamp(_bodyShake / 0.3f, 0f, 1f);
            jx = (float)GD.RandRange(-1f, 1f) * 7f * s;
            jy = (float)GD.RandRange(-1f, 1f) * 5f * s;
        }
        if (_art != null)
        {
            float b = Mathf.Sin(_t * 1.55f);
            float h = Mathf.Clamp(_hurtT / 0.18f, 0f, 1f);
            _art.Position = new Vector2(b * 2.2f + jx, Mathf.Abs(b) * 1.6f + jy);
            _art.Scale = new Vector2(
                _bodyScale.X * (1f + 0.12f * h),
                _bodyScale.Y * (1f - 0.13f * h));
        }

        // 血量变化检测（TryTakeHit 不是虚方法，只能靠对比）
        if (Hp != _lastHp)
        {
            if (Hp < _lastHp) { _flash = 0.12f; _hurtT = 0.18f; ApplyFlash(); }
            _lastHp = Hp;
            if (!Enraged && MaxHp > 0 && Hp * 100 <= MaxHp * EnragePercent && Hp > 0)
                EnterEnrage();
        }

        if (State == SState.Dormant || State == SState.Intro) return;
        if (State == SState.Spit || State == SState.Summon) return;

        if (_summonCd > 0f) _summonCd -= dt;

        if (State == SState.Tail)
        {
            TickTail(dt);
            return;
        }

        // Idle：等一个间隔，然后挑一个技能
        _idleTimer -= dt;
        if (_idleTimer <= 0f) PickSkill();
    }

    // ────────────────────────────────────────── 对外

    /// <summary>对话结束，正式开打。</summary>
    public void BeginBattle()
    {
        // 对话阶段会把状态置成 Intro，这里要允许从 Intro 进 Idle
        if (State == SState.Dead || State == SState.Idle || State == SState.Spit
            || State == SState.Tail || State == SState.Summon) return;
        State = SState.Idle;
        Invulnerable = false;
        _idleTimer = 1.0f;
        GD.Print("[SnakeBoss] 开战");
    }

    public void SetIntro()
    {
        if (State == SState.Dormant) State = SState.Intro;
    }

    /// <summary>
    /// 玩家阵亡、整场战斗作废：回满血、回到休眠、重新上锁，并清掉场上残留的召唤物。
    /// <see cref="_fightGen"/> 一起 +1，还在 await 的技能协程会自己退出。
    /// </summary>
    public void ResetFight()
    {
        _fightGen++;

        State = SState.Dormant;
        Invulnerable = true;
        Enraged = false;
        _idleTimer = 1.2f;
        _summonCd = 0f;
        _lastSkill = -1;
        _repeat = 0;
        _tailT = 0f;
        _bodyShake = 0f;
        _hurtT = 0f;
        _flash = 0f;
        _tailFrame = -1;

        if (_tailArt != null) _tailArt.Visible = false;
        if (_deadArt != null)
        {
            _deadArt.Visible = false;
            _deadArt.Modulate = new Color(1f, 1f, 1f, 0f);
        }
        if (_art != null)
        {
            _art.Visible = true;
            _art.Modulate = Colors.White;
            _art.Position = Vector2.Zero;
            _art.Scale = _bodyScale;
        }
        SetMouthOpen(false);

        SetHp(MaxHp);          // 只改血量，不触发死亡流程
        _lastHp = Hp;

        ClearSpawned();
        QueueRedraw();
        GD.Print("[SnakeBoss] 战斗已重置");
    }

    /// <summary>清掉 Boss 放出来的东西（小怪 / 毒池 / 苹果 / 法阵），免得复活后一进场就被打死。</summary>
    private void ClearSpawned()
    {
        var parent = GetParent();
        if (parent == null) return;
        foreach (var c in parent.GetChildren())
        {
            if (c == this) continue;
            if (c is SnakeMinion || c is PoisonZone || c is FallingApple
                || c is PoisonBlob || c is MagicCircle || c is AppleBurst)
                c.QueueFree();
        }
    }

    private void PickSkill()
    {
        int pick;
        // 召唤有冷却；连续两次同技能就换一个
        bool canSummon = _summonCd <= 0f;
        for (int guard = 0; guard < 8; guard++)
        {
            pick = (int)(GD.Randi() % 3) + 1;
            if (pick == 3 && !canSummon) continue;
            if (pick == _lastSkill && _repeat >= 1) continue;
            if (pick == _lastSkill) _repeat++; else _repeat = 0;
            _lastSkill = pick;
            switch (pick)
            {
                case 1: DoSpit(); return;
                case 2: DoTail(); return;
                default: DoSummon(); return;
            }
        }
        DoSpit();
    }

    private Vector2 MouthWorld => GlobalPosition + MouthAnchor;

    /// <summary>吐毒前把嘴张开（换一张贴图，盘绕完全重合，不会跳）。</summary>
    public void SetMouthOpen(bool open)
    {
        if (_art == null) return;
        var tex = open ? _texOpen : _texIdle;
        if (tex != null) _art.Texture = tex;
    }

    /// <summary>震树：整条蛇抖一下（苹果掉落时用）。</summary>
    public void ShakeBody(float time) => _bodyShake = Mathf.Max(_bodyShake, time);

    /// <summary>
    /// 甩尾的地面预警带：只画选中的那一侧，从树干一直铺到墙。
    /// 尾巴本体是独立贴图（TailArt），这里只画红色危险区 + 蓄力推进线。
    /// </summary>
    public override void _Draw()
    {
        if (State != SState.Tail) return;
        float life = TailWarn + TailSwing + 0.35f;
        if (_tailT > life) return;

        float lo = Mathf.Min(0f, _tailSide * _tailReach);
        float hi = Mathf.Max(0f, _tailSide * _tailReach);
        float w = hi - lo;
        if (w < 1f) return;

        float k = Mathf.Clamp(_tailT / Mathf.Max(0.05f, TailWarn), 0f, 1f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(_t * 14f);
        const float top = -66f;
        const float hgt = 62f;

        if (_tailT <= TailWarn)
        {
            // ① 整侧底色：够红的半透明覆盖，不会被误看成树影
            DrawRect(new Rect2(lo, top, w, hgt), new Color(0.88f, 0.07f, 0.05f, 0.28f + 0.10f * pulse));
            // ② 竖条纹（像素风危险区），不画斜线避免溢出矩形外
            for (float sx = lo + 10f; sx < hi - 4f; sx += 32f)
                DrawLine(new Vector2(sx, top + 3f), new Vector2(sx, top + hgt - 3f),
                         new Color(1f, 0.30f, 0.18f, 0.13f + 0.07f * pulse), 3f);
            // ③ 蓄力推进：从树干往墙填满，读得出还剩多久
            float fill = w * k;
            float fx = _tailSide > 0 ? lo : hi - fill;
            DrawRect(new Rect2(fx, top, fill, hgt), new Color(1f, 0.36f, 0.18f, 0.20f + 0.12f * pulse));
            // ④ 外框 + 地面线：把区域"框"出来
            DrawRect(new Rect2(lo, top, w, hgt), new Color(1f, 0.5f, 0.3f, 0.5f + 0.35f * k), false, 3f);
            DrawLine(new Vector2(lo, -6f), new Vector2(hi, -6f),
                     new Color(1f, 0.4f, 0.24f, 0.5f + 0.4f * k), 3f);
            float bx = _tailSide > 0 ? lo + fill : hi - fill;
            DrawLine(new Vector2(bx, top), new Vector2(bx, top + hgt),
                     new Color(1f, 0.7f, 0.45f, 0.55f + 0.45f * pulse), 4f);
        }
        else
        {
            // 扫过去之后余影快速褪掉
            float f = Mathf.Clamp((_tailT - TailWarn) / 0.4f, 0f, 1f);
            DrawRect(new Rect2(lo, top, w, hgt), new Color(0.95f, 0.12f, 0.10f, 0.24f * (1f - f)));
        }
    }

    private void ApplyFlash()
    {
        if (_art == null) return;
        float k = Mathf.Clamp(_flash / 0.12f, 0f, 1f);
        _art.Modulate = new Color(1f + 0.9f * k, 1f - 0.25f * k, 1f - 0.25f * k, 1f);
    }
}
