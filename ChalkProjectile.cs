using Godot;

/// <summary>
/// 远程武器射出的投射物（粉笔）。
///
/// 和载具的 Bullet 的区别：
///   · Bullet 用 _Draw 画一个圆，按「帧数」存活
///   · 这个用 Sprite2D 显示贴图，按「飞行距离」存活（需求是"最远 400px"）
///
/// 用法（在玩家脚本里）：
/// <code>
/// var p = new ChalkProjectile {
///     Damage = w.RangedDamage,
///     Speed = w.RangedSpeed,
///     MaxDistance = w.RangedMaxDistance,
///     ...
/// };
/// GetTree().CurrentScene.AddChild(p);   // 别挂在玩家下面，否则会被玩家带走
/// p.GlobalPosition = 出生点;
/// p.Launch(方向);
/// </code>
/// </summary>
public partial class ChalkProjectile : Area2D
{
    [Export] public int Damage = 30;             // 伤害
    [Export] public float Speed = 900f;          // 飞行速度（px/s）
    [Export] public float MaxDistance = 400f;    // 飞满这么远就消失（px）
    [Export] public float Radius = 5f;           // 命中判定半径
    [Export] public float Knockback = 160f;      // 命中后把敌人推多远
    [Export] public Texture2D ProjectileTexture; // 贴图
    [Export] public float SpriteScale = 0.5f;    // 贴图缩放（长度和基础粗细一起变）
    [Export] public float SpriteThickness = 1.4f; // 只加粗不加长：横向再乘这么多倍

    private Vector2 _dir = Vector2.Right;
    private Vector2 _start;
    private bool _spent;      // 已命中或已过期，等销毁
    private float _fade;      // 消失时的淡出计时（防止"啪"一下没了）

    public override void _Ready()
    {
        CollisionLayer = 0;       // 自己不属于任何层（不会被别的子弹打到）
        CollisionMask = 4;        // 只打敌人层
        Monitoring = true;

        AddChild(new CollisionShape2D
        {
            Shape = new CircleShape2D { Radius = Radius }
        });

        if (ProjectileTexture != null)
        {
            // 粉笔贴图是**竖着**的（磨损的笔尖在贴图底部，也就是 +Y 方向），
            // 而飞行方向是水平的。转 -90° 正好把 +Y 转到 +X：
            // 笔尖朝前飞。转 +90° 的话笔尖会拖在后面，看起来像倒着扔。
            //
            // 缩放是**非等比**的：贴图转 90° 之后，它的「宽」变成了屏幕上的竖直方向，
            // 所以 Scale.X 控制的是粗细、Scale.Y 控制的是长度。
            // 想让粉笔变粗又不想变长，只调 SpriteThickness（乘在 X 上）就行。
            var sp = new Sprite2D
            {
                Texture = ProjectileTexture,
                Scale = new Vector2(SpriteScale * SpriteThickness, SpriteScale),
                Rotation = -Mathf.Pi * 0.5f,
            };
            AddChild(sp);
        }

        BodyEntered += OnBodyEntered;
    }

    /// <summary>设置飞行方向。AddChild 之后调用。</summary>
    public void Launch(Vector2 dir)
    {
        _dir = dir.LengthSquared() > 0.0001f ? dir.Normalized() : Vector2.Right;
        Rotation = _dir.Angle();
        _start = GlobalPosition;
        _spent = false;
        _fade = 0f;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // 命中后淡出一下再销毁，比瞬间消失自然
        if (_spent)
        {
            _fade += dt;
            Modulate = new Color(1f, 1f, 1f, Mathf.Max(0f, 1f - _fade / 0.08f));
            if (_fade >= 0.08f) QueueFree();
            return;
        }

        // 按距离而不是帧数：改了 Speed 之后最远距离也不会跟着变
        Position += _dir * Speed * dt;

        float moved = GlobalPosition.DistanceTo(_start);
        if (moved >= MaxDistance)
        {
            // 一帧走 15px，直接判会超出去几像素；钳回到正好 MaxDistance，
            // 这样"最远 400"是准的，不受速度影响
            GlobalPosition = _start + _dir * MaxDistance;
            _spent = true;
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_spent) return;
        if (body is not Enemy enemy) return;

        _spent = true;
        // ignoreInvuln = true：每一发都独立结算，不会被敌人 0.25 秒的受击无敌吃掉
        enemy.TryTakeHit(Damage, GlobalPosition - _dir * 10f, Knockback, 0.2f, true);
    }
}
