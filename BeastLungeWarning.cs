using Godot;

/// <summary>
/// 技能 1「横贯飞扑」的地面安全圈。
///
/// 只有一样东西：地面上一个绿色扁椭圆（俯视的圆，画成透视压扁的样子）。
/// 站在圈里按 C 蹲下就能躲过这一扑。
///
/// 为什么不再画红色轨道 / 圆柱 / 进度条：
///   那些东西把整个战场糊满了，反而看不清圈在哪。玩家真正需要的信息只有一个——
///   「往哪站、什么时候蹲」。所以只留地面这个圈，靠闪烁和呼吸告诉玩家「就是现在」。
///
/// 为什么用 _Draw 而不是贴图：
///   圈的宽度必须等于 ArenaRect 宽 × DodgeZoneRatio。老大以后改这两个参数，
///   画出来的圈要跟着变，贴图做不到。
///
/// 由 BeastAppleBoss 在代码里创建，蓄力结束调 StopWarning 收掉。
/// </summary>
public partial class BeastLungeWarning : Node2D
{
    [Export] public Color ZoneFill = new Color(0.25f, 1f, 0.40f, 0.20f);
    [Export] public Color ZoneLine = new Color(0.40f, 1f, 0.50f, 0.95f);
    [Export] public float BlinkSpeed = 7f;      // 闪烁快慢
    [Export] public float Squash = 0.24f;       // 椭圆的扁度（1 = 正圆，0.24 = 贴地看）

    private float _groundY;
    private float _centerX;
    private float _halfW;
    private float _duration = 3f;
    private float _t;
    private bool _running;

    /// <summary>由 Boss 调用。所有坐标都是世界坐标，节点本身摆在 (0,0)。</summary>
    public void Setup(float groundY, float centerX, float halfWidth, float duration)
    {
        _groundY = groundY;
        _centerX = centerX;
        _halfW = Mathf.Max(8f, halfWidth);
        _duration = Mathf.Max(0.05f, duration);
        _t = 0f;
        _running = true;

        GlobalPosition = Vector2.Zero;
        ZIndex = 8;          // 盖在地形之上，但在玩家之下
        QueueRedraw();
    }

    public void StopWarning()
    {
        _running = false;
        QueueFree();
    }

    public override void _Process(double delta)
    {
        if (!_running) return;
        _t += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_running) return;

        float progress = Mathf.Clamp(_t / _duration, 0f, 1f);
        // 越接近起跳闪得越快，但不会闪到看不见（最低留 0.45 的亮度）
        float blink = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(_t * (BlinkSpeed + progress * 9f)));
        var center = new Vector2(_centerX, _groundY);
        float rx = _halfW;
        float ry = _halfW * Squash;

        // 1) 半透明填充：一个压扁的椭圆
        DrawEllipse(center, rx, ry, new Color(ZoneFill.R, ZoneFill.G, ZoneFill.B, ZoneFill.A * blink), 0f, true);

        // 2) 外圈描边：亮绿色，闪烁
        var line = new Color(ZoneLine.R, ZoneLine.G, ZoneLine.B, ZoneLine.A * blink);
        DrawEllipse(center, rx, ry, line, 3f);

        // 3) 里圈：跟着蓄力进度往外扩一圈，给一个「还有多久」的读感，
        //    但只是一个细环，不是进度条
        float grow = 0.55f + 0.42f * progress;
        var inner = new Color(ZoneLine.R, ZoneLine.G, ZoneLine.B, 0.55f * blink);
        DrawEllipse(center, rx * grow, ry * grow, inner, 2f);
    }

    /// <summary>画一个椭圆。fill = true 时填实，否则只描边。</summary>
    private void DrawEllipse(Vector2 center, float rx, float ry, Color color, float width, bool fill = false)
    {
        const int Seg = 48;
        if (fill)
        {
            var pts = new Vector2[Seg];
            for (int i = 0; i < Seg; i++)
            {
                float a = Mathf.Tau * i / Seg;
                pts[i] = center + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
            }
            DrawColoredPolygon(pts, color);
            return;
        }

        var ring = new Vector2[Seg + 1];
        for (int i = 0; i <= Seg; i++)
        {
            float a = Mathf.Tau * i / Seg;
            ring[i] = center + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
        }
        DrawPolyline(ring, color, width, true);
    }
}
