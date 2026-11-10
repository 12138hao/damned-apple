using Godot;

/// <summary>
/// 三技能：龟派气功式的水流冲击波。
///
/// 观感要点（老大反复提过的）：
///   1. **一整条**连续的水纹光波，不是多段贴图拼起来 —— 所以这里不贴任何美术，
///      整条光柱由一个程序化 shader 算出来，天然没有接缝、没有重复图案；
///   2. **立即出现**，不做"从枪口一点点涨出来"的动画，蓄力结束就是整条轰出去；
///   3. 亮蓝光波 + 水纹两层在同一个 shader 里，外面不再叠额外的发光精灵。
///
/// 伤害按 tick 结算（默认每 0.22 秒一次），站在柱子里会持续掉血。
///
/// 结构：
///   FairyBeam(Area2D，位于发射点，Rotation = 发射角)
///     ├ Art(Sprite2D，1x1 白贴图 + 光波 shader，Scale 到 长度 x 粗细)
///     └ Hit(CollisionShape2D，覆盖整条光柱)
/// </summary>
public partial class FairyBeam : Area2D
{
    [Export] public float Duration = 1.0f;       // 光波持续多久
    [Export] public float Length = 1600f;        // 光波长度（够横穿整个战场）
    [Export] public float Thickness = 150f;      // 光波粗细（世界像素，屏幕上还要乘缩放）
    [Export] public int Damage = 5;              // 每次 tick 的伤害
    [Export] public float TickInterval = 0.22f;  // 多久结算一次伤害
    [Export] public float SweepSpeed = 7f;       // 每秒最多转多少度（越小越难追人）

    /// <summary>波长：每 190 世界像素一个循环。改这个值 = 改水纹的疏密。</summary>
    public float WaveLength = 190f;

    private Sprite2D _art;
    private ShaderMaterial _mat;
    private Box _player;
    private float _life;
    private float _tick;
    private float _aim;          // 当前朝向（弧度）

    public static FairyBeam Spawn(Node parent, Vector2 origin, float aimAngle,
        float duration, float length, float thickness, int damage, float sweep)
    {
        var beam = new FairyBeam
        {
            Duration = duration,
            Length = length,
            Thickness = thickness,
            Damage = damage,
            SweepSpeed = sweep,
        };
        parent.AddChild(beam);
        beam.GlobalPosition = origin;
        beam.Build(aimAngle);
        return beam;
    }

    private void Build(float aimAngle)
    {
        CollisionLayer = 0;
        CollisionMask = 2;
        _aim = aimAngle;
        Rotation = _aim;

        AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(Length, Thickness * 0.8f) },
            Position = new Vector2(Length * 0.5f, 0f),
        });

        // 1x1 白贴图 + 程序化 shader：UV 0..1 铺满整条光柱，没有贴图重复
        var white = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        white.SetPixel(0, 0, Colors.White);

        _art = new Sprite2D
        {
            Name = "Art",
            Texture = ImageTexture.CreateFromImage(white),
            Centered = false,
            Position = new Vector2(0f, -Thickness * 0.5f),
            Scale = new Vector2(Length, Thickness),
            ZIndex = 5,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };

        var shader = GD.Load<Shader>("res://assets/boss/fairy/water_beam.gdshader");
        if (shader != null)
        {
            _mat = new ShaderMaterial { Shader = shader };
            _mat.SetShaderParameter("wave_count", Mathf.Max(1f, Length / Mathf.Max(1f, WaveLength)));
            _art.Material = _mat;
        }
        AddChild(_art);

        _player = GetTree().Root.FindChild("box", true, false) as Box;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _life += dt;

        if (_life > Duration)
        {
            QueueFree();
            return;
        }

        // 缓慢转向玩家（不是瞬移对准，否则躲不开）
        if (_player != null && IsInstanceValid(_player))
        {
            float want = (_player.GlobalPosition - GlobalPosition).Angle();
            float max = Mathf.DegToRad(SweepSpeed) * dt;
            _aim += Mathf.Clamp(Mathf.AngleDifference(_aim, want), -max, max);
            Rotation = _aim;
        }

        // 亮度轻微呼吸 + 最后 0.06 秒淡出，别硬切
        if (_mat != null)
        {
            float e = 0.94f + 0.06f * Mathf.Sin(_life * 22f);
            float fade = Mathf.Clamp((Duration - _life) / 0.06f, 0f, 1f);
            _mat.SetShaderParameter("energy", e * fade);
        }

        // 伤害 tick
        _tick -= dt;
        if (_tick <= 0f)
        {
            _tick = TickInterval;
            foreach (var body in GetOverlappingBodies())
            {
                if (body is Box b && b.IsAlive)
                {
                    b.TakeDamage(Damage, GlobalPosition);
                    break;
                }
            }
        }
    }
}
