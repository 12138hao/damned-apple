using Godot;

public partial class MovingPlatform : AnimatableBody2D
{
    [Export] public Vector2 Travel = new Vector2(200f, 0f); // 相对出生点往右走多远
    [Export] public float TripTime = 2f;   // 单程要几秒
    [Export] public float Phase = 0f;      // 0~1，多个平台错开节奏
    private Vector2 _origin;
    private float _t;

    public override void _Ready()
    {
        _origin = Position;   // 用 Position 不是 GlobalPosition，Travel 是相对量
    }
	    public override void _PhysicsProcess(double delta)
    {
        _t += (float)delta;
        float k = Mathf.PingPong(_t / TripTime + Phase, 1f);   // 0 → 1 → 0 无限折返
        Position = _origin + Travel * k;
    }
}

