using Godot;

/// <summary>
/// 可坍塌地板。平时和普通地面一样（StaticBody2D），
/// 调用 Collapse() 后先抖动，然后禁用碰撞体、贴图下落淡出、最后自毁。
///
/// 用法：把这块地板放在战场下方，宽度和 BossArena.ArenaSize.X 一致，
/// 左右正好和两侧正常的 road_break 接上。
/// BossArena 在 Boss 消失后会调 Collapse()。
/// </summary>
public partial class CollapseFloor : StaticBody2D
{
    [ExportGroup("坍塌表现")]
    [Export] public float ShakeTime = 0.25f;       // 抖动多久（秒），短一点更连贯
    [Export] public float ShakeStrength = 7f;      // 抖动幅度（像素）
    [Export] public float FallTime = 0.7f;         // 下落淡出多久
    [Export] public float FallDistance = 340f;     // 下落多远

    private bool _collapsed;
    private bool _shapeOff;
    private float _t;

    private CollisionShape2D _shape;
    private Sprite2D _sprite;
    private Vector2 _spriteStart;

    public override void _Ready()
    {
        _shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (_sprite != null) _spriteStart = _sprite.Position;

        SetProcess(false);   // 平时不跑 _Process
    }

    /// <summary>贴图原始宽度（road_seamless_1024x64.png）</summary>
    private const float TextureWidth = 1024f;

    /// <summary>按指定宽度调整碰撞体和贴图（由 BossArena 调用，跟随 ArenaSize.X）。</summary>
    public void SetWidth(float width)
    {
        if (_shape?.Shape is RectangleShape2D rect)
            rect.Size = new Vector2(width, rect.Size.Y);

        // 用 Scale 横向拉伸，不用 region_rect：
        // region_rect 超过贴图实际宽度时 Godot 不绘制超出部分，地板会「消失」。
        if (_sprite != null)
            _sprite.Scale = new Vector2(width / TextureWidth, _sprite.Scale.Y);
    }

    /// <summary>开始坍塌：抖动 → 掉下去并消失。</summary>
    public void Collapse()
    {
        if (_collapsed) return;
        _collapsed = true;
        _t = 0f;
        SetProcess(true);
        AudioManager.Instance?.PlaySfx("collapse", 1f);
    }

    public override void _Process(double delta)
    {
        if (!_collapsed) return;

        _t += (float)delta;

        // ① 抖动阶段
        if (_t < ShakeTime)
        {
            if (_sprite != null)
                _sprite.Position = _spriteStart + new Vector2(
                    (GD.Randf() - 0.5f) * ShakeStrength * 2f,
                    (GD.Randf() - 0.5f) * ShakeStrength * 2f);
            return;
        }

        // ② 坍塌：立刻关碰撞，让人掉下去
        if (!_shapeOff)
        {
            _shapeOff = true;
            if (_shape != null) _shape.SetDeferred("disabled", true);
        }

        float k = Mathf.Min(1f, (_t - ShakeTime) / FallTime);

        if (_sprite != null)
        {
            _sprite.Position = _spriteStart + new Vector2(0f, FallDistance * k * k);   // 加速下坠
            _sprite.Modulate = new Color(1f, 1f, 1f, 1f - k);
        }

        if (k >= 1f)
        {
            SetProcess(false);
            QueueFree();
        }
    }
}
