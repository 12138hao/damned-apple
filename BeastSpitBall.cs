using Godot;
using System;

/// <summary>
/// 从 Boss 嘴里吐出来的恐怖苹果：沿抛物线飞到落点，落地时回调生成真正的怪。
///
/// 飞行途中会翻滚、拖出红色残影，给玩家「有东西要掉下来了」的预警。
/// 由 BeastAppleBoss.SpitOne 创建。
/// </summary>
public partial class BeastSpitBall : Node2D
{
    [Export] public float SpinSpeed = 7.5f;      // 翻滚速度（弧度/秒）
    [Export] public float ArcHeight = 70f;       // 抛物线额外拱高

    private Vector2 _from;
    private Vector2 _to;
    private float _duration = 0.72f;
    private float _t;
    private Action _onLand;
    private Sprite2D _sprite;
    private readonly System.Collections.Generic.List<Node2D> _trail = new();
    private float _trailTimer;

    public void Setup(Vector2 from, Vector2 to, float duration, Action onLand)
    {
        _from = from;
        _to = to;
        _duration = Mathf.Max(0.05f, duration);
        _onLand = onLand;
        _t = 0f;

        GlobalPosition = from;
        ZIndex = 7;

        var sheet = GD.Load<Texture2D>("res://assets/hop_apple_sheet.png");
        _sprite = new Sprite2D
        {
            Texture = sheet,
            Hframes = 32,
            Frame = 0,
            Scale = new Vector2(0.65f, 0.65f),
        };
        AddChild(_sprite);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt;

        float k = Mathf.Clamp(_t / _duration, 0f, 1f);
        GlobalPosition = Sample(k);

        if (_sprite != null) _sprite.Rotation += SpinSpeed * dt;

        // 残影：每隔一小会儿留一个半透明的自己
        _trailTimer -= dt;
        if (_trailTimer <= 0f && k < 0.92f)
        {
            _trailTimer = 0.05f;
            SpawnTrail();
        }
        FadeTrail(dt);

        if (k >= 1f) Land();
    }

    private Vector2 Sample(float k)
    {
        Vector2 p = _from.Lerp(_to, k);
        p.Y -= Mathf.Sin(k * Mathf.Pi) * ArcHeight;   // 往上拱一下
        return p;
    }

    private void SpawnTrail()
    {
        var ghost = new Sprite2D
        {
            Texture = _sprite?.Texture,
            Hframes = 32,
            Frame = 0,
            Scale = new Vector2(0.65f, 0.65f),
            Modulate = new Color(1f, 0.45f, 0.40f, 0.45f),
            GlobalPosition = GlobalPosition,
            Rotation = _sprite?.Rotation ?? 0f,
            ZIndex = 6,
        };
        GetParent().AddChild(ghost);
        _trail.Add(ghost);
    }

    private void FadeTrail(float dt)
    {
        for (int i = _trail.Count - 1; i >= 0; i--)
        {
            var g = _trail[i];
            if (g == null || !IsInstanceValid(g)) { _trail.RemoveAt(i); continue; }

            var c = g.Modulate;
            c.A -= dt * 2.4f;
            if (c.A <= 0.02f)
            {
                g.QueueFree();
                _trail.RemoveAt(i);
                continue;
            }
            g.Modulate = c;
        }
    }

    private void Land()
    {
        var pos = _to;
        var cb = _onLand;
        _onLand = null;

        // 先清残影，别让它留在场上
        foreach (var g in _trail)
            if (g != null && IsInstanceValid(g)) g.QueueFree();
        _trail.Clear();

        cb?.Invoke();
        QueueFree();
    }

    public override void _ExitTree()
    {
        foreach (var g in _trail)
            if (g != null && IsInstanceValid(g)) g.QueueFree();
        _trail.Clear();
    }
}
