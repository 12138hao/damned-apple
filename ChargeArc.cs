using Godot;

/// <summary>
/// 角色左侧的弧形蓄力条。用 _Draw 直接画，不需要贴图。
/// 挂在 box 下，位置调到角色左侧即可。
/// </summary>
public partial class ChargeArc : Node2D
{
    [Export] public float Radius = 26f;      // 弧的半径（像素）
    [Export] public float Width = 6f;        // 弧的粗细
    [Export] public float StartDeg = 135f;   // 起始角度（度，0 = 向右，逆时针为正）
    [Export] public float SweepDeg = 270f;   // 总共扫过多少度
    [Export] public Color BgColor = new Color(0.08f, 0.08f, 0.12f, 0.9f);
    [Export] public Color FillColor = new Color(1f, 0.82f, 0.2f);
    [Export] public Color FullColor = new Color(1f, 0.42f, 0.35f);   // 蓄满时的高亮色

    private float _progress;

    /// <summary>0~1 的蓄力进度。</summary>
    public void SetProgress(float p)
    {
        float clamped = Mathf.Clamp(p, 0f, 1f);
        if (Mathf.IsEqualApprox(clamped, _progress)) return;
        _progress = clamped;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_progress <= 0.001f) return;

        float start = Mathf.DegToRad(StartDeg);
        float sweep = Mathf.DegToRad(SweepDeg);

        // 底槽
        DrawArc(Vector2.Zero, Radius, start, start + sweep, 48, BgColor, Width);

        // 填充：蓄满后换成高亮色
        Color c = _progress >= 0.999f ? FullColor : FillColor;
        DrawArc(Vector2.Zero, Radius, start, start + sweep * _progress, 48, c, Width - 2f);
    }
}
