using Godot;

/// <summary>
/// 剑气：武器贴图比攻击判定短的时候，用它补上"多出来的那段攻击距离"。
///
/// 挂在 Weapon 节点下面（会跟着武器一起转），沿 X 轴（武器朝向）画。
/// 白色半透明 + 往后飘的颗粒尾气，攻击时闪一下就没。
/// </summary>
public partial class WeaponAura : Node2D
{
    [Export] public float StartDist = 38f;      // 从武器尖端往外多少像素开始
    [Export] public float Length = 34f;         // 剑气长度
    [Export] public float Width = 16f;          // 剑气最宽处
    [Export] public Color AuraColor = new Color(1f, 1f, 1f, 0.8f);  // 白色 80% 不透明
    [Export] public int ParticleCount = 24;     // 颗粒数量
    [Export] public float Duration = 0.18f;     // 持续多久

    private struct Particle
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Size;
        public float Alpha;
    }

    private float _t = -1f;
    private Particle[] _ps = System.Array.Empty<Particle>();
    private System.Random _rng = new();

    public override void _Ready()
    {
        Visible = false;
        ZIndex = 5;
    }

    /// <summary>放一次剑气。</summary>
    public void Fire()
    {
        _t = 0f;
        Visible = true;

        int n = Mathf.Max(1, ParticleCount);
        _ps = new Particle[n];

        for (int i = 0; i < n; i++)
        {
            float t = (float)_rng.NextDouble();
            float x = StartDist + Length * t;
            float y = (float)(_rng.NextDouble() - 0.5) * Width * 0.8f;

            _ps[i] = new Particle
            {
                Pos = new Vector2(x, y),
                // 往握把方向（-X）飘，越远越快
                Vel = new Vector2(-(20f + 60f * t), (float)(_rng.NextDouble() - 0.5) * 40f),
                Size = 1f + (float)_rng.NextDouble() * 1.6f,
                Alpha = 0.7f + (float)_rng.NextDouble() * 0.3f,
            };
        }

        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_t < 0f) return;

        _t += (float)delta;
        if (_t >= Duration)
        {
            _t = -1f;
            Visible = false;
            return;
        }

        float dt = (float)delta;
        float fade = 1f - _t / Duration;

        for (int i = 0; i < _ps.Length; i++)
        {
            _ps[i].Pos += _ps[i].Vel * dt;
            _ps[i].Vel *= 0.92f;               // 阻力
            _ps[i].Alpha *= 0.94f;             // 逐渐消失
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_t < 0f) return;

        float k = _t / Duration;
        float fade = 1f - k;

        // 主体：从尖端往外的一个细长三角，越远越尖
        float reach = StartDist + Length * (0.55f + 0.45f * k);
        var body = new Vector2[]
        {
            new(StartDist, -Width * 0.5f * fade),
            new(reach, 0f),
            new(StartDist, Width * 0.5f * fade),
        };
        DrawColoredPolygon(body, new Color(AuraColor, AuraColor.A * fade));

        // 中线亮一点，看着有能量感
        DrawLine(
            new Vector2(StartDist, 0f),
            new Vector2(reach, 0f),
            new Color(1f, 1f, 1f, 0.85f * fade),
            1.5f, true);

        // 颗粒尾气
        foreach (var p in _ps)
        {
            if (p.Alpha <= 0.02f) continue;
            DrawCircle(p.Pos, p.Size, new Color(1f, 1f, 1f, p.Alpha * fade));
        }
    }
}
