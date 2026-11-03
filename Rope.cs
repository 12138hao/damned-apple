using Godot;

/// <summary>
/// 可攀爬的绳索。挂在 Area2D 上，玩家走进判定区就能按 W 往上爬。
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
    public float RopeX => GlobalPosition.X;

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
        // 距离按绳底算，不是按节点中心算 —— 绳子 500 高，用中心会算成 227px，
        // 站在地上的玩家永远看不到提示。
        var foot = new Vector2(GlobalPosition.X, BottomY);
        bool want = !_player.IsClimbing
                    && _player.GlobalPosition.DistanceTo(foot) <= HintShowDistance;
        if (want != _hint.Visible) _hint.Visible = want;
    }
}
