using Godot;

/// <summary>
/// 毒液投放的轨道提示：从 Boss 嘴边拉一条抛物线到落点，落点画一个椭圆警示圈，
/// 圈内的倒计时收完之后才真正生成毒滩。
///
/// 为什么要有它：原来的喷毒是「瞬间出现在脚下」，玩家只能靠记位置躲，
/// 没有任何反应时间。加了轨道提示之后，看到弧线和正在收缩的圈就知道该往哪躲。
///
/// 由 BossEnemy 在代码里创建并 Setup，不需要做成场景。
/// </summary>
public partial class SpitMarker : Node2D
{
    [ExportGroup("外观")]
    [Export] public Color ArcColor = new Color(0.95f, 0.55f, 0.30f, 0.80f);
    [Export] public Color RingColor = new Color(1f, 0.42f, 0.28f, 0.95f);
    [Export] public Color FillColor = new Color(1f, 0.42f, 0.28f, 0.14f);
    [Export] public float LineWidth = 2.5f;
    [Export] public float RingSquashY = 0.32f;    // 警示圈的扁度，跟毒滩形状对齐（毒滩也是扁的）
    [Export] public float ArcHeight = 170f;       // 抛物线拱高

    private const int EllipseSegments = 48;
    private const int ArcSegments = 28;

    private Vector2 _origin;       // 抛出点（Boss 嘴，世界坐标）
    private Vector2 _land;         // 落点（世界坐标）
    private float _radius = 140f;
    private float _warnTime = 0.8f;
    private float _t;
    private bool _landed;
    private System.Action<Vector2> _onLand;

    /// <summary>由 BossEnemy 调用。onLand 在倒计时结束时收到落点坐标。</summary>
    public void Setup(Vector2 origin, Vector2 land, float radius,
                      float warnTime, System.Action<Vector2> onLand)
    {
        _origin = origin;
        _land = land;
        _radius = Mathf.Max(8f, radius);
        _warnTime = Mathf.Max(0.05f, warnTime);
        _onLand = onLand;

        // 节点放在落点，绘制时用「世界坐标 - 落点」换算成局部坐标，数字小、好调试
        GlobalPosition = land;
        ZIndex = 5;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_landed) return;

        _t += (float)delta;
        QueueRedraw();

        if (_t >= _warnTime)
        {
            _landed = true;
            _onLand?.Invoke(_land);
            QueueFree();
        }
    }

    public override void _Draw()
    {
        float k = Mathf.Clamp(_t / _warnTime, 0f, 1f);

        DrawArc(k);
        DrawWarningRing(k);
    }

    /// <summary>抛物线：从抛出点画到落点，长度跟着倒计时一起长出来。</summary>
    private void DrawArc(float k)
    {
        Vector2 p0 = _origin - _land;
        Vector2 p2 = Vector2.Zero;
        // 控制点抬高：起点和终点的中点再往上抬 ArcHeight，形成拱形
        Vector2 p1 = (p0 + p2) * 0.5f + new Vector2(0f, -ArcHeight);

        int count = Mathf.Max(2, Mathf.RoundToInt(ArcSegments * k));
        var pts = new Vector2[count + 1];
        for (int i = 0; i <= count; i++)
        {
            float t = (float)i / count * k;
            float u = 1f - t;
            pts[i] = u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }

        DrawPolyline(pts, ArcColor, LineWidth, true);

        // 弧线头部点一个小球，像正在飞的那滴毒液
        if (pts.Length > 0)
            DrawCircle(pts[pts.Length - 1], 4.5f, RingColor);
    }

    /// <summary>落点警示圈：外圈固定，内圈随倒计时收缩到零。</summary>
    private void DrawWarningRing(float k)
    {
        float rx = _radius;
        float ry = _radius * RingSquashY;

        // 半透明填充，让玩家一眼看到危险范围
        DrawColoredPolygon(EllipsePoints(Vector2.Zero, rx, ry), FillColor);
        DrawEllipseOutline(Vector2.Zero, rx, ry, RingColor, LineWidth);

        // 倒计时：内圈从满缩到 0，收完了就落地
        float inner = 1f - k;
        if (inner > 0.02f)
        {
            var innerColor = new Color(RingColor.R, RingColor.G, RingColor.B, 0.55f);
            DrawEllipseOutline(Vector2.Zero, rx * inner, ry * inner, innerColor, 1.5f);
        }
    }

    private Vector2[] EllipsePoints(Vector2 center, float rx, float ry)
    {
        var pts = new Vector2[EllipseSegments];
        for (int i = 0; i < EllipseSegments; i++)
        {
            float ang = Mathf.Tau * i / EllipseSegments;
            pts[i] = center + new Vector2(Mathf.Cos(ang) * rx, Mathf.Sin(ang) * ry);
        }
        return pts;
    }

    private void DrawEllipseOutline(Vector2 center, float rx, float ry, Color color, float width)
    {
        var pts = new Vector2[EllipseSegments + 1];
        for (int i = 0; i <= EllipseSegments; i++)
        {
            float ang = Mathf.Tau * i / EllipseSegments;
            pts[i] = center + new Vector2(Mathf.Cos(ang) * rx, Mathf.Sin(ang) * ry);
        }
        DrawPolyline(pts, color, width, true);
    }
}
