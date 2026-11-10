using Godot;
using System.Collections.Generic;

/// <summary>
/// 泥潭：玩家踩进来移动变慢、跳不高，进出和行走时给泥浆反馈。
/// </summary>
/// <remarks>
/// 节点怎么摆：
///   把 MudPit 挂在一个 Area2D 上，Area2D 的 <b>位置放在泥浆表面那条线上</b>，
///   碰撞形状往下延伸（覆盖泥浆面到潭底）。这样冒泡粒子只需要从 y=0 往上冒，
///   不用再算表面偏移。
///
///   粒子（冒泡 / 飞溅）全部由代码建，不依赖场景里手摆，
///   换一个更宽的泥潭时只改 Area2D 的碰撞形状，粒子会自动跟着铺开。
///
/// 为什么倍率放这里而不是放 Box 上：
///   不同泥潭可以有不同的黏度（浅泥 / 深沼 / 沥青），玩家那边只负责
///   「取所有踩着的泥潭里最黏的那个」，两边职责分开，加新泥潭不用改玩家代码。
///
/// 载具为什么要单独一组倍率：
///   同一片泥，人还能趟着走，车比人重好几倍、轮子是硬接触，进去基本就陷住了。
///   用同一个 SpeedMult 的话，载具自带的 1.3 倍速会把泥的惩罚抵掉一半，
///   表现成「开车在泥上照样飞」，和想要的「几乎推不动」完全相反。
/// </remarks>
public partial class MudPit : Area2D
{
    // ── 对玩家的限制 ──────────────────────────────────────────
    [ExportGroup("玩家限制")]
    /// <summary>横向移动速度倍率。0.6 = 还能走六成速度。</summary>
    [Export(PropertyHint.Range, "0.05,1,0.01")] public float SpeedMult = 0.60f;

    /// <summary>
    /// 跳跃力倍率。
    /// <para>
    /// <b>这里填的是「力」，不是「高度」</b>，两者差一个平方关系：
    /// 高度 = v² / (2g)，所以 高度倍率 = 本值²。
    /// </para>
    /// <para>
    /// 想要「跳起高度只剩原来的 40%」→ 本值 = √0.4 ≈ <b>0.632</b>。
    /// 如果直接填 0.4，高度只剩 0.16（16%），比原来还闷得多。
    /// </para>
    /// </summary>
    [Export(PropertyHint.Range, "0.05,1,0.01")] public float JumpMult = 0.632f;

    /// <summary>加速度倍率。压低它会让起步和转向更迟滞，加重「陷在泥里」的感觉。</summary>
    [Export(PropertyHint.Range, "0.05,1,0.01")] public float AccelMult = 0.75f;

    // ── 对载具的限制 ──────────────────────────────────────────
    [ExportGroup("载具限制")]
    /// <summary>
    /// 坐着载具进泥潭时的横向速度倍率。<b>乘在载具自身倍率之上，不是替换。</b>
    /// <para>
    /// 实际速度 = Speed × Box.VehicleSpeedMult × 本值。
    /// jungle 里 Speed=700、VehicleSpeedMult=1.3，本值 0.12 → 约 <b>109 像素/秒</b>；
    /// 而同样在泥里走路是 700×0.4 = 280。所以开车进泥比下车走还慢，
    /// 玩家自然会想弃车 —— 这正是「极其缓慢」想要的效果。
    /// </para>
    /// <para>
    /// 想调成「明显变慢但还能忍」就填 0.3 左右；0.12 是「陷住了」。
    /// </para>
    /// </summary>
    [Export(PropertyHint.Range, "0.02,1,0.01")] public float VehicleSpeedMult = 0.12f;

    /// <summary>
    /// 载具在泥里的加速度倍率。压得比速度更低，起步会有明显的「轮子空转」迟滞，
    /// 不会一按方向键就窜到那个很低的上限。
    /// </summary>
    [Export(PropertyHint.Range, "0.05,1,0.01")] public float VehicleAccelMult = 0.35f;

    // ── 入泥反馈 ──────────────────────────────────────────────
    [ExportGroup("入泥反馈")]
    [Export] public string SplashSfx = "mud_splash";
    [Export(PropertyHint.Range, "0,1,0.05")] public float SplashVolume = 0.375f;   // 原来 0.75，太刺耳，砍到 50%

