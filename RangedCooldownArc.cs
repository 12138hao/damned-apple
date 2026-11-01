using Godot;

/// <summary>
/// 远程武器（粉笔）的内置冷却弧形指示条。用 _Draw 画，不需要贴图。
/// 画法照抄 ChargeArc（同样的底槽 + 填充 + 圆角弧），只改两处行为：
///
///   1. 冷却没走完时**底槽一直画出来**。
///      ChargeArc 是「进度 0 就整段不画」，照搬过来的话刚射完那一瞬间是空的，
///      看不到"这次 CD 一共有多长"，要等填充长出来才知道。这里反过来：
///      先画满整条槽，再用亮色填出已经恢复的部分。
///   2. 弧的总角度由冷却时长算出来：SweepDeg = 冷却秒数 × DegPerSecond。
///      所以改 WeaponData.RangedCooldown 之后，弧会自动变长/变短，
///      但会被 MaxSweepDeg 卡住，永远是个弧，不会变成整圈。
///
/// 朝向不归这个脚本管：Box 那边按人物朝向设置 Position 和 Scale.X（-1 就是镜像到右边）。
/// </summary>
public partial class RangedCooldownArc : Node2D
{
    [ExportGroup("形状")]
    [Export] public float Radius = 22f;          // 弧的半径（像素）
    [Export] public float Width = 5f;            // 弧的粗细
    [Export] public float StartDeg = 135f;       // 起始角度（度，0 = 向右，逆时针为正）；填充从这条边开始长
    [Export] public float DegPerSecond = 80f;    // 每 1 秒冷却对应多少度（1.5s → 120°）
    [Export] public float MinSweepDeg = 40f;     // 下限，CD 再短也留一小段，不然看不见
    [Export] public float MaxSweepDeg = 270f;    // 上限，CD 再长也不会画成整圈

    [ExportGroup("颜色")]
    [Export] public Color BgColor = new Color(0.08f, 0.08f, 0.12f, 0.9f);
    [Export] public Color FillColor = new Color(0.72f, 0.88f, 1f);   // 恢复中：粉笔的浅蓝白
    [Export] public Color FullColor = new Color(1f, 0.82f, 0.2f);    // 刚恢复满（下一帧就消失了）

    private bool _active;
    private float _progress;      // 0~1，已恢复的比例
    private float _sweepDeg = 120f;

    /// <summary>现在是否正在显示（冷却中）。</summary>
    public bool IsActive => _active;

    /// <summary>已恢复的比例 0~1。</summary>
    public float Progress => _progress;

    /// <summary>这条弧当前的总角度（由冷却时长换算来的）。</summary>
    public float SweepDegrees => _sweepDeg;

    /// <summary>收起并清空（冷却结束 / 换武器 / 空手时调用）。</summary>
    public void Clear()
    {
        if (!_active) return;
        _active = false;
        QueueRedraw();
    }

    /// <summary>
    /// 每帧刷新。<paramref name="total"/> = 这次冷却一共多长（秒），
    /// <paramref name="remaining"/> = 还剩多少（秒）。
    /// </summary>
    public void SetCooldown(float total, float remaining)
    {
        // 冷却结束或没有冷却：整条弧消失
        if (total <= 0.0001f || remaining <= 0.0001f)
        {
            Clear();
            return;
        }

        _active = true;
        _progress = Mathf.Clamp(1f - remaining / total, 0f, 1f);
        _sweepDeg = Mathf.Clamp(total * DegPerSecond, MinSweepDeg, MaxSweepDeg);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_active) return;

        float start = Mathf.DegToRad(StartDeg);
        float sweep = Mathf.DegToRad(_sweepDeg);

        // 弧越长分段越多，否则长弧会画成折线
        int points = Mathf.Clamp((int)(_sweepDeg * 0.4f), 12, 128);

        // 底槽：整段冷却长度都画出来 = "这次 CD 一共多长"
        DrawArc(Vector2.Zero, Radius, start, start + sweep, points, BgColor, Width);

        // 填充：已经恢复的部分，从起始边往结束边长
        if (_progress > 0.001f)
        {
            Color c = _progress >= 0.999f ? FullColor : FillColor;
            DrawArc(Vector2.Zero, Radius, start, start + sweep * _progress, points, c, Width - 2f);
        }
    }
}
