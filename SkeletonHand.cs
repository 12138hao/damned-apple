using Godot;
using System.Collections.Generic;

/// <summary>
/// 不死海盗苹果发射出去的骷髅手。
///
/// 三个阶段：
///   Fly    直线飞向锁定的方向，撞墙 / 超距 → 回收
///   Drag   抓到玩家：把玩家钉在自己身上，一路拖回苹果面前
///   Return 没抓到：飞回苹果手上
///
/// 碰撞不用物理体：每帧做一次「到玩家的距离」判定 + 一次点查询查墙，
/// 单发投射物这样做最省事，也不用配碰撞层。
/// </summary>
public partial class SkeletonHand : Node2D
{
    public enum Phase { Fly, Drag, Return }

    public UndeadPirateApple SourceApple;
    public Box Target;

    [Export] public float FlySpeed = 620f;      // 飞行速度（像素/秒）
    [Export] public float MaxRange = 780f;      // 飞这么远还没打到就回收
    [Export] public float HitRadius = 30f;      // 判定半径
    [Export] public float DragSpeed = 1100f;    // 拖人回来的速度
    [Export] public float ReturnSpeed = 900f;   // 回收速度
    [Export] public float GrabLift = -26f;      // 被抓住的玩家相对手心上浮多少

    /// <summary>手贴图里「手指指向」的角度（度）。用来把贴图转到飞行方向上。</summary>
    [Export] public float ArtForwardDeg = -141f;

    /// <summary>
    /// 飞行手的贴图缩放。
    /// 贴图是 160x141 的「整只手」，而长在身体上的那只手只有 30x50（再乘怪的 0.55）。
    /// 按高度对齐：141 × ArtScale = 50 × 0.55  →  ArtScale ≈ 0.195。
    /// 填 0.2 让它在屏幕上和身体上那只手一样高（张开的手指会让它略宽一点，这是贴图本身形状决定的）。
    /// </summary>
    [Export] public float ArtScale = 0.2f;

    private Phase _phase = Phase.Fly;
    private Vector2 _dir = Vector2.Right;
    private float _travelled;
    private Sprite2D _sprite;

    // 拖尾：记录最近若干个世界坐标，画成渐隐的紫色雾团
    private readonly List<Vector2> _trail = new();
    private const int TrailMax = 16;

    private float _time;

    public override void _Ready()
    {
        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (_sprite != null) _sprite.Scale = Vector2.One * ArtScale;
        ZIndex = 20;                       // 盖在玩家和泥浆前景之上
        _dir = _dir.Normalized();
    }

    /// <summary>由苹果怪在生成时调用。</summary>
    public void Launch(Vector2 dir)
    {
        _dir = dir.Normalized();
        _phase = Phase.Fly;
        _travelled = 0f;
        if (_sprite != null) _sprite.Rotation = _dir.Angle() - Mathf.DegToRad(ArtForwardDeg);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _time += dt;

        switch (_phase)
        {
            case Phase.Fly: UpdateFly(dt); break;
            case Phase.Drag: UpdateDrag(dt); break;
            case Phase.Return: UpdateReturn(dt); break;
        }

        UpdateTrail();
        QueueRedraw();
    }

    // ---------------------------------------------------------- 飞行

    private void UpdateFly(float dt)
    {
        Vector2 step = _dir * FlySpeed * dt;
        GlobalPosition += step;
        _travelled += step.Length();

        // 打到玩家 → 抓住。判定点用身体中心，和苹果瞄准的是同一个点，
        // 否则瞄胸口、判脚底，手会从人身上穿过去。
        if (Target != null && IsInstanceValid(Target) && Target.IsAlive)
        {
            if (GlobalPosition.DistanceTo(Target.BodyCenter) <= HitRadius)
            {
                Target.BeginGrab();
                _phase = Phase.Drag;
                return;
            }
        }

        // 撞墙 / 飞太远 → 回收
        if (_travelled > MaxRange || HitWall())
        {
            _phase = Phase.Return;
        }
    }

    /// <summary>点查询看当前位置是不是插进了实体地形（层 1）。</summary>
    private bool HitWall()
    {
        var q = new PhysicsPointQueryParameters2D
        {
            Position = GlobalPosition,
            CollideWithBodies = true,
            CollideWithAreas = false,
            CollisionMask = 1,
        };
        return GetWorld2D().DirectSpaceState.IntersectPoint(q).Count > 0;
    }

    // ---------------------------------------------------------- 拖人

    private void UpdateDrag(float dt)
    {
        if (Target == null || !IsInstanceValid(Target))
        {
            _phase = Phase.Return;
            return;
        }

        Vector2 goal = SourceApple != null && IsInstanceValid(SourceApple) ? SourceApple.GrabPoint : GlobalPosition;
        GlobalPosition = GlobalPosition.MoveToward(goal, DragSpeed * dt);

        // 玩家被钉在手心上，略微上浮一点，看起来是被拎着的
        Target.SetGrabbedPosition(GlobalPosition + new Vector2(0, GrabLift));

        if (GlobalPosition.DistanceTo(goal) < 6f)
        {
            // 到苹果面前了，交给苹果打投技
            if (SourceApple != null && IsInstanceValid(SourceApple)) SourceApple.OnHandArrivedWithPlayer();
            QueueFree();
        }
    }

    // ---------------------------------------------------------- 回收

    private void UpdateReturn(float dt)
    {
        Vector2 goal = SourceApple != null && IsInstanceValid(SourceApple)
            ? SourceApple.HandWorldPosition
            : GlobalPosition + Vector2.Left * 100f;

        GlobalPosition = GlobalPosition.MoveToward(goal, ReturnSpeed * dt);

        if (GlobalPosition.DistanceTo(goal) < 8f)
        {
            if (SourceApple != null && IsInstanceValid(SourceApple)) SourceApple.OnHandMissed();
            QueueFree();
        }
    }

    // ---------------------------------------------------------- 拖尾 / 紫雾

    private void UpdateTrail()
    {
        if (_trail.Count == 0 || _trail[_trail.Count - 1].DistanceTo(GlobalPosition) > 3f)
        {
            _trail.Add(GlobalPosition);
            if (_trail.Count > TrailMax) _trail.RemoveAt(0);
        }
    }

    public override void _Draw()
    {
        // 拖尾：越旧越淡越小。
        // 半径要跟着手的大小走 —— 手只有 32x28 像素，光斑比手还大就会糊成一团紫云。
        for (int i = 0; i < _trail.Count; i++)
        {
            float k = (float)(i + 1) / _trail.Count;     // 0.06 ~ 1
            Vector2 local = _trail[i] - GlobalPosition;
            float r = 1.5f + 4.5f * k;
            DrawCircle(local, r, new Color(0.55f, 0.25f, 0.92f, 0.24f * k));
            DrawCircle(local, r * 0.55f, new Color(0.78f, 0.55f, 1f, 0.18f * k));
        }

        // 手心那团光，跟着呼吸
        float pulse = 0.75f + 0.25f * Mathf.Sin(_time * 14f);
        DrawCircle(Vector2.Zero, 9f * pulse, new Color(0.60f, 0.28f, 0.95f, 0.20f));
        DrawCircle(Vector2.Zero, 5f * pulse, new Color(0.82f, 0.62f, 1f, 0.24f));
    }
}