    /// <summary>两次入泥反馈之间的最短间隔，防止在边界反复进出时刷屏。</summary>
    [Export] public float EnterCooldown = 0.4f;

    /// <summary>下落速度超过这个值才算「砸进去」，否则算「走进去」，反馈小一号。</summary>
    [Export] public float HeavyFallSpeed = 130f;

    [Export] public int SplashAmount = 18;
    [Export] public float SplashSpeed = 260f;
    [Export] public int SoftSplashAmount = 5;
    [Export] public float SoftSplashSpeed = 110f;

    /// <summary>
    /// 泥点从溅起到落回泥面需要多久。超过这个时间粒子就该消失，
    /// 否则会穿到泥浆下面去，看着像泥点掉进了地底。
    /// 220 初速配 1200 重力：飞起约 20px，0.37 秒落回原点。
    /// </summary>
    [Export] public float SplashLifetime = 0.5f;

    /// <summary>
    /// 单个泥点的缩放范围。mud_drop.png 只有 8×8，放大 1 倍 = 8 像素。
    /// 之前这里写 1.5~3.0，等于把泥点画成 24 像素的大方块，糊在主角两侧像两坨灰斑。
    /// 像素风的飞溅要「碎」，0.5~1.1 就是 4~9 像素，才对得上颗粒感。
    /// </summary>
    [Export] public float SplashScaleMin = 0.5f;
    [Export] public float SplashScaleMax = 1.1f;

    /// <summary>跳跃时溅起的泥点比普通入泥更多，这个倍数单独放大。</summary>
    [Export] public float JumpSplashScale = 1.35f;

    // ── 行走反馈 ──────────────────────────────────────────────
    [ExportGroup("行走反馈")]
    [Export] public string StepSfx = "mud_step";
    [Export(PropertyHint.Range, "0,1,0.05")] public float StepVolume = 0.25f;      // 原来 0.5，同步砍到 50%

    /// <summary>泥里走多久踩一下（秒）。</summary>
    [Export] public float StepInterval = 0.32f;

    /// <summary>横向速度超过这个值才算在走，站着不动不响。</summary>
    [Export] public float StepSpeedThreshold = 25f;

    // ── 冒泡 ──────────────────────────────────────────────────
    [ExportGroup("冒泡")]
    /// <summary>
    /// 泥浆表面相对本节点原点的 Y 偏移。
    /// 节点直接放在泥浆面上就留 0；如果为了覆盖玩家跳跃把判定区往上抬了，
    /// 这里填抬高的量，冒泡和飞溅才会落在真正的泥面上。
    /// </summary>
    [Export] public float SurfaceOffsetY = 0f;

    [Export] public bool EnableBubbles = true;
    [Export] public int BubbleAmount = 90;
    [Export] public float BubbleLifetime = 1.9f;
    [Export] public float BubbleRiseSpeed = 14f;

    /// <summary>泡泡上升方向的散角（度）。0 = 笔直往上，太大像冒烟。</summary>
    [Export(PropertyHint.Range, "0,90,1")] public float BubbleSpread = 22f;
    [Export] public float BubbleScaleMin = 0.5f;
    [Export] public float BubbleScaleMax = 1.2f;

    private readonly List<Box> _inside = new();

    /// <summary>每个玩家上一帧是不是站在地面上。用来抓「起跳」和「落回」这两个瞬间。</summary>
    private readonly Dictionary<Box, bool> _wasOnFloor = new();

    private CpuParticles2D _bubbles;
    private CpuParticles2D _splashHard;
    private CpuParticles2D _splashSoft;
    private float _enterCd;
    private float _stepTimer;
    private float _halfWidth = 200f;

    public override void _Ready()
    {
        // 只负责「发现玩家」，不参与物理阻挡（阻挡是 StaticBody 的事）
        CollisionLayer = 0;
        CollisionMask = 2;
        Monitoring = true;

        _halfWidth = MeasureHalfWidth();

        BuildBubbles();
        _splashHard = MakeSplashEmitter("SplashHard", SplashAmount, SplashSpeed);
        AddChild(_splashHard);
        _splashSoft = MakeSplashEmitter("SplashSoft", SoftSplashAmount, SoftSplashSpeed);
        AddChild(_splashSoft);

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_enterCd > 0f) _enterCd -= dt;

