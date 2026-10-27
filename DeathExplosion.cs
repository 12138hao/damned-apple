using Godot;

/// <summary>
/// 死后爆炸组件：挂在 Enemy 下面，怪物死时对周围造成一次范围伤害，可选留下毒圈。
///
/// 用法：挂到怪物下的空 Node 上。
///   - 只要爆炸：调 Damage / Radius
///   - 还要毒圈：勾上 LeavePoison，调 Poison* 参数
/// 不想要爆炸的怪物就不挂，Enemy.cs 一行都不用改。
/// </summary>
public partial class DeathExplosion : Node
{
    [Export] public int Damage = 20;          // 爆炸伤害
    [Export] public float Radius = 60f;       // 爆炸半径（像素）
    [Export] public float Delay = 0f;         // 死后延迟多久爆（秒），0 = 立刻

    [ExportGroup("毒圈")]
    [Export] public bool LeavePoison = false;         // 炸完留一圈毒
    [Export] public float PoisonRadius = 60f;         // 毒圈半径
    [Export] public float PoisonDuration = 3f;        // 毒圈存在几秒
    [Export] public int PoisonDamage = 5;             // 毒圈每跳伤害
    [Export] public float PoisonTickInterval = 0.5f;  // 每跳间隔
    [Export] public float PoisonYOffset = 28f;        // 毒圈往下挪多少（贴到脚底地面）
    [Export] public float PoisonSquashY = 0.2f;       // 纵向压扁比例（贴图约 0.2）

    [ExportGroup("毒圈贴图")]
    [Export] public Texture2D PoisonTexture;              // 留空则用 assets/poison_puddle.png
    [Export] public float PoisonTextureHalfWidth = 58f;   // 贴图内容半宽（换图时要改）
    [Export] public float PoisonTextureBottomOffset = 59f; // 液底到图片中心的距离（换图时要改）

    public override void _Ready()
    {
        if (GetParent() is Enemy enemy)
        {
            enemy.Died += OnDied;
        }
        else
        {
            GD.PushWarning("[DeathExplosion] 父节点不是 Enemy，爆炸不会生效");
        }
    }

    private async void OnDied()
    {
        var owner = GetParent<Node2D>();
        if (owner == null) return;

        // 先把位置记下来：下面可能要 await，等回来怪物已经被 QueueFree 了
        Vector2 center = owner.GlobalPosition;
        Node root = owner.GetTree().CurrentScene ?? owner.GetTree().Root;

        if (Delay > 0f)
        {
            var timer = GetTree().CreateTimer(Delay);
            await ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
        }

        // 炸一下
        Explode(center, owner);

        // 留毒圈
        if (LeavePoison)
        {
            var zone = new PoisonZone
            {
                Radius = PoisonRadius,
                Duration = PoisonDuration,
                DamagePerTick = PoisonDamage,
                TickInterval = PoisonTickInterval,
                SquashY = PoisonSquashY,
                ZoneTexture = PoisonTexture,                        // null 时 PoisonZone 自己加载默认图
                TextureHalfWidth = PoisonTextureHalfWidth,
                TextureBottomOffset = PoisonTextureBottomOffset,
            };
            root.AddChild(zone);

            // 往下挪到脚底：center 是怪物中心，毒圈要贴在地面上
            zone.GlobalPosition = center + new Vector2(0f, PoisonYOffset);
        }
    }

    private void Explode(Vector2 center, Node2D owner)
    {
        // owner 可能已经被销毁，用记下来的 center 就行
        World2D world = owner != null && IsInstanceValid(owner)
            ? owner.GetWorld2D()
            : GetViewport().World2D;

        var space = world.DirectSpaceState;
        var shape = new CircleShape2D { Radius = Radius };
        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = shape,
            Transform = new Transform2D(0f, center),
            CollisionMask = 2,            // 只打玩家层
            CollideWithBodies = true,
            CollideWithAreas = false,
        };

        foreach (var hit in space.IntersectShape(query))
        {
            if (hit["collider"].As<Node>() is Box player)
            {
                player.TakeDamage(Damage, center);
            }
        }

        GD.Print($"[DeathExplosion] 在 {center} 爆炸，半径 {Radius}，伤害 {Damage}");
    }
}
