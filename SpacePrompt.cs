using Godot;

/// <summary>
/// 被抓时的「狂按空格挣脱」提示。
///
/// 样式对齐 e-marker-64.png 那套 E 键标：银灰键帽 + 深灰描边 + 深色字，
/// 底下留一条厚度当 3D 边。键帽会自己按节奏「按下去 → 弹回来」，
/// 用动作暗示玩家「这是要连点的」，而不是单纯摆个图标。
///
/// 挂在 box 下面即可，它自己读玩家的 Grabbed / StruggleProgress。
/// 位置：摆在**斜上方**（身体侧面之外 + 头顶略高），并自动挑「背离怪」的那一侧。
/// 注意别把键帽放回中轴线上 —— 只要它的边缘越过身体中线，看着就又变回「正上方」了。
/// </summary>
public partial class SpacePrompt : Control
{
    [ExportGroup("尺寸")]
    [Export] public float CapWidth = 26f;
    [Export] public float CapHeight = 14f;
    [Export] public float CapThickness = 2f;      // 键帽底部厚度（3D 边）
    [Export] public int CornerRadius = 3;
    [Export] public int FontSize = 7;

    [ExportGroup("位置")]
    [Export] public float AnchorOffsetY = -46f;   // 键帽顶边离身体中心多高（负数是往上）
    [Export] public float BodyHalfWidth = 14f;    // 身体半边宽，键帽从这条线往外摆
    [Export] public float SideGap = 4f;           // 再往外让开多少

    [ExportGroup("节奏")]
    [Export] public float PressPeriod = 0.34f;    // 每这么多秒「按」一下
    [Export] public float PressDepth = 2.5f;      // 按下去多少像素

    [ExportGroup("配色（参考 E 键标）")]
    [Export] public Color CapColor = new(0.84f, 0.84f, 0.85f);
    [Export] public Color EdgeColor = new(0.27f, 0.27f, 0.29f);
    [Export] public Color TextColor = new(0.17f, 0.17f, 0.19f);
    [Export] public Color BarBackColor = new(1f, 1f, 1f, 0.28f);   // 空槽：淡白，深色背景上才看得见
    [Export] public Color BarFillColor = new(0.97f, 0.97f, 0.98f);

    private const float BarHeight = 3f;
    private const float BarGap = 2f;

    private StyleBoxFlat _cap;
    private StyleBoxFlat _edge;
    private Box _player;
    private float _t;

    public override void _Ready()
    {
        _player = GetParent() as Box;
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new Vector2(CapWidth, CapHeight + CapThickness + BarGap + BarHeight);

        _cap = MakeStyle(CapColor, EdgeColor, CornerRadius, 2);
        _edge = MakeStyle(EdgeColor, EdgeColor, CornerRadius, 0);

        Visible = false;
    }

    public override void _Process(double delta)
    {
        bool grab = _player != null && IsInstanceValid(_player) && _player.Grabbed;
        if (grab != Visible)
        {
            Visible = grab;
            _t = 0f;                 // 每次被抓都从「第一下」开始按
        }
        if (!Visible) return;

        // 摆在斜上方：整个键帽挪到身体侧面之外，别再压在中轴线上（那样看着就是「正上方」）。
        // 左右挑「背离怪」的那一侧 —— 放怪那边会被身体和紫烟糊住。
        float side = _player.GrabSource.X >= _player.GlobalPosition.X ? -1f : 1f;
        float outX = BodyHalfWidth + SideGap;
        Position = new Vector2(side < 0f ? -(outX + CapWidth) : outX, AnchorOffsetY);

        _t += (float)delta;
        QueueRedraw();
    }

    /// <summary>这一瞬间键帽被按下多少：0 = 完全弹起，1 = 按到底。</summary>
    private float PressAmount()
    {
        float k = (_t % PressPeriod) / PressPeriod;
        if (k < 0.18f) return k / 0.18f;                       // 快速按下
        if (k < 0.42f) return 1f - (k - 0.18f) / 0.24f;        // 弹回来
        return 0f;                                             // 剩下的时间停着
    }

    public override void _Draw()
    {
        float press = PressAmount() * PressDepth;

        // 键帽底部的厚度（不动，露出来的那一条就是「侧面」）
        DrawStyleBox(_edge, new Rect2(0f, CapThickness, CapWidth, CapHeight));

        // 键帽本体（跟着 press 往下走）
        DrawStyleBox(_cap, new Rect2(0f, press, CapWidth, CapHeight));

        // SPACE 字样：手动画居中，不用额外挂 Label
        Font font = ThemeDB.FallbackFont;
        Vector2 textSize = font.GetStringSize("SPACE", HorizontalAlignment.Left, -1f, FontSize);
        DrawString(font,
            new Vector2((CapWidth - textSize.X) * 0.5f, press + CapHeight * 0.5f + textSize.Y * 0.36f),
            "SPACE", HorizontalAlignment.Left, -1f, FontSize, TextColor);

        // 底下一条进度：按了几下 / 要按几下
        float barY = CapHeight + CapThickness + BarGap;
        float progress = _player != null && IsInstanceValid(_player) ? _player.StruggleProgress : 0f;

        DrawRect(new Rect2(0f, barY, CapWidth, BarHeight), BarBackColor);
        if (progress > 0.001f)
            DrawRect(new Rect2(0f, barY, CapWidth * progress, BarHeight), BarFillColor);
    }

    private static StyleBoxFlat MakeStyle(Color fill, Color border, int radius, int borderWidth)
    {
        return new StyleBoxFlat
        {
            BgColor = fill,
            BorderColor = border,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            AntiAliasing = true,
        };
    }
}
