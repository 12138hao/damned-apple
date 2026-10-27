using Godot;

/// <summary>
/// 地上的载具：按 E 捡起来（进"背包"），之后按 R 乘坐 / 下车。
///
/// 和武器拾取物（Pickup.cs）不一样——载具不是武器，捡到后由玩家自己按 R 切换。
/// 挂在一个 Area2D 上，`Collision Mask` 必须是 2（玩家层），否则检测不到玩家。
/// </summary>
public partial class VehiclePickup : Area2D
{
    [Export] public Texture2D VehicleSheet;      // 乘坐后人物用的贴图（留空 = 不换贴图）
    [Export] public int VehicleHframes = 1;      // 那张贴图横排几帧
    [Export] public Texture2D IconTexture;       // 摆在地上时的贴图（留空则用 VehicleSheet）
    [Export] public float IconScale = 0.5f;      // 摆在地上时的缩放（128 × 这个值）
    [Export] public float RideScale = 0.7f;      // 乘坐后人物贴图的缩放（128 × 这个值）
    [Export] public float BobSpeed = 3f;         // 上下浮动速度
    [Export] public float BobHeight = 4f;
    [Export] public string PickupId = "";        // 留空则用节点路径

    private Sprite2D _sprite;
    private Vector2 _basePos;
    private float _t;
    private Box _player;
    private bool _inRange;
    private bool _taken;
    private string _key;

    public override void _Ready()
    {
        _key = string.IsNullOrEmpty(PickupId) ? GetPath().ToString() : PickupId;
        if (GameState.I != null && GameState.I.HasPickedUp(_key))
        {
            _taken = true;
            QueueFree();
            return;
        }

        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        // 地上显示用 IconTexture（VehicleSheet 是 8 帧的乘坐贴图，直接用会变成一大条）
        if (_sprite != null)
        {
            if (IconTexture != null) _sprite.Texture = IconTexture;
            else if (VehicleSheet != null) _sprite.Texture = VehicleSheet;

            // 地上图标大小由这个节点自己管，不依赖场景里手调的 Scale
            _sprite.Scale = Vector2.One * IconScale;
        }

        _basePos = Position;
        BodyEntered += OnEnter;
        BodyExited += OnExit;
    }

    public override void _Process(double delta)
    {
        if (_taken) return;

        // 上下浮动，让地上的东西更显眼
        _t += (float)delta * BobSpeed;
        Position = _basePos + new Vector2(0f, Mathf.Sin(_t) * BobHeight);

        if (!_inRange || _player == null || !IsInstanceValid(_player)) return;
        if (GameState.I != null && GameState.I.DialogueActive) return;

        if (Input.IsActionJustPressed("interact"))
        {
            _taken = true;
            GameState.I?.MarkPickedUp(_key);
            _player.GiveVehicle(VehicleSheet, VehicleHframes, RideScale);
            QueueFree();
        }
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
        if (body is Box) _inRange = false;
    }
}
