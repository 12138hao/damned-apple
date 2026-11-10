using Godot;

/// <summary>
/// 被甩尾震落的苹果：先在落点画影子预警，然后从树冠上方缓缓旋转着掉下来。
/// 砸中玩家扣 Damage，落到地面炸成一团碎屑后消失。
///
/// 用的是「像素蛇怪生成 (6).png」这棵树上那颗苹果本身（assets/eden/tree_apple.png），
/// 不是手画的圆球。
/// </summary>
public partial class FallingApple : Node2D
{
    public float GroundY = 700f;
    public int Damage = 12;
    public float WarnTime = 0.85f;
    public float StartHeight = 520f;    // 从地面上方多高开始落
    public float Gravity = 820f;        // 慢一点，能看清它在转
    public float Spin = 1.7f;           // 角速度（弧度/秒）

    /// <summary>苹果落地/砸中人那一下的音效名（对应 assets/audio/sfx/&lt;名字&gt;，留空不播）。</summary>
    public string SfxLand = "collapse";

    /// <summary>落地音效最短间隔（秒）。一波 7 颗苹果会在同一两帧内砸地，
    /// 逐颗播会叠成噪音；0.12 秒最多响一声，一波听到 2~3 声"咚咚"，够表达"一串苹果下来了"。</summary>
    public float SfxLandGap = 0.12f;

    private static Texture2D _tex;

    private float _t;
    private float _y;                   // 相对地面线的竖直偏移（0=落地）
    private float _vy;
    private float _spinDir;
    private bool _falling;
    private bool _done;
    private Sprite2D _art;
    private Box _player;

    public static FallingApple Spawn(Node parent, float worldX, float groundY)
    {
        var a = new FallingApple { GroundY = groundY, ZIndex = 8 };
        parent.AddChild(a);
        a.GlobalPosition = new Vector2(worldX, groundY);
        return a;
    }

    public override void _Ready()
    {
        _player = GetTree().Root.FindChild("box", true, false) as Box;
        _tex ??= GD.Load<Texture2D>("res://assets/eden/tree_apple.png");

        _art = new Sprite2D
        {
            Name = "Art",
            Texture = _tex,
            Centered = true,
            Visible = false,
            Scale = new Vector2(1.65f, 1.65f),
        };
        AddChild(_art);

        _spinDir = GD.Randf() < 0.5f ? -1f : 1f;
        Spin *= (float)GD.RandRange(0.75, 1.25);
        // 同一波里不要每个都一模一样：起落时间岔开一点
        WarnTime += (float)GD.RandRange(-0.08f, 0.14f);
    }

    public override void _Process(double delta)
    {
        if (_done) return;
        float dt = (float)delta;
        _t += dt;

        if (!_falling)
        {
            if (_t >= WarnTime)
            {
                _falling = true;
                _y = -StartHeight * (float)GD.RandRange(0.92, 1.06);
                _vy = 24f;
                _art.Visible = true;
                _art.Position = new Vector2(0f, _y);
                _art.Rotation = (float)GD.RandRange(-0.4, 0.4);
            }
            QueueRedraw();
            return;
        }

        _vy += Gravity * dt;
        _y += _vy * dt;
        _art.Position = new Vector2(0f, _y);
        _art.Rotation += _spinDir * Spin * dt;

        var center = GlobalPosition + new Vector2(0f, _y);
        if (_player != null && IsInstanceValid(_player)
            && center.DistanceTo(_player.GlobalPosition + new Vector2(0f, -20f)) < 28f)
        {
            _player.TakeDamage(Damage, center);
            Burst();
            return;
        }

        if (_y >= 0f) Burst();
        QueueRedraw();
    }

    private void Burst()
    {
        if (_done) return;
        _done = true;
        PlayLandSfx();

        var burst = new AppleBurst { ZIndex = 8 };
        GetParent().AddChild(burst);
        burst.GlobalPosition = new Vector2(GlobalPosition.X, GlobalPosition.Y + Mathf.Min(0f, _y));
        QueueFree();
    }

