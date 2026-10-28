using Godot;

/// <summary>Box 的武器：武器槽切换、贴图与碰撞形状、剑气。</summary>
/// <remarks>Box 的 partial 部分，字段仍声明在 Box.cs 里。</remarks>
public partial class Box
{
    /// <summary>捡起武器（由 Pickup 调用）。slot 从 1 开始。</summary>
    public void GiveWeapon(int slot, WeaponData data = null)
    {
        int index = slot - 1;
        if (data != null && index >= 0 && index < Weapons.Length)
        {
            Weapons[index] = data;      // 把武器存进这一格
        }
        EquipAt(index);
        _holding = true;
    }

    /// <summary>从 from 开始往后找下一个有武器的槽，找不到返回 -1。</summary>
    private int FindNextWeaponSlot(int from)
    {
        for (int k = 1; k <= Weapons.Length; k++)
        {
            int idx = ((from + k) % Weapons.Length + Weapons.Length) % Weapons.Length;
            if (idx < Weapons.Length && Weapons[idx] != null) return idx;
        }
        return -1;
    }

    private void EquipAt(int index)
    {
        if (index >= 0 && index < Weapons.Length && Weapons[index] != null)
        {
            _currentSlot = index;
            Current = Weapons[index];
            UpdateWeaponVisual();
            ApplyWeaponShape(Current, false);   // 切换武器时重置成普通形状
            if (_weaponSprite != null && Current != null)
                _weaponSprite.Scale = Vector2.One * Current.SpriteScale;
        }
        else
        {
            _currentSlot = -1;
            Current = null;
            _holding = false;
            if (_weaponSprite != null) _weaponSprite.Visible = false;
        }
    }

    /// <summary>按当前武器刷新手持贴图。</summary>
    private void UpdateWeaponVisual()
    {
        if (_weaponSprite == null) return;                  // _Ready 之前调用时保护
        if (Current == null) return;

        if (Current.Icon != null) _weaponSprite.Texture = Current.Icon;

        // 握把对齐节点原点：偏移也要乘缩放，否则放大后握把会跑偏
        _weaponSprite.Scale = Vector2.One * Current.SpriteScale;
        _weaponSprite.Position = Current.SpriteOffset * Current.SpriteScale;
    }

    /// <summary>
    /// 按武器参数调整攻击判定框。
    /// 普通攻击用 RangeScale 整体放大；突刺用 ThrustRangeScale 且把框压成一条细线。
    /// </summary>
    private void ApplyWeaponShape(WeaponData w, bool thrust)
    {
        if (_weaponShape == null) return;

        float scale = 1f;
        if (w != null)
        {
            scale = thrust ? w.ThrustRangeScale : w.RangeScale;
        }

        // 尺寸和位置一起放大，远端距离才是真正的 N 倍
        _weaponShape.Scale = new Vector2(scale, scale);
        _weaponShape.Position = _baseWeaponShapePos * scale;

        if (thrust && w != null)
        {
            // 突刺：细长的线
            _weaponShape.Shape = new RectangleShape2D
            {
                Size = new Vector2(w.ThrustWidth, 42f)
            };
        }
        else if (_baseWeaponShape != null)
        {
            _weaponShape.Shape = _baseWeaponShape;
        }
    }

    /// <summary>1~4 切武器（同槽再按收起）、R 上下车。</summary>
    private void HandleWeaponSwitch(bool inputLocked)
    {
        // 1/2/3/4 切换武器：有武器的槽可以切，同一槽再按一次收起
        for (int i = 0; i < 4; i++)
        {
            if (!Input.IsActionJustPressed($"slot{i + 1}")) continue;

            // 载具状态下不能换武器（换了也挥不出去，只会让木棒举在车上）
            if (_inVehicle) break;

            // 该槽没有武器就忽略
            if (i >= Weapons.Length || Weapons[i] == null) break;

            if (i == 0)
            {
                // 1 键在「有武器的槽」之间循环：举起 → 收起 → 下一个 → 举起 → 收起…
                // 例：木棒举起 → 收起 → 长枪举起 → 收起 → 木棒举起
                // 只要手上举着东西就先收起，不管当前是哪个槽
                if (_holding)
                {
                    _holding = false;
                }
                else
                {
                    int next = FindNextWeaponSlot(_currentSlot);
                    if (next >= 0)
                    {
                        EquipAt(next);
                        _holding = true;
                    }
                }
            }
            else if (_currentSlot == i)
            {
                _holding = !_holding;          // 其他槽保持原逻辑：举起 / 收起
            }
            else
            {
                EquipAt(i);
                _holding = true;
            }
            break;
        }

        // R 键：上车 / 下车（对话中不响应）
        if (!inputLocked && Input.IsActionJustPressed("vehicle"))
        {
            ToggleVehicle();
        }
    }
}
