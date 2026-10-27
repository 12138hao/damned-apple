using Godot;

/// <summary>
/// 存档点：走过去按 E，把死亡后的复活位置改到这里。
///
/// 用法：把 RevivePoint.tscn 拖进任意场景，摆到地面上即可，不需要写代码。
/// 复制多份也各自独立（用节点路径区分），互不影响。
///
/// 行为：
///   · 按 E → 主角跳起来喊「存档成功！」→ 这个存档点消失
///   · 之后在**别的**存档点存档时，这个会自动重新出现
///
/// 外观是代码画的（地面光圈 + 光柱），没有贴图依赖。
/// 想换成贴图就在下面加一个 Sprite2D，然后把 _Draw 里的内容删掉。
/// </summary>
public partial class RevivePoint : Area2D
{
    [Export] public Color IdleColor = new Color(0.45f, 0.7f, 1f);   // 光圈颜色
    [Export] public float RingRadius = 24f;                          // 地面光圈半径
    [Export] public float PillarHeight = 70f;                        // 光柱高度
    [Export] public string SaveText = "存档成功！";                  // 主角说的话

    private Box _player;
    private CollisionShape2D _shape;
    private bool _inRange;
    private bool _taken;      // 已经被这次存档用掉了（消失中）
    private float _t;
    private string _key;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 2;      // 只检测玩家层
        Monitoring = true;

        _shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");

        // 存档点的唯一标识 = 场景路径 + 节点路径。
        // 光用节点路径不行：不同场景里都叫 /root/Main/RevivePoint，会互相干扰
        // （在 firstry 存档会把 house_inside 的也变成"已用掉"）。
        string scenePath = Owner?.SceneFilePath
                           ?? GetTree().CurrentScene?.SceneFilePath
                           ?? "";
        _key = scenePath + "::" + GetPath();

        // 上次就是在这里存的档 → 一进来就是消失状态
        if (GameState.I != null && GameState.I.RevivePointKey == _key)
            ApplyTaken(true);

        BodyEntered += OnEnter;
        BodyExited += OnExit;
    }

    public override void _Process(double delta)
    {
        // 别的存档点被激活了 → 这个重新出现
        if (_taken && GameState.I != null && GameState.I.RevivePointKey != _key)
            ApplyTaken(false);

        if (_taken) return;   // 消失中，不处理交互

        _t += (float)delta;
        QueueRedraw();        // 呼吸动画

        if (!_inRange || _player == null || !IsInstanceValid(_player)) return;
        if (GameState.I != null && GameState.I.DialogueActive) return;

        if (Input.IsActionJustPressed("interact"))
        {
            // ① 主角跳起来说话（音效和拾取共用同一个 pickup，想分开就改成别的 key）
            AudioManager.Instance?.PlaySfx("pickup", 0.9f);
            _player.DoSaveCheer(SaveText);

            // ② 记录存档点
            _player.SetRevivePoint(GlobalPosition);

            // ③ 存档顺便把血回满
            _player.HealFull();
            if (GameState.I != null)
            {
                GameState.I.RevivePointKey = _key;
                GameState.I.ReviveScenePath = GetTree().CurrentScene?.SceneFilePath ?? "";
                GameState.I.RevivePosition = GlobalPosition;
            }

            // ④ 自己消失
            ApplyTaken(true);
        }
    }

    /// <summary>切换「已用掉」状态：隐藏/显示 + 开关碰撞体。</summary>
    private void ApplyTaken(bool taken)
    {
        _taken = taken;
        Visible = !taken;
        if (_shape != null) _shape.SetDeferred("disabled", taken);
    }

    public override void _Draw()
    {
        Color c = IdleColor;
        float pulse = 1f + 0.08f * Mathf.Sin(_t * 3f);

        // ① 贴地的椭圆光圈（压扁成俯视效果）
        float rx = RingRadius * pulse;
        float ry = rx * 0.42f;
        const int seg = 36;
        var ring = new Vector2[seg];
        for (int i = 0; i < seg; i++)
        {
            float a = i / (float)seg * Mathf.Tau;
            ring[i] = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
        }
        for (int i = 0; i < seg; i++)
        {
            DrawLine(ring[i], ring[(i + 1) % seg], new Color(c, 0.9f), 2f, true);
        }

        // ② 中心光柱（往上收窄、渐淡）
        var pillar = new Vector2[]
        {
            new(-7f, 0f),
            new(7f, 0f),
            new(2.5f, -PillarHeight),
            new(-2.5f, -PillarHeight),
        };
        DrawColoredPolygon(pillar, new Color(c, 0.2f));

        // ③ 中心亮点
        DrawCircle(Vector2.Zero, 4.5f * pulse, new Color(c, 0.95f));
    }

    private void OnEnter(Node2D body)
    {
        if (body is Box b) { _player = b; _inRange = true; }
    }

    private void OnExit(Node2D body)
    {
        if (body is Box) _inRange = false;
    }
}