        // 玩家可能在泥潭里被销毁（死亡重开 / 切场景），这里顺手清掉
        _inside.RemoveAll(p => p == null || !IsInstanceValid(p));

        if (_inside.Count == 0)
        {
            _stepTimer = 0f;
            return;
        }

        var player = _inside[0];

        // ── 起跳 / 落回 ──
        // 泥里跳起来要把泥蹬起来，落回去要再砸一坨。
        // 用「上一帧在地面、这一帧不在」来抓起跳瞬间，比读按键可靠
        // （跳跃有缓冲和土狼时间，按键那一刻不一定真的离地）。
        bool onFloor = player.IsOnFloor();
        bool wasOnFloor = _wasOnFloor.TryGetValue(player, out bool prev) ? prev : onFloor;
        _wasOnFloor[player] = onFloor;

        if (wasOnFloor && !onFloor && player.Velocity.Y < -20f)
        {
            EmitSplash(_splashHard, player.GlobalPosition.X, JumpSplashScale);
            AudioManager.Instance?.PlaySfx(SplashSfx, SplashVolume * 0.85f,
                                           1.12f + GD.Randf() * 0.12f);
        }
        else if (!wasOnFloor && onFloor)
        {
            EmitSplash(_splashHard, player.GlobalPosition.X, 1f);
            AudioManager.Instance?.PlaySfx(SplashSfx, SplashVolume * 0.95f,
                                           0.90f + GD.Randf() * 0.12f);
        }

        bool walking = onFloor && Mathf.Abs(player.Velocity.X) > StepSpeedThreshold;

        if (!walking)
        {
            _stepTimer = 0f;
            return;
        }

        _stepTimer -= dt;
        if (_stepTimer > 0f) return;

