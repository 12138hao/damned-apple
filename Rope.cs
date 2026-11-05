using Godot;

/// <summary>
/// 可攀爬的绳索。挂在 Area2D 上，玩家走进判定区就能按 W 往上爬。
///
/// 它同时是「可攀爬物」的基类：<see cref="Vine"/> 继承它，
/// 只覆盖「长度怎么算、上下多快、到顶抛不抛、空中抓不抓」这几条，
/// 判定区/提示/吸附这些公用逻辑不用重写。
///
/// 判定和移动是分开的：
///   · 这里（Area2D）只负责「玩家在不在绳子范围内」，并把自己登记到 Box.CurrentRope；
///   · 真正让玩家上下移动的是 Box.Move.cs 里的 UpdateClimb。
/// 这么拆是因为移动必须走 Box 自己的 Velocity + MoveAndSlide，
/// 让 Area2D 去推 CharacterBody2D 会跟重力、碰撞打架。
///
/// 视觉：子节点 Sprite2D 用 assets/rope.png（12×500 的像素麻绳），
/// 子节点 WMarker 用 EMarker.cs 那个浮动闪烁脚本，只把贴图换成 W 键帽。
/// </summary>
public partial class Rope : Area2D
{
    [ExportGroup("攀爬")]
    [Export] public float ClimbSpeed = 220f;      // 上下爬的速度（像素/秒）
    [Export] public float TopY = 0f;              // 绳顶世界 Y：玩家中心爬到这就自动松手
    [Export] public float BottomY = 500f;         // 绳底世界 Y：踩到地面就松手
    [Export] public float SnapSpeed = 320f;       // 抓绳时向绳子中线吸附的最大速度
    [Export] public float SnapStrength = 9f;      // 吸附强度，越大贴得越紧

    [ExportGroup("提示")]
    [Export] public NodePath HintPath = "WMarker";   // W 键帽节点（默认子节点）
    [Export] public float HintShowDistance = 120f;   // 玩家离绳底多近才显示 W 提示

    private Box _player;
    private Sprite2D _hint;
    private bool _inside;

    /// <summary>玩家是否在绳子判定区内。Box 每帧查这个。</summary>
    public bool PlayerInRange => _inside && _player != null && IsInstanceValid(_player);

    /// <summary>绳子中线的世界 X。攀爬时玩家会被吸附到这条线上。</summary>
    public virtual float RopeX => GlobalPosition.X;

    // ── 给 Box.UpdateClimb 用的接口 ────────────────────────────
    // 这些必须是 virtual 属性，不能直接让 Box 去读字段：
    //   绳索的语义是「节点在绳子中点，TopY/BottomY 是写死的世界坐标」（老场景按这个配的）；
    //   藤蔓的语义是「节点在藤蔓顶端，Length 往下量」。
    // 两种摆法完全不同，所以把「怎么算」交给各自的类，Box 只问结果。

    /// <summary>爬到哪个 Y（玩家中心）就算到顶。</summary>
    public virtual float ClimbTopY => TopY;

    /// <summary>爬到哪个 Y（玩家中心）就算到底。</summary>
    public virtual float ClimbBottomY => BottomY;

    /// <summary>往上爬的速度。</summary>
    public virtual float UpSpeed => ClimbSpeed;

    /// <summary>往下滑的最终速度（藤蔓比绳索快很多）。</summary>
    public virtual float DownSpeed => ClimbSpeed;

    /// <summary>下滑的加速度：从 0 加速到 DownSpeed，做出「溜下去」的手感。</summary>
    public virtual float DownAccel => ClimbSpeed * 4f;

    /// <summary>蹬开时的横向初速度。0 = 保持当前横向速度（绳索的老行为）。</summary>
    public virtual float JumpOffSpeed => 0f;

    /// <summary>蹬开时往上弹多少（乘 jumpVelocity）。</summary>
    public virtual float JumpOffUp => 0.75f;

    /// <summary>爬到顶端是「被抛出去」还是「停住」。绳索要抛（上面是传送门），藤蔓要停。</summary>
    public virtual bool TossAtTop => true;

    /// <summary>空中碰到就自动抓（不按 W 也抓）。换藤全靠这个。</summary>
    public virtual bool AutoGrabInAir => false;

    public override void _Ready()
    {
        Monitoring = true;
        _hint = GetNodeOrNull<Sprite2D>(HintPath);
        if (_hint != null) _hint.Visible = false;

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not Box box) return;
        _player = box;
        _inside = true;
        box.CurrentRope = this;
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is not Box box) return;
        _inside = false;
        if (box.CurrentRope == this) box.CurrentRope = null;
        if (_hint != null) _hint.Visible = false;
    }

    public override void _Process(double delta)
    {
        if (_hint == null) return;
        if (_player == null || !IsInstanceValid(_player) || !_inside)
        {
            if (_hint.Visible) _hint.Visible = false;
            return;
        }

        // 提示只在「还没抓住绳子」时显示：已经爬上去就没必要再提示按 W。
        // 距离按**底端**算，不是按节点中心算 —— 绳子 500 高，用中心会算成 227px，
        // 站在地上的玩家永远看不到提示。藤蔓同理，所以走 ClimbBottomY。
        var foot = new Vector2(GlobalPosition.X, ClimbBottomY);
        bool want = !_player.IsClimbing
                    && _player.GlobalPosition.DistanceTo(foot) <= HintShowDistance;
        if (want != _hint.Visible) _hint.Visible = want;
    }
}
