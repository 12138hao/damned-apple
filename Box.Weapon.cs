using Godot;

/// <summary>Box 的武器：武器槽切换、贴图与碰撞形状、剑气。</summary>
/// <remarks>Box 的 partial 部分，字段仍声明在 Box.cs 里。</remarks>
public partial class Box
{
    /// <summary>捡起武器（由 Pickup 调用）。slot 从 1 开始。</summary>
    /// <param name="equip">true = 捡起来就举在手上（木棍的旧行为）；false = 只进背包，要按数字键才拿出来</param>
    public void GiveWeapon(int slot, WeaponData data = null, bool equip = true)
    {
        int index = slot - 1;
        if (data != null && index >= 0 && index < Weapons.Length)
        {
            Weapons[index] = data;      // 把武器存进这一格
        }

        if (equip)
        {
            EquipAt(index);
            _holding = true;
        }
    }

    // ─────────────────────────── 伤害取值 ───────────────────────────
    // 数值来源只有一个：武器自己的 .tres（assets/weapons/*.tres）。
    // 以前这里还有一层"Box 上的覆盖值优先"，结果每张地图各存一份、改了一处别处不生效，已经去掉。

    /// <summary>近战实际伤害区间：直接取 .tres 的 DamageMin / DamageMax。</summary>
    private void MeleeDamageRange(WeaponData w, out int min, out int max)
    {
        min = Mathf.Max(0, w.DamageMin);
        max = Mathf.Max(min, w.DamageMax);   // 只填了 min 时别让 Lerp 倒着走
    }

    /// <summary>远程每发伤害：RangedDamage；没填就退回 DamageMax / DamageMin。</summary>
    private int RangedDamageOf(WeaponData w)
    {
        if (w.RangedDamage > 0) return w.RangedDamage;
        return w.DamageMax > 0 ? w.DamageMax : w.DamageMin;
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

    /// <summary>
    /// 从 from 之后往后找下一个有武器的槽（**不绕回**），没有则返回 -1。
    /// 和上面那个绕回版不同：绕回版永远能找到东西，这个版能判断"后面已经没有了"。
    /// </summary>
    private int FindNextWeaponSlotAfter(int from)
    {
        for (int idx = from + 1; idx < Weapons.Length; idx++)
        {
            if (Weapons[idx] != null) return idx;
        }
        return -1;
    }

    private void EquipAt(int index)
    {
        // 换武器时把远程的挥动状态清干净，
        // 否则从粉笔切到木棍时 _atkRanged 还是 true，木棍会打不出判定
        _atkRanged = false;
        _rangedFired = false;

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

        // 远程武器手上拿的是投射物本身（粉笔），粉笔盒由 UpdateChalkBox 画在左手
        Texture2D tex = (Current.IsRanged && Current.ProjectileIcon != null)
            ? Current.ProjectileIcon
            : Current.Icon;
        if (tex != null) _weaponSprite.Texture = tex;

        float sc = Current.IsRanged ? Current.ProjectileScale : Current.SpriteScale;

        // 握把对齐节点原点：偏移也要乘缩放，否则放大后握把会跑偏。
        // 粉笔以中心为握把，不做偏移。
        _weaponSprite.Scale = Vector2.One * sc;
        _weaponSprite.Position = Current.IsRanged ? Vector2.Zero : Current.SpriteOffset * Current.SpriteScale;
    }

    /// <summary>
    /// 手上那张贴图该不该显示。
    /// 远程武器（粉笔）手上不画粉笔，只留左手那个粉笔盒，
    /// 所以远程一律返回 false，不管 ShowWeaponSprite 开没开。
    /// 近战照旧听 ShowWeaponSprite。
    /// </summary>
    private bool HandSpriteVisible =>
        ShowWeaponSprite && !(Current != null && Current.IsRanged);

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
                // 1 键在「空手 + 所有武器」之间循环，一圈只出现一次空手：
                //   空手 → 槽1 → 槽2 → … → 空手 → 槽1 → …
                // 例（木棍在槽1、尺子在槽2）：空手 → 木棍 → 尺子 → 空手 → 木棍…
                // 和原来的区别：原来「举着就收起」，会在每两把之间插一个空手；
                // 现在只有「后面没有武器了」才收起，所以两把武器是连着切的。
                if (!_holding)
                {
                    // 空手 → 拿起第一把（槽位最小的那把）
                    int first = FindNextWeaponSlot(-1);
                    if (first >= 0)
                    {
                        EquipAt(first);
                        _holding = true;
                    }
                }
                else
                {
                    // 手上举着 → 换下一把；后面再没有了就收起，回到空手
                    int next = FindNextWeaponSlotAfter(_currentSlot);
                    if (next >= 0)
                    {
                        EquipAt(next);
                        _holding = true;
                    }
                    else
                    {
                        _holding = false;
                        _currentSlot = -1;   // 清掉，下一轮从第一把重新开始
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
