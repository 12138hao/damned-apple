using Godot;

/// <summary>
/// 实时投影阴影：把目标的精灵压扁 + 斜切 + 压暗，投到脚下的地面上。
///
/// 为什么不用 Light2D 的方向光阴影：
/// 方向光的影子是无限长的，场景里一百多棵树会把整片草地都罩进阴影，
/// 变成一条条黑带，反而更假。这里用"接触影"的做法 —— 影子短、贴合脚下、
/// 能在空中变化（跳起来影子变小变淡），是像素横版游戏的常规解法。
///
/// 用法：挂在角色底下，TargetPath 指向要投影的 Sprite2D 就行。
/// 以后加小怪，复制这个节点、改一下 GroundY 就有了。
/// </summary>
[Tool]
public partial class ShadowCaster : Node2D
{
    [Export] public bool Enabled = true;
    [Export] public NodePath TargetPath;               // 要投影的精灵；留空则自动找父节点下的 Sprite2D
    [Export] public float GroundY = 700f;              // 影子落在哪条世界水平线上
    // 地面不是一条水平线时（斜坡、藤蔓、平台）勾这个：影子跟着脚走，不看 GroundY
    [Export] public bool SnapToFeet = false;
    [Export] public float Squash = 0.34f;              // 垂直压扁多少（0.34 = 压到三成高）
    [Export] public float WidthScale = 1.45f;          // 水平略微撑开，影子要比脚宽才看得出来
    [Export] public float SinkY = 10f;                 // 往下沉一点，别被自己身体挡住
    [Export] public float SkewDeg = -20f;              // 斜切角度：太阳在右边，影子往左边倒
    [Export] public Color Tint = new Color(0.05f, 0.04f, 0.14f, 0.62f);
    [Export] public Vector2 Offset = Vector2.Zero;     // 相对脚下的额外偏移

    [ExportGroup("离地衰减")]
    [Export] public float MaxAirHeight = 240f;         // 离地这么高时影子最淡最小
    [Export] public float MinAlpha = 0.10f;
    [Export] public float MinScale = 0.55f;

    private Sprite2D _src;
    private Sprite2D _shadow;

    public override void _Ready()
    {
        // 影子要压在世界坐标的地面上，不受角色父级变换影响
        TopLevel = true;
        ZAsRelative = false;
        ZIndex = -40;          // 在草地(-60)之上、角色(10)之下

        _shadow = new Sprite2D
        {
            Name = "ShadowArt",
            Centered = true,
            Modulate = Tint,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        AddChild(_shadow);

        ResolveTarget();
        SetProcess(true);
    }

    private void ResolveTarget()
    {
        if (TargetPath != null && !TargetPath.IsEmpty)
        {
            _src = GetNodeOrNull<Sprite2D>(TargetPath);
            if (_src != null) return;
        }

        // 没指定就找父节点下的第一个 Sprite2D
        var parent = GetParent();
        if (parent == null) return;
        foreach (var c in parent.GetChildren())
        {
            if (c is Sprite2D s) { _src = s; break; }
        }
        if (_src == null) _src = parent.GetNodeOrNull<Sprite2D>("Sprite2D");
    }

    public override void _Process(double delta)
    {
        if (!Enabled || _src == null || _shadow == null) return;

        // 跟着目标的贴图和当前帧走，动画换了影子也跟着换
        _shadow.Texture = _src.Texture;
        _shadow.Hframes = _src.Hframes;
        _shadow.Vframes = _src.Vframes;
        _shadow.Frame = _src.Frame;
        _shadow.FlipH = _src.FlipH;
        _shadow.Offset = Vector2.Zero;     // 不抄目标的 offset，否则影子会浮在脚上方

        // "脚底"= 精灵中心 + 半高 + offset（精灵是居中的）
        float halfH = _src.Texture != null ? _src.Texture.GetHeight() * Mathf.Abs(_src.Scale.Y) * 0.5f : 0f;
        float feetY = _src.GlobalPosition.Y + _src.Offset.Y + halfH;
        float baseY = SnapToFeet ? feetY : GroundY;
        float air = Mathf.Max(0f, baseY - feetY);          // 离地高度

        float t = MaxAirHeight > 0f ? Mathf.Clamp(air / MaxAirHeight, 0f, 1f) : 0f;
        float alpha = Mathf.Lerp(Tint.A, MinAlpha, t);
        float widen = Mathf.Lerp(1f, MinScale, t);

        _shadow.Modulate = new Color(Tint.R, Tint.G, Tint.B, alpha);
        _shadow.Skew = Mathf.DegToRad(SkewDeg);
        // 水平撑开、垂直压扁；离地越高整体缩得越小
        _shadow.Scale = new Vector2(widen * WidthScale, widen * Squash);
        _shadow.GlobalPosition = new Vector2(_src.GlobalPosition.X, baseY + SinkY) + Offset;
    }
}
