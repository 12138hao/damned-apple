using Godot;

/// <summary>
/// 冲击波 / 落地烟尘 / 爆炸碎片。一次性的，播完自己销毁。
///
/// 由 Boss 在代码里创建：
///   new BeastShockwave { Radius = 120f, Duration = 0.45f }
/// 想要碎片就加 IsDebris = true（会往外飞并受重力）。
/// </summary>
public partial class BeastShockwave : Node2D
{
    [Export] public float Radius = 120f;
    [Export] public float Duration = 0.45f;
    [Export] public float Delay = 0f;         // 延迟多久开始
    [Export] public bool IsDebris = false;    // 碎片模式
    [Export] public Color RingColor = new Color(1f, 0.72f, 0.35f, 0.9f);
    [Export] public Color FillColor = new Color(1f, 0.55f, 0.20f, 0.35f);

    /// <summary>
    /// 跟着某个节点横向跑（Y 保持在生成时的那条线上）。
    ///
    /// 用途：Boss 的斜向冲锋速度可以调到 1200，一秒能甩出几百像素。
    /// 撞击光圈如果钉死在原地，等它扩到最大时 Boss 早就飞走了，
    /// 看起来就是「光圈在后面追」。锁上 X 之后，光圈变成一道
    /// 贴着天花板 / 地面跟着 Boss 跑的冲击波，速度再快也不会脱节。
    /// </summary>
    [Export] public Node2D FollowTarget;
    [Export] public bool FollowXOnly = false;

    private float _t;
    private bool _started;
    private Vector2 _vel;
    private float _spin;
    private float _fixedY;
    private bool _fixedYCaptured;

    public override void _Ready()
    {
        ZIndex = 9;
        if (IsDebris)
        {
            // 碎片：随机往四周飞，带一点上抛
            float a = GD.Randf() * Mathf.Tau;
            float sp = 120f + GD.Randf() * 180f;
            _vel = new Vector2(Mathf.Cos(a) * sp, Mathf.Sin(a) * sp - 120f);
            _spin = (GD.Randf() - 0.5f) * 12f;
        }
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // 跟随：放在延迟判断之前，这样「还没开始扩」的那几帧也在跟着走
        if (FollowTarget != null && IsInstanceValid(FollowTarget))
        {
            // 第一次处理时把 Y 记下来当锚点（这时候调用方已经摆好位置了，
            // 在 _Ready 里记会拿到还没设位置的旧值）
            if (!_fixedYCaptured) { _fixedY = GlobalPosition.Y; _fixedYCaptured = true; }

            float y = FollowXOnly ? _fixedY : FollowTarget.GlobalPosition.Y;
            GlobalPosition = new Vector2(FollowTarget.GlobalPosition.X, y);
        }

        if (!_started)
        {
            _t += dt;
            if (_t < Delay) return;
            _started = true;
            _t = 0f;
        }

        _t += dt;

        if (IsDebris)
        {
            _vel += new Vector2(0f, 900f * dt);
            Position += _vel * dt;
            Rotation += _spin * dt;
        }

        QueueRedraw();

        if (_t >= Duration) QueueFree();
    }

    public override void _Draw()
    {
        if (!_started) return;

        float k = Mathf.Clamp(_t / Mathf.Max(0.01f, Duration), 0f, 1f);

        if (IsDebris)
        {
            // 碎片：一个小方块，越飞越小越淡
            float a = 1f - k;
            var c = new Color(0.85f, 0.18f, 0.12f, a);
            float s = Radius * (1f - k * 0.5f) * 0.5f;
            DrawRect(new Rect2(-s * 0.5f, -s * 0.5f, s, s), c);
            return;
        }

        // 冲击波：一圈往外扩，越来越淡
        float r = Radius * Mathf.Sqrt(k);      // sqrt 让前段扩得快
        float alpha = 1f - k;

        DrawCircle(Vector2.Zero, r * 0.55f, new Color(FillColor.R, FillColor.G, FillColor.B, FillColor.A * alpha * 0.6f));

        var ring = new Color(RingColor.R, RingColor.G, RingColor.B, RingColor.A * alpha);
        DrawArc(Vector2.Zero, r, 0f, Mathf.Tau, 48, ring, Mathf.Lerp(6f, 1.5f, k), true);

        // 内圈，慢半拍
        if (k > 0.25f)
        {
            float k2 = (k - 0.25f) / 0.75f;
            var ring2 = new Color(RingColor.R, RingColor.G, RingColor.B, RingColor.A * (1f - k2) * 0.6f);
            DrawArc(Vector2.Zero, Radius * Mathf.Sqrt(k2) * 0.7f, 0f, Mathf.Tau, 40, ring2, 2f, true);
        }
    }
}
