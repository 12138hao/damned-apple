using Godot;

/// <summary>
/// 战场空气墙：建墙 + 靠近才渐显的视觉。
///
/// 两个 Arena 的墙原来各写一份，而且野兽苹果那边忘了加视觉——
/// 玩家撞上去时看不到任何边界，像是被空气卡住。现在统一走这里。
/// </summary>
public static class ArenaWalls
{
    private const string AirWallPath = "res://assets/ui/boss/air_wall.png";
    private const float AirWallSrcW = 512f;    // air_wall.png 的原始尺寸
    private const float AirWallSrcH = 2048f;

    /// <summary>
    /// 建一面墙。坐标是相对 parent 的局部坐标。
    /// visualWidth &lt;= 0 表示不加视觉（只挡人，看不见）。
    /// </summary>
    public static StaticBody2D Make(Node2D parent, Vector2 localPos, float height, float thickness,
                                    float visualWidth, float visualHeight)
    {
        var wall = new StaticBody2D
        {
            CollisionLayer = 1,      // 和地形同层，玩家 / 敌人都被挡住
            CollisionMask = 0,
            Position = localPos,
        };
        wall.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(thickness, height) }
        });

        if (visualWidth > 0f)
        {
            var vis = new Sprite2D
            {
                Name = "Visual",
                Texture = GD.Load<Texture2D>(AirWallPath),
                Modulate = new Color(1f, 1f, 1f, 0f),   // 初始全透明，玩家靠近才渐显
                Scale = new Vector2(visualWidth / AirWallSrcW, visualHeight / AirWallSrcH),
            };
            wall.AddChild(vis);
        }

        parent.AddChild(wall);
        return wall;
    }

    /// <summary>
    /// 玩家靠近哪面墙，哪面墙就渐显。wallLX / wallRX 是世界坐标。
    /// </summary>
    public static void UpdateAlpha(Node2D player, float wallLX, float wallRX,
                                   float showDistance, float maxAlpha,
                                   Sprite2D visL, Sprite2D visR, float dt)
    {
        if (player == null || !GodotObject.IsInstanceValid(player)) return;

        float dist = showDistance <= 0.01f ? 1f : showDistance;
        float px = player.GlobalPosition.X;

        float aL = Mathf.Clamp(1f - Mathf.Abs(px - wallLX) / dist, 0f, 1f) * maxAlpha;
        float aR = Mathf.Clamp(1f - Mathf.Abs(px - wallRX) / dist, 0f, 1f) * maxAlpha;

        FadeTo(visL, aL, dt);
        FadeTo(visR, aR, dt);
    }

    private static void FadeTo(Sprite2D vis, float targetAlpha, float dt)
    {
        if (vis == null || !GodotObject.IsInstanceValid(vis)) return;
        var c = vis.Modulate;
        c.A = Mathf.Lerp(c.A, targetAlpha, Mathf.Min(1f, dt * 8f));
        vis.Modulate = c;
    }
}
