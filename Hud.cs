using Godot;

/// <summary>
/// 全局 HUD。已注册为自动加载（Autoload），跨场景常驻。
/// 玩家在 _Ready 里主动调用 BindPlayer 注册自己，避免依赖查找时序。
/// </summary>
public partial class Hud : CanvasLayer
{
    // ── 武器格布局（单位 px，相对 WeaponRack 面板左上角）
    private const float CellSize = 46f;       // 格子边长
    private const float CellOrigin = 8f;      // 格子离金框内侧的距离
    private const float IconInset = 5f;       // 图标离格子边缘的距离（留呼吸空间）

    private Label _hpLabel;
    private TextureProgressBar _hpBar;
    private Label _deathLabel;
    private Box _player;

    private Panel _weaponCell;
    private TextureRect _weaponIcon;
    private Label _weaponNum;
    private StyleBoxFlat _cellIdle;
    private StyleBoxFlat _cellActive;

    // ── 变更检测：只有这几项真的变了才重建样式，不然每帧都刷一遍纯属浪费
    private int _shownSlot = -2;                  // -2 = 还没刷过
    private bool _shownHolding;
    private WeaponData _shownWeapon;

    public override void _Ready()
    {
        _hpLabel = GetNode<Label>("HpLabel");
        _hpBar = GetNode<TextureProgressBar>("HpBar");
        _deathLabel = GetNode<Label>("DeathLabel");
        _deathLabel.Visible = false;

        BuildWeaponCell();

        // 启动时是主菜单，没有玩家，整层先藏起来。
        // 进游戏后 Box._Ready 会调 BindPlayer，那时再显示。
        Visible = false;
    }

    /// <summary>由玩家在 _Ready 里调用：绑定当前场景的玩家。</summary>
    public void BindPlayer(Box player)
    {
        if (player == null || !IsInstanceValid(player)) return;

        Visible = true;   // 有玩家了，HUD 该出现了

        // 解绑旧的，避免残留信号
        if (_player != null && IsInstanceValid(_player))
        {
            _player.HpChanged -= OnHpChanged;
            _player.Died -= OnDied;
            _player.Revived -= OnRevived;
        }

        _player = player;
        _player.HpChanged += OnHpChanged;
        _player.Died += OnDied;
        _player.Revived += OnRevived;

        _deathLabel.Visible = false;
        Refresh();
        RefreshWeaponCell();
        GD.Print($"[Hud] 已绑定玩家 Hp={player.Hp}/{player.MaxHp}");
    }

    public override void _Process(double delta)
    {
        // 没有玩家 / 整层被藏起来（主菜单）时什么都不做
        if (!Visible || _player == null || !IsInstanceValid(_player)) return;

        if (!WeaponChanged()) return;
        RefreshWeaponCell();
    }

    public void Refresh()
    {
        if (_player == null || !IsInstanceValid(_player)) return;
        OnHpChanged(_player.Hp, _player.MaxHp);
    }

    private void OnHpChanged(int hp, int maxHp)
    {
        _hpBar.MaxValue = maxHp;
        _hpBar.Value = hp;
        _hpLabel.Text = hp + " / " + maxHp;
    }

    private void OnDied()
    {
        _deathLabel.Visible = true;
    }

    private void OnRevived()
    {
        _deathLabel.Visible = false;
    }

    // ── 武器格 ────────────────────────────────────────────────

    /// <summary>
    /// 血条下面那一格：只显示**当前手持**的那把武器，切武器就换图标，空手就是空格。
    /// 金色外框是 hud.tscn 里的 WeaponRack 面板（9-slice 贴图），
    /// 里面的格子、图标、槽位号都在代码里建。
    /// </summary>
    private void BuildWeaponCell()
    {
        var rack = GetNodeOrNull<Panel>("WeaponRack");
        if (rack == null)
        {
            GD.PushWarning("[Hud] 找不到 WeaponRack 节点，武器格不会显示");
            return;
        }

        // 空格：深棕底 + 中金边；有武器：更亮的底 + 亮金边。
        // 底色必须**不透明**：半透明会被天空的蓝色透上来，把棕色洗成死灰。
        _cellIdle = MakeCellStyle(new Color(0.17f, 0.12f, 0.07f), new Color(0.68f, 0.50f, 0.22f));
        _cellActive = MakeCellStyle(new Color(0.32f, 0.22f, 0.10f), new Color(1f, 0.87f, 0.47f));

        _weaponCell = new Panel
        {
            Position = new Vector2(CellOrigin, CellOrigin),
            Size = new Vector2(CellSize, CellSize),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _weaponCell.AddThemeStyleboxOverride("panel", _cellIdle);
        rack.AddChild(_weaponCell);

        _weaponIcon = new TextureRect
        {
            Position = new Vector2(IconInset, IconInset),
            Size = new Vector2(CellSize - IconInset * 2f, CellSize - IconInset * 2f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _weaponCell.AddChild(_weaponIcon);

        // 左上角显示槽位号，对应键盘 1~4；空手时不显示
        _weaponNum = new Label
        {
            Text = "",
            Position = new Vector2(2f, -4f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _weaponNum.AddThemeFontSizeOverride("font_size", 10);
        _weaponNum.AddThemeColorOverride("font_color", new Color(1f, 0.90f, 0.62f, 0.95f));
        _weaponNum.AddThemeColorOverride("font_outline_color", new Color(0.13f, 0.08f, 0.03f));
        _weaponNum.AddThemeConstantOverride("outline_size", 3);
        _weaponCell.AddChild(_weaponNum);
    }

    private static StyleBoxFlat MakeCellStyle(Color bg, Color border)
    {
        var s = new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
            AntiAliasing = false,
        };
        s.SetBorderWidthAll(3);
        s.SetCornerRadiusAll(0);
        return s;
    }

    /// <summary>现在真正举在手上的那把武器；空手返回 null。</summary>
    private WeaponData HeldWeapon()
    {
        if (!_player.Holding) return null;

        int slot = _player.CurrentSlot;
        if (slot < 0 || slot >= _player.Weapons.Length) return null;
        return _player.Weapons[slot];
    }

    /// <summary>把当前手持的武器刷到那一格上。</summary>
    private void RefreshWeaponCell()
    {
        if (_player == null || !IsInstanceValid(_player)) return;

        WeaponData w = HeldWeapon();
        bool has = w?.Icon != null;

        _weaponIcon.Texture = w?.Icon;
        _weaponIcon.Visible = has;
        _weaponIcon.Modulate = has ? Colors.White : new Color(0.6f, 0.6f, 0.6f);

        // 空手不显示槽位号，免得留个孤零零的数字
        _weaponNum.Text = has ? (_player.CurrentSlot + 1).ToString() : "";

        _weaponCell.AddThemeStyleboxOverride("panel", has ? _cellActive : _cellIdle);

        _shownWeapon = w;
        _shownSlot = _player.CurrentSlot;
        _shownHolding = _player.Holding;
    }

    /// <summary>槽位、手持状态、手上那把有没有换——三者只要变一个就要重刷。</summary>
    private bool WeaponChanged()
    {
        if (_player.CurrentSlot != _shownSlot) return true;
        if (_player.Holding != _shownHolding) return true;
        return !ReferenceEquals(HeldWeapon(), _shownWeapon);
    }
}
