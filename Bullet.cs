using Godot;

/// <summary>
/// 载具发射的蓝色子弹。
///
/// 直线飞行，撞到敌人造成伤害后消失，飞满 LifeFrames 帧也自动消失。
/// 用代码 new 出来后调 Launch(dir) 再 AddChild 到场景根（别挂在玩家下面，会被带走）。
/// </summary>
public partial class Bullet : Area2D
{
    [Export] public int Damage = 3;           // 伤害
    [Export] public float Speed = 420f;       // 飞行速度（像素/秒）
    [Export] public int LifeFrames = 100;     // 飞多少物理帧后消失
    [Export] public float Knockback = 120f;   // 击中后把敌人推多远
    [Export] public float Radius = 4f;        // 判定半径
    [Export] public Color BulletColor = new Color(0.35f, 0.7f, 1f);   // 蓝色

    private Vector2 _dir = Vector2.Right;
    private int _frames;
    private bool _spent;   // 已经命中，等销毁

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 4;        // 只打敌人层
        Monitoring = true;

        var shape = new CollisionShape2D
        {
            Shape = new CircleShape2D { Radius = Radius }
        };
        AddChild(shape);

        BodyEntered += OnBodyEntered;
    }

    /// <summary>设置飞行方向（必须在 AddChild 之前或之后立刻调）。</summary>
    public void Launch(Vector2 dir)
    {
        _dir = dir.LengthSquared() > 0.001f ? dir.Normalized() : Vector2.Right;
        Rotation = _dir.Angle();
        _frames = 0;
        _spent = false;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_spent) return;

        Position += _dir * Speed * (float)delta;
        _frames++;

        if (_frames >= LifeFrames)
        {
            QueueFree();
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_spent) return;
        if (body is not Enemy enemy) return;

        _spent = true;

        // 从子弹来的方向打过去，ignoreInvuln 绕开敌人的受击无敌（子弹是一发一次的）
        enemy.TryTakeHit(Damage, GlobalPosition - _dir * 10f, Knockback, 0.2f, true);
        QueueFree();
    }

    public override void _Draw()
    {
        // 拖尾
        DrawLine(
            new Vector2(-Radius * 3.5f, 0f),
            Vector2.Zero,
            new Color(BulletColor, 0.45f),
            Radius * 1.6f, true);

        // 弹体
        DrawCircle(Vector2.Zero, Radius, BulletColor);

        // 中心高光
        DrawCircle(Vector2.Zero, Radius * 0.5f, new Color(0.85f, 0.95f, 1f, 0.95f));
    }
}
