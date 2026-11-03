using Godot;

/// <summary>
/// 可拾取道具：靠近后按 E 捡起，并通知玩家获得了哪一格武器。
/// </summary>
public partial class Pickup : Area2D
{
    [Export] public int Slot = 1;                    // 放进第几个武器格
    [Export] public WeaponData Weapon;               // 这把武器的数据（不填 = 只占格子没有战斗参数）
    [Export] public bool AutoEquip = true;           // 捡起后是否立刻举在手上（false = 只进背包，按数字键才拿出来）

    /// <summary>
    /// 覆盖这把武器的内置发射冷却（秒）。只有远程武器（粉笔这类）才有意义。
    /// 填 0 或负数 = 不覆盖，用 Weapon 资源里自带的值。
    /// 改的是捡起来那一刻的副本，不会写回 .tres，也不会影响别处引用同一个资源的地方。
    /// </summary>
    [Export] public float RangedCooldownOverride = 0f;

    [Export] public float BobSpeed = 3.0f; // 上下浮动的速度
    [Export] public float BobHeight = 4.0f;
    [Export] public string PickupId = "";  // 留空则用节点路径，切场景回来不会重复刷

    private Sprite2D _sprite;
    private Vector2 _basePos;
    private float _t;
    private Box _player;
    private bool _inRange;
    private bool _taken;
    private string _key;

    public override void _Ready()
    {
        // 已经捡过就不要再刷出来
        _key = string.IsNullOrEmpty(PickupId) ? GetPath().ToString() : PickupId;
        if (GameState.I != null && GameState.I.HasPickedUp(_key))
        {
            _taken = true;
            QueueFree();
            return;
        }

        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (_sprite != null && Weapon?.Icon != null) _sprite.Texture = Weapon.Icon;
        _basePos = Position;
        BodyEntered += OnEnter;
        BodyExited += OnExit;
    }

    public override void _Process(double delta)
    {
        if (_taken) return;

        // 上下轻轻浮动，让地上的道具更显眼
        _t += (float)delta * BobSpeed;
        Position = _basePos + new Vector2(0f, Mathf.Sin(_t) * BobHeight);

        // 站在范围内按 E 才捡
        if (_inRange && _player != null && IsInstanceValid(_player)
            && (GameState.I == null || !GameState.I.DialogueActive)
            && Input.IsActionJustPressed("interact"))
        {
            _taken = true;
            GameState.I?.MarkPickedUp(_key);
            _player.GiveWeapon(Slot, BuildWeapon(), AutoEquip);
            AudioManager.Instance?.PlaySfx("pickup", 0.9f);
            QueueFree();
        }
    }

    /// <summary>
    /// 决定这次捡起来要交给玩家哪份武器数据。
    /// 没填覆盖值就直接用原资源；填了就复制一份再改，绝不污染 .tres。
    /// </summary>
    private WeaponData BuildWeapon()
    {
        if (Weapon == null) return null;
        if (RangedCooldownOverride <= 0f) return Weapon;

        var copy = (WeaponData)Weapon.Duplicate();
        copy.RangedCooldown = RangedCooldownOverride;
        // 副本的 ResourcePath 是空字符串（实测），存档靠 SourcePath 回查原 .tres
        copy.SourcePath = Weapon.ResourcePath;
        return copy;
    }

    private void OnEnter(Node2D body)
    {
        if (body is Box b)
        {
            _player = b;
            _inRange = true;
        }
    }

    private void OnExit(Node2D body)
    {
        if (body is Box)
        {
            _inRange = false;
        }
    }
}
