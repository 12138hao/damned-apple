using Godot;

/// <summary>
/// 红色召唤法阵：程序化画同心圆 + 旋转符文，蓄力完成时闪一下。
/// 用法：Spawn(parent, 世界坐标, 半径, 蓄力时长)。
/// </summary>
public partial class MagicCircle : Node2D
{
    public float Radius = 60f;
    public float ChargeTime = 1f;
    public Color RingColor = new Color(1f, 0.16f, 0.18f, 1f);

    /// <summary>法阵蓄满、小怪冒出来那一下的音效名（对应 assets/audio/sfx/&lt;名字&gt;.ogg/.wav/.mp3，留空不播）。</summary>
    public string SfxSpawn = "collapse";

    private float _t;
    private float _spin;
    private bool _done;

    public static MagicCircle Spawn(Node parent, Vector2 worldPos, float radius, float charge)
    {
        var c = new MagicCircle { Radius = radius, ChargeTime = charge, ZIndex = 3 };
        parent.AddChild(c);
        c.GlobalPosition = worldPos;
        return c;
    }

    public override void _Ready()
    {
        _t = 0f;
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        _spin += (float)delta * 2.2f;

        if (_t >= ChargeTime && !_done)
        {
            _done = true;
            AudioManager.Instance?.PlaySfx(SfxSpawn, 0.5f, 1.4f);
        }
        if (_t > ChargeTime + 0.45f) QueueFree();
        QueueRedraw();
    }

    public override void _Draw()
    {
        float k = Mathf.Clamp(_t / Mathf.Max(0.05f, ChargeTime), 0f, 1f);
        float ease = 1f - (1f - k) * (1f - k);
        float r = Radius * (0.25f + 0.75f * ease);
        float a = _t > ChargeTime ? Mathf.Max(0f, 1f - (_t - ChargeTime) / 0.45f) : 1f;

        // 地面压扁：法阵是铺在地上的椭圆
        const float squash = 0.42f;
        var col = new Color(RingColor.R, RingColor.G, RingColor.B, 0.85f * a);
        var fade = new Color(RingColor.R, RingColor.G, RingColor.B, 0.28f * a);

        // 外圈 + 内圈（加粗加亮，不然在草地上看不出来）
        DrawSetTransform(Vector2.Zero, 0f, new Vector2(1f, squash));
        DrawCircle(Vector2.Zero, r, new Color(0.9f, 0.12f, 0.14f, 0.16f * a * ease));
        DrawArc(Vector2.Zero, r, 0f, Mathf.Tau, 56, col, 3.6f, true);
        DrawArc(Vector2.Zero, r * 0.62f, 0f, Mathf.Tau, 44, col, 2.4f, true);
        DrawArc(Vector2.Zero, r * 0.9f, 0f, Mathf.Tau, 48, fade, 8f, true);

        // 旋转符文：8 根短线
        for (int i = 0; i < 8; i++)
        {
            float ang = _spin + i * Mathf.Tau / 8f;
            var d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            DrawLine(d * r * 0.66f, d * r * 0.96f, col, 3.2f, true);
        }

        // 中心光斑
        DrawCircle(Vector2.Zero, r * 0.34f, new Color(1f, 0.32f, 0.26f, 0.5f * a * ease));
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
