using Godot;

/// <summary>
/// 「按 E」提示图标：上下浮动 + 轻微闪烁。
/// 挂在 Sprite2D 上即可，参数都能在检查器里调。
/// </summary>
public partial class EMarker : Sprite2D
{
    [ExportGroup("上下浮动")]
    [Export] public float FloatAmplitude = 4f;    // 上下浮动幅度（像素）
    [Export] public float FloatSpeed = 2.5f;      // 浮动快慢

    [ExportGroup("闪烁")]
    [Export] public float BlinkSpeed = 3.0f;      // 闪烁快慢
    [Export] public float MinAlpha = 0.6f;        // 最淡时的不透明度（0.6 = 60%）
    [Export] public float MaxAlpha = 0.8f;        // 最实时的不透明度（0.8 = 80%）

    private Vector2 _basePos;
    private float _t;

    public override void _Ready()
    {
        _basePos = Position;
        ApplyAlpha(MaxAlpha);
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;

        // 上下浮动：正弦波，围绕原始位置上下摆
        Position = _basePos + new Vector2(0f, Mathf.Sin(_t * FloatSpeed) * FloatAmplitude);

        // 闪烁：正弦波映射到 0~1，再在 MinAlpha ~ MaxAlpha 之间来回
        float k = (Mathf.Sin(_t * BlinkSpeed) + 1f) * 0.5f;
        ApplyAlpha(Mathf.Lerp(MinAlpha, MaxAlpha, k));
    }

    /// <summary>只改 alpha，保留 RGB，避免覆盖手动设的颜色。</summary>
    private void ApplyAlpha(float a)
    {
        var c = Modulate;
        c.A = a;
        Modulate = c;
    }
}
