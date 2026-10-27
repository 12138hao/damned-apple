using Godot;
using System.Collections.Generic;

/// <summary>
/// 让所有子节点（云）水平缓慢漂移，飘出右边界后从左边绕回来。
/// 挂在 Node2D 上，把云朵 Sprite2D 作为子节点摆好位置即可。
/// </summary>
public partial class Clouds : Node2D
{
    [Export] public float Speed = 8f;          // 每秒漂多少像素，越大越快
    [Export] public float LeftEdge = -260f;    // 绕回时的左边界
    [Export] public float RightEdge = 3260f;   // 超过这个 x 就绕回左边

    // 缓存子节点：GetChildren() 每帧都会新建数组，是 GC 卡顿的主要来源
    private Node2D[] _clouds = System.Array.Empty<Node2D>();

    public override void _Ready()
    {
        var list = new List<Node2D>();
        foreach (Node child in GetChildren())
        {
            if (child is Node2D n) list.Add(n);
        }
        _clouds = list.ToArray();
    }

    public override void _Process(double delta)
    {
        float dx = Speed * (float)delta;
        float span = RightEdge - LeftEdge;

        // 用索引循环，避免 foreach 分配枚举器
        for (int i = 0; i < _clouds.Length; i++)
        {
            Node2D cloud = _clouds[i];
            if (!IsInstanceValid(cloud)) continue;

            Vector2 p = cloud.Position;
            p.X += dx;
            if (p.X > RightEdge)
            {
                p.X -= span;
            }
            cloud.Position = p;
        }
    }
}
