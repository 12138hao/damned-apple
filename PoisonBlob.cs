using Godot;

/// <summary>
/// 蛇毒弹：从蛇嘴朝目标点抛物线飞出，落地炸成一滩深紫毒池。
/// 飞行途中砸到玩家算一次伤害，落地后由 PoisonZone 持续扣血。
/// </summary>
public partial class PoisonBlob : Node2D
{
    public Vector2 Target;
    public float FlightTime = 0.85f;
    public int HitDamage = 10;          // 飞行中砸到人
    public int PoolDamage = 10;         // 毒池每跳
    public float PoolRadius = 70f;
    public float PoolDuration = 4f;
    public float PoolTickInterval = 1f;   // 毒池每跳间隔

    /// <summary>毒池落地那一下的音效名（对应 assets/audio/sfx/&lt;名字&gt;，留空不播）。</summary>
    public string SfxLand = "collapse";

    private Vector2 _start;
    private Vector2 _vel;
    private float _t;
    private bool _done;
    private Box _player;

    public static PoisonBlob Spawn(Node parent, Vector2 from, Vector2 target)
    {
        var b = new PoisonBlob { Target = target, ZIndex = 6 };
        parent.AddChild(b);
        b.GlobalPosition = from;
        return b;
    }

    public override void _Ready()
    {
        _start = GlobalPosition;
        _player = GetTree().Root.FindChild("box", true, false) as Box;

        float T = Mathf.Max(0.25f, FlightTime);
        const float g = 1500f;
        // 抛物线：水平匀速、竖直由重力 + 初速决定
        _vel = new Vector2((Target.X - _start.X) / T, (Target.Y - _start.Y - 0.5f * g * T * T) / T);
    }

    public override void _Process(double delta)
    {
        if (_done) return;
        float dt = (float)delta;
        _t += dt;

        _vel.Y += 1500f * dt;
        GlobalPosition += _vel * dt;

        // 飞行中命中
        if (_player != null && IsInstanceValid(_player))
        {
            if (GlobalPosition.DistanceTo(_player.GlobalPosition) < 26f)
            {
                _player.TakeDamage(HitDamage, GlobalPosition);
                Land();
                return;
            }
        }

        if (GlobalPosition.Y >= Target.Y || _t > 4f) Land();
        QueueRedraw();
    }

    private void Land()
    {
        if (_done) return;
        _done = true;

        var zone = new PoisonZone
        {
            Radius = PoolRadius,
            Duration = PoolDuration,
            DamagePerTick = PoolDamage,
            TickInterval = PoolTickInterval,
            SquashY = 0.24f,
            ZIndex = 2,
            ZoneTexture = GD.Load<Texture2D>("res://assets/eden/poison_purple.png"),
        };
        GetParent().AddChild(zone);
        zone.GlobalPosition = new Vector2(GlobalPosition.X, Target.Y);

        AudioManager.Instance?.PlaySfx(SfxLand, 0.45f, 1.5f);
        QueueFree();
    }

    public override void _Draw()
    {
        // 一团带高光的紫色液滴，飞行时拉出一点尾巴
        DrawCircle(Vector2.Zero, 9f, new Color(0.35f, 0.05f, 0.55f, 1f));
        DrawCircle(Vector2.Zero, 6.5f, new Color(0.55f, 0.18f, 0.85f, 1f));
        DrawCircle(new Vector2(-1.5f, -2f), 2.2f, new Color(0.85f, 0.65f, 1f, 0.9f));
        var tailDir = -_vel.Normalized();
        DrawLine(Vector2.Zero, tailDir * 7f, new Color(0.45f, 0.1f, 0.7f, 0.55f), 4f);
    }
}