        _stepTimer = StepInterval;
        PlayStep(player);
    }

    public override void _ExitTree()
    {
        // 场景卸载时把标记还回去，否则玩家的计数会一直挂着，下次进场景还在「泥里」
        foreach (var p in _inside)
        {
            if (p != null && IsInstanceValid(p)) p.ExitMud(this);
        }
        _inside.Clear();
        _wasOnFloor.Clear();
    }

    // ── 判定 ──────────────────────────────────────────────────

    private void OnBodyEntered(Node2D body)
    {
        if (body is not Box player) return;
        if (_inside.Contains(player)) return;

        _inside.Add(player);
        player.EnterMud(this);

        // 记一下初始落地状态，免得刚进判定区就被误判成「落回」
        _wasOnFloor[player] = player.IsOnFloor();

        if (_enterCd > 0f) return;
        _enterCd = EnterCooldown;

        // 从高处砸下来给大反馈，从地面走进来给小反馈
        bool heavy = player.Velocity.Y > HeavyFallSpeed;
        var emitter = heavy ? _splashHard : _splashSoft;
        float speedScale = heavy ? 1f : 0.6f;

        EmitSplash(emitter, player.GlobalPosition.X);
        AudioManager.Instance?.PlaySfx(
            SplashSfx,
            SplashVolume * (heavy ? 1f : 0.6f),
            (heavy ? 0.92f : 1.08f) + GD.Randf() * 0.12f);
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is not Box player) return;
        if (!_inside.Remove(player)) return;
        _wasOnFloor.Remove(player);
        player.ExitMud(this);
    }

    // ── 反馈 ──────────────────────────────────────────────────

    private void PlayStep(Box player)
    {
        EmitSplash(_splashSoft, player.GlobalPosition.X);
        AudioManager.Instance?.PlaySfx(
            StepSfx,
            StepVolume * (0.85f + GD.Randf() * 0.3f),
            0.9f + GD.Randf() * 0.25f);
    }

    /// <summary>把粒子发射点挪到玩家脚下再爆一次。</summary>
    private void EmitSplash(CpuParticles2D emitter, float worldX, float scaleBoost = 1f)
    {
        if (emitter == null || !IsInstanceValid(emitter)) return;

        // 夹在泥潭范围内，免得玩家在边界外触发时泥点从天上冒出来
        float localX = Mathf.Clamp(worldX - GlobalPosition.X, -_halfWidth + 8f, _halfWidth - 8f);
        emitter.Position = new Vector2(localX, SurfaceOffsetY);

        // 想溅得更多时放大粒子而不是改 Amount —— 改 Amount 会重建整个粒子池，代价太大
        emitter.ScaleAmountMin = SplashScaleMin * scaleBoost;
        emitter.ScaleAmountMax = SplashScaleMax * scaleBoost;

        emitter.Restart();
    }

    // ── 粒子构建 ──────────────────────────────────────────────

    private void BuildBubbles()
    {
        if (!EnableBubbles) return;

        var tex = GD.Load<Texture2D>("res://assets/jungle/bubble.png");
        if (tex == null)
        {
            GD.Print("[MudPit] 找不到 bubble.png，跳过冒泡");
            return;
        }

        // 生命周期内：先保持可见，最后两成时间快速淡出 —— 模拟「冒到表面破掉」
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(1f, 1f, 1f, 1f));
        ramp.SetColor(1, new Color(1f, 1f, 1f, 0f));
        ramp.AddPoint(0.78f, new Color(1f, 1f, 1f, 0.9f));

        _bubbles = new CpuParticles2D
        {
            Name = "Bubbles",
            Texture = tex,
            Amount = BubbleAmount,
            Lifetime = BubbleLifetime,
            Preprocess = 2.5f,          // 进场景时泥潭已经是「冒了一阵」的状态，不是空的
            Position = new Vector2(0f, SurfaceOffsetY + 4f),   // 略低于泥面，看起来是从泥里顶出来的
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(Mathf.Max(20f, _halfWidth - 12f), 4f),
            Direction = new Vector2(0f, -1f),
            Spread = BubbleSpread,
            Gravity = new Vector2(0f, -6f),
            InitialVelocityMin = BubbleRiseSpeed * 0.45f,
            InitialVelocityMax = BubbleRiseSpeed,
            ScaleAmountMin = BubbleScaleMin,
            ScaleAmountMax = BubbleScaleMax,
            ColorRamp = ramp,
            ZIndex = 1,                 // 在泥浆贴图之上、玩家之下
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        AddChild(_bubbles);
    }

    private CpuParticles2D MakeSplashEmitter(string name, int amount, float speed)
    {
        var tex = GD.Load<Texture2D>("res://assets/jungle/mud_drop.png");

        return new CpuParticles2D
        {
            Name = name,
            Texture = tex,
            Amount = Mathf.Max(1, amount),
            Lifetime = SplashLifetime,
            OneShot = true,
            Explosiveness = 1f,         // 一帧内全部喷出，才有「砸下去」的爆发感
            Emitting = false,
            Direction = new Vector2(0f, -1f),
            Spread = 70f,
            Gravity = new Vector2(0f, 900f),    // 泥块有重量：飞起约 35px 再落回泥面
            InitialVelocityMin = speed * 0.55f,
            InitialVelocityMax = speed,
            ScaleAmountMin = SplashScaleMin,
            ScaleAmountMax = SplashScaleMax,
            ZIndex = 2,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
    }

    /// <summary>从子节点里找碰撞形状，取半宽。找不到就给个默认值。</summary>
    private float MeasureHalfWidth()
    {
        foreach (var child in GetChildren())
        {
            if (child is CollisionShape2D cs)
            {
                if (cs.Shape is RectangleShape2D rect) return rect.Size.X * 0.5f;
                if (cs.Shape is CapsuleShape2D cap) return cap.Radius;
                if (cs.Shape is CircleShape2D cir) return cir.Radius;
            }
            if (child is CollisionPolygon2D poly)
            {
                float minX = float.MaxValue, maxX = float.MinValue;
                foreach (var pt in poly.Polygon)
                {
                    minX = Mathf.Min(minX, pt.X);
                    maxX = Mathf.Max(maxX, pt.X);
                }
                if (maxX > minX) return (maxX - minX) * 0.5f;
            }
        }
        return 200f;
    }
}