    // 上一声"苹果落地"是什么时候（所有苹果共用这一个时间戳）
    private static ulong _lastLandSfxMs;

    /// <summary>落点音效（带节流）。同一波十几颗苹果会在同一两帧内落地，逐颗播会叠成一片，
    /// 所以每 <see cref="SfxLandGap"/> 秒最多只响一声 —— 听感上还是"一串苹果砸下来"。</summary>
    private void PlayLandSfx()
    {
        ulong now = Time.GetTicksMsec();
        if (now - _lastLandSfxMs < (ulong)(Mathf.Max(0f, SfxLandGap) * 1000f)) return;
        _lastLandSfxMs = now;

        // 音高随机一点，避免"同一声"重复得太机械
        AudioManager.Instance?.PlaySfx(SfxLand, 0.5f, 1.75f + GD.Randf() * 0.3f);
    }

    public override void _Draw()
    {
        // 影子永远贴地面，随高度变小变淡，提示"落点在这儿"
        float hgt = Mathf.Clamp(-_y / (StartHeight + 40f), 0f, 1f);
        if (_falling)
        {
            // 暖色光晕 + 下落拖影：树叶背景太花，没有这层的话苹果会跟树上的果子糊在一起
            var p = new Vector2(0f, _y);
            DrawCircle(p, 34f, new Color(1f, 0.40f, 0.22f, 0.09f));
            DrawCircle(p, 25f, new Color(1f, 0.50f, 0.30f, 0.10f));
            DrawLine(p + new Vector2(0f, -20f), p + new Vector2(0f, -48f),
                     new Color(1f, 0.45f, 0.3f, 0.14f), 7f);

            float s = 0.55f - 0.22f * hgt;
            DrawSetTransform(Vector2.Zero, 0f, new Vector2(1f, 0.3f));
            DrawCircle(Vector2.Zero, 30f * s, new Color(0.08f, 0.04f, 0.02f, 0.42f * (1f - 0.35f * hgt)));
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
            // 地面红圈：越接近落地越明显，玩家靠这个躲
            DrawArc(Vector2.Zero, 24f, 0f, Mathf.Tau, 28,
                    new Color(1f, 0.22f, 0.14f, 0.20f + 0.30f * hgt), 2f, true);
            return;
        }

        // 蓄势期：收缩的红圈 + 影子
        float k = Mathf.Clamp(_t / Mathf.Max(0.05f, WarnTime), 0f, 1f);
        var c = new Color(0.1f, 0.05f, 0.02f, 0.18f + 0.3f * k);
        DrawSetTransform(Vector2.Zero, 0f, new Vector2(1f, 0.32f));
        DrawCircle(Vector2.Zero, 26f * (1f - 0.2f * k), c);
        DrawArc(Vector2.Zero, 27f, 0f, Mathf.Tau, 26,
                new Color(0.9f, 0.18f, 0.12f, 0.3f + 0.45f * k), 2f, true);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}

/// <summary>落地碎屑，纯演出。</summary>
public partial class AppleBurst : Node2D
{
    private float _t;
    private readonly Vector2[] _dirs =
    {
        new(-1f, -0.8f), new(-0.4f, -1.2f), new(0.3f, -1.1f), new(0.9f, -0.7f), new(0f, -0.4f),
    };

    public override void _Process(double delta)
    {
        _t += (float)delta;
        if (_t > 0.45f) QueueFree();
        QueueRedraw();
    }

    public override void _Draw()
    {
        float k = Mathf.Clamp(_t / 0.45f, 0f, 1f);
        var c = new Color(0.75f, 0.12f, 0.1f, 1f - k);
        foreach (var d in _dirs)
        {
            var p = d * (26f * k);
            p.Y += 40f * k * k;
            DrawCircle(p, 3.2f * (1f - k) + 0.8f, c);
        }
        DrawCircle(Vector2.Zero, 16f * (0.4f + k), new Color(0.85f, 0.2f, 0.15f, 0.35f * (1f - k)));
    }
}
