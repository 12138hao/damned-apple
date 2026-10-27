using Godot;
using System.Collections.Generic;

/// <summary>
/// 毒圈：贴在地面上的一滩液体，踩上去持续掉血，时间到自动消失。
///
/// 有两种画法：
///   - 有贴图（ZoneTexture）→ 用 Sprite2D 显示，液底对齐节点（= 地面）
///   - 没贴图 → 回退成 _Draw 画的压扁椭圆
///
/// 碰撞体统一用压扁的矩形，和视觉大致对齐。
/// </summary>
public partial class PoisonZone : Area2D
{
    [Export] public Texture2D ZoneTexture;           // 留空则用默认的毒液图
    [Export] public float TextureHalfWidth = 58f;    // 贴图内容半宽（用来按 Radius 缩放）
    [Export] public float TextureBottomOffset = 59f; // 贴图里液底相对图片中心的距离

    [Export] public float Radius = 60f;              // 判定半径（像素）
    // 压扁比例：贴图内容 116×23，比例约 0.2。碰撞体也按这个比例，
    // 免得「站在液体上方看着没碰到却掉血」。
    [Export] public float SquashY = 0.2f;
    [Export] public float Duration = 3f;             // 存在多久（秒）
    [Export] public int DamagePerTick = 5;           // 每跳伤害
    [Export] public float TickInterval = 0.5f;       // 每跳间隔（秒）
    [Export] public Color FillColor = new Color(1f, 0.88f, 0.15f, 0.38f);
    [Export] public Color RimColor = new Color(1f, 0.95f, 0.35f, 0.9f);

    private float _life;
    private float _tick;
    private Sprite2D _visual;
    private readonly List<Box> _inside = new();

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 2;       // 检测玩家层
        Monitoring = true;

        // 碰撞体用压扁的矩形，和贴图大致对齐
        var shape = new CollisionShape2D
        {
            Shape = new RectangleShape2D
            {
                Size = new Vector2(Radius * 2f, Radius * 2f * SquashY)
            }
        };
        AddChild(shape);

        // 贴图没指定就加载默认的
        if (ZoneTexture == null)
        {
            ZoneTexture = GD.Load<Texture2D>("res://assets/poison_puddle.png");
        }

        if (ZoneTexture != null)
        {
            float s = Radius / Mathf.Max(1f, TextureHalfWidth);

            _visual = new Sprite2D
            {
                Name = "Visual",
                Texture = ZoneTexture,
                Scale = new Vector2(s, s),
                // 把「液体底部」对齐到节点原点，这样节点放在地面上时液体正好铺在地上
                Offset = new Vector2(0f, -TextureBottomOffset),
            };
            AddChild(_visual);
        }

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _Process(double delta)
    {
        _life += (float)delta;

        if (_life >= Duration)
        {
            QueueFree();
            return;
        }

        float k = _life / Duration;

        // 快消失时淡出（Area2D 的 Modulate 会传给子节点）
        Modulate = new Color(1f, 1f, 1f, 1f - k * 0.75f);

        // 有贴图时用轻微缩放做呼吸感
        if (_visual != null)
        {
            float baseScale = Radius / Mathf.Max(1f, TextureHalfWidth);
            float pulse = 1f + 0.04f * Mathf.Sin(_life * 7f);
            _visual.Scale = new Vector2(baseScale * pulse, baseScale * pulse);
        }
        else
        {
            QueueRedraw();   // 没贴图才需要重绘椭圆
        }

        // 持续伤害：用 while + 减去间隔，避免 delta 波动累积误差
        _tick += (float)delta;
        while (_tick >= TickInterval && _life < Duration)
        {
            _tick -= TickInterval;

            _inside.RemoveAll(p => !IsInstanceValid(p));

            foreach (var player in _inside)
            {
                // 用 TakeDotDamage：只扣血，不击退
                player.TakeDotDamage(DamagePerTick);
            }
        }
    }

    /// <summary>没有贴图时的回退画法：压扁的椭圆。</summary>
    public override void _Draw()
    {
        if (_visual != null) return;   // 有贴图就不画

        float pulse = 1f + 0.05f * Mathf.Sin(_life * 7f);
        float rx = Radius * pulse;
        float ry = rx * SquashY;

        const int seg = 48;
        var pts = new Vector2[seg];
        for (int i = 0; i < seg; i++)
        {
            float a = i / (float)seg * Mathf.Tau;
            pts[i] = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
        }

        DrawColoredPolygon(pts, FillColor);
        for (int i = 0; i < seg; i++)
        {
            DrawLine(pts[i], pts[(i + 1) % seg], RimColor, 2f, true);
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Box player && !_inside.Contains(player))
        {
            _inside.Add(player);
        }
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is Box player)
        {
            _inside.Remove(player);
        }
    }
}
