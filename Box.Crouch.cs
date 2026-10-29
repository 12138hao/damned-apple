using Godot;

/// <summary>
/// Box 的蹲下：按住 C 蹲下，碰撞体高度减半，期间不能移动也不能跳。
///
/// 实现要点：
///   · 碰撞体是 RectangleShape2D，缩到一半后要把中心往下挪 (h - h')/2，脚底才不会浮空
///   · 原形状是共享资源（_baseBodyShape），不能就地改它的 Size，
///     否则站起来时"恢复"拿到的是已经被改过的形状 —— 必须新建一个
///   · 站起来不检查头顶：这关没有低矮通道，等真加了再补
/// </summary>
public partial class Box
{
    [ExportGroup("蹲下")]
    [Export] public bool CanCrouch = true;
    [Export] public float CrouchHeightRatio = 0.5f;  // 蹲下后碰撞体占原来的多少
    [Export] public int CrouchFrame = 20;            // 蹲下用精灵表的第几帧
    [Export] public float CrouchFallMultiplier = 1.9f; // 蹲着往下掉时重力倍数（加速下落）
    [Export] public float CrouchKnockbackScale = 0.4f; // 蹲着被击退时，击退力打几折

    private bool _crouching;
    private Vector2 _standShapePos;      // 站立时碰撞体的本地位置
    private bool _shapeCaptured;

    /// <summary>是否正在蹲下。移动、跳跃、动画都要看这个。</summary>
    public bool IsCrouching => _crouching;

    private void UpdateCrouch(bool inputLocked)
    {
        if (!CanCrouch) return;
        if (_bodyShape == null) return;

        // 载具里不蹲；死亡/复活期间不处理
        // 不再要求站在地上：空中按 C 也要能蹲，才能吃到「加速下落」
        bool want = !inputLocked && !_inVehicle
                    && _state == State.Alive
                    && Input.IsActionPressed("crouch");

        if (want == _crouching) return;

        if (!_shapeCaptured)
        {
            _standShapePos = _bodyShape.Position;
            _shapeCaptured = true;
        }

        if (want) EnterCrouch();
        else ExitCrouch();
    }

    private void EnterCrouch()
    {
        _crouching = true;

        if (_bodyShape?.Shape is RectangleShape2D rect)
        {
            float h = rect.Size.Y;
            float newH = h * CrouchHeightRatio;
            _bodyShape.Shape = new RectangleShape2D
            {
                Size = new Vector2(rect.Size.X, newH)
            };
            // 中心下移一半的差值，脚底位置保持不变
            _bodyShape.Position = new Vector2(_standShapePos.X,
                                              _standShapePos.Y + (h - newH) * 0.5f);
        }

        // 蹲下瞬间把水平速度清掉，避免"滑行着蹲下"
        Velocity = new Vector2(0f, Velocity.Y);
    }

    private void ExitCrouch()
    {
        _crouching = false;

        if (_bodyShape != null && _baseBodyShape != null)
        {
            _bodyShape.Shape = _baseBodyShape;
            _bodyShape.Position = _standShapePos;
        }
    }

    /// <summary>
    /// 身体此刻占的世界矩形（读碰撞体的真实形状，所以蹲下时会自动矮一半）。
    /// 给敌人的命中判定用——怪物该不该咬到人，按这个算才不会「没碰到也扣血」。
    /// </summary>
    public Rect2 BodyRect()
    {
        if (_bodyShape?.Shape is RectangleShape2D rect)
        {
            Vector2 center = _bodyShape.GlobalPosition;
            return new Rect2(center - rect.Size * 0.5f, rect.Size);
        }
        // 没有碰撞体时退回一个 32×32 的方块，别让调用方拿到空矩形
        return new Rect2(GlobalPosition - new Vector2(16f, 16f), new Vector2(32f, 32f));
    }
}
