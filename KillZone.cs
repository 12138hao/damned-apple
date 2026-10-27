using Godot;

/// <summary>
/// 死亡区域：玩家或怪物一碰到就立刻死亡。
/// 玩家走完整的死亡 + 复活流程；怪物直接压扁消失。
/// 挂在 Area2D 上，下面需要有一个 CollisionShape2D。
/// 适合做深渊底部、尖刺、岩浆这类「碰到就死」的地形。
/// </summary>
public partial class KillZone : Area2D
{
    [Export] public bool OnlyFromAbove = false;   // true = 只有从上方掉进来才判定
    [Export] public float MinFallSpeed = 0f;      // >0 时要求下落速度超过这个值才算（可挡走路误触）

    public override void _Ready()
    {
        // 用事件方式：进入瞬间就判定，比每帧轮询更准
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        // 玩家：走完整的死亡 + 复活流程
        if (body is Box player)
        {
            if (Skip(player.GlobalPosition.Y, player.Velocity.Y)) return;
            player.Kill();
            return;
        }

        // 怪物：直接压扁消失
        if (body is Enemy enemy)
        {
            if (Skip(enemy.GlobalPosition.Y, enemy.Velocity.Y)) return;
            enemy.Kill();
        }
    }

    /// <summary>统一的进入条件判断，返回 true 表示这次不算死。</summary>
    private bool Skip(float bodyY, float bodyVelY)
    {
        // 坐标系 y 向下为正：bodyY < 死区Y 表示该物体在死区上方
        if (OnlyFromAbove && bodyY > GlobalPosition.Y) return true;
        if (MinFallSpeed > 0f && bodyVelY < MinFallSpeed) return true;
        return false;
    }
}
