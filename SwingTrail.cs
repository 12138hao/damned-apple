using Godot;

/// <summary>
/// 挥击气浪：流星状弧带 —— 头部宽、尾部收细，尾巴散出颗粒，做出破风感。
/// 挂在 box 下、位置 (0,0)，弧心就是角色中心。
/// </summary>
public partial class SwingTrail : Node2D
{
    [Export] public float Radius = 44f;         // 弧的半径（要能覆盖攻击判定范围）
    [Export] public float HeadWidth = 20f;      // 头部最宽处
    [Export] public Color TrailColor = new Color(1f, 1f, 1f, 0.5f);   // 50% 白
    [Export] public int BodySegments = 14;      // 主体分成几段（每段宽度不同）
    [Export] public int ParticleCount = 14;     // 尾部颗粒数量
    [Export] public float ParticleSpread = 10f; // 颗粒在半径方向的抖动幅度
    [Export] public float ParticleSize = 1.6f;  // 颗粒基础大小

    private float _fromDeg;
    private float _toDeg;
    private float _alpha;

    // 固定抖动表：避免每帧随机导致颗粒乱跳
    private static readonly float[] Jitter =
    {
        0.10f, -0.45f, 0.70f, -0.20f, 0.50f, -0.65f, 0.30f,
        -0.10f, 0.60f, -0.35f, 0.20f, -0.55f, 0.45f, -0.25f,
    };

    public void Show(float fromDeg, float toDeg, float alpha)
    {
        _fromDeg = fromDeg;
        _toDeg = toDeg;
        _alpha = Mathf.Clamp(alpha, 0f, 1f);
        QueueRedraw();
    }

    public void Clear()
    {
        _alpha = 0f;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_alpha <= 0.01f) return;

        // 1) 主体：从头（当前角度）往尾巴（起始角）逐渐收细
        for (int i = 0; i < BodySegments; i++)
        {
            float t0 = (float)i / BodySegments;
            float t1 = (float)(i + 1) / BodySegments;

            float a0 = Mathf.Lerp(_toDeg, _fromDeg, t0);
            float a1 = Mathf.Lerp(_toDeg, _fromDeg, t1);
            float w0 = HeadWidth * (1f - t0) * (1f - t0);   // 平方衰减，头粗尾尖

            Color c = TrailColor;
            c.A = TrailColor.A * _alpha * (1f - t0 * 0.78f);

            DrawArc(Vector2.Zero, Radius,
                    Mathf.DegToRad(a0), Mathf.DegToRad(a1),
                    3, c, Mathf.Max(w0, 1f));
        }

        // 2) 尾气颗粒：集中在后半段，沿弧线散开
        for (int i = 0; i < ParticleCount; i++)
        {
            float t = 0.5f + 0.5f * (i / (float)ParticleCount);
            float a = Mathf.Lerp(_toDeg, _fromDeg, t);
            float jitter = Jitter[i % Jitter.Length];
            float r = Radius + jitter * ParticleSpread;

            float rad = Mathf.DegToRad(a);
            Vector2 p = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * r;

            Color c = TrailColor;
            c.A = TrailColor.A * _alpha * (1f - t) * 0.95f;
            DrawCircle(p, ParticleSize * (1f - t * 0.5f) + (i % 3) * 0.4f, c);
        }
    }
}
