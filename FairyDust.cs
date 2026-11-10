using Godot;

/// <summary>
/// 一技能的精灵粉尘：从空中往下落的小光点，碰到玩家扣血。
///
/// "必须可见，但要小" —— 本体只有 10x10 像素，外面加一圈很淡的光晕，
/// 落在绿草地上能一眼看见，又不会挡住玩家判断缝隙。
/// 贴图是代码现画的（6x6 菱形），不额外占素材。
/// </summary>
public partial class FairyDust : Area2D
{
    public int Damage = 6;
    public float FallSpeed = 130f;
    public float FallGravity = 260f;   // 改名：避免和 Area2D.Gravity 撞名
    public float LifeTime = 4f;

    private float _vy;
    private float _life;
    private bool _hitOnce;
    private Sprite2D _art;

    public static FairyDust Spawn(Node parent, Vector2 pos, Vector2 initialVel)
    {
        var d = new FairyDust();
        parent.AddChild(d);
        d.GlobalPosition = pos;
        d.Build(initialVel);
        return d;
    }

    private void Build(Vector2 initialVel)
    {
        CollisionLayer = 0;
        CollisionMask = 2;
        _vy = initialVel.Y;

        var shape = new CollisionShape2D { Shape = new CircleShape2D { Radius = 7f } };
        AddChild(shape);

        _art = new Sprite2D
        {
            Texture = MakeSpark(),
            Scale = Vector2.One * 1.6f,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            ZIndex = 3,
        };
        AddChild(_art);

        BodyEntered += OnBodyEntered;
    }

    private static Texture2D MakeSpark()
    {
        const int n = 6;
        var img = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                int d = Mathf.Abs(x - 2) + Mathf.Abs(y - 2);
                if (d > 3) continue;
                // 中心亮白，外面一圈淡紫，看起来像飘落的精灵尘
                var c = d <= 1
                    ? new Color(1f, 1f, 1f, 1f)
                    : new Color(0.86f, 0.78f, 1f, d == 2 ? 0.95f : 0.55f);
                img.SetPixel(x, y, c);
            }
        }
        return ImageTexture.CreateFromImage(img);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _life += dt;
        if (_life > LifeTime) { QueueFree(); return; }

        _vy += FallGravity * dt;
        Position += new Vector2(0f, _vy * dt);

        // 落地就消失（不在地上留东西）
        if (Position.Y > 380f) { QueueFree(); return; }

        // 闪烁，避免看着像静止的贴图
        if (_art != null) _art.Modulate = new Color(1f, 1f, 1f, 0.75f + 0.25f * Mathf.Sin(_life * 18f));
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_hitOnce) return;
        if (body is not Box player || !player.IsAlive) return;
        _hitOnce = true;
        player.TakeDamage(Damage, GlobalPosition);
        QueueFree();
    }
}
