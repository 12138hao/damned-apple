using Godot;

/// <summary>
/// Box 的远程武器（粉笔）：按住左键按节奏发射、挥臂动作、左手粉笔盒。
/// </summary>
/// <remarks>
/// Box 的 partial 部分，字段仍声明在 Box.cs 里。
/// 远程和近战的差别只在三处：
///   1. 没有蓄力，按住左键就按 Cooldown 一直发
///   2. 挥动时不做近战形状判定，改成挥到 55% 时生成一颗投射物
///   3. 左手额外显示一个粉笔盒（不参与挥动）
/// 挥动的旋转动画仍然复用 Box.Combat.cs 里那套「从上往下扫」。
/// </remarks>
public partial class Box
{
    /// <summary>
    /// 左手粉笔盒（以及它外面那圈冷却弧）比右手基准位置再往下多少像素。
    /// 盒子缩放后高约 27px，往下 8px 才像"端在手里"，不然浮在手背上。
    /// 嫌高/嫌低就改这个数字，正数往下。
    /// </summary>
    private const float ChalkBoxDropY = 12f;

    /// <summary>远程武器的输入：CD 好了且按住左键就开始下一次挥动。</summary>
    private void UpdateRangedInput(float dt)
    {
        _rangedCd = Mathf.Max(0f, _rangedCd - dt);

        if (_attackTimer > 0f) return;                    // 上一发还在挥
        if (!Input.IsActionPressed("attack")) return;     // 按住才连发，点一下只发一颗
        if (_rangedCd > 0f) return;                       // 还在内置冷却里

        StartRangedFire(Current);
    }

    /// <summary>起手：锁定方向、开始挥臂。</summary>
    private void StartRangedFire(WeaponData w)
    {
        if (w == null) return;

        AudioManager.Instance?.PlaySfx("swing", 0.8f, 1.1f + GD.Randf() * 0.06f);

        _atkRanged = true;
        _rangedFired = false;
        _atkTime = Mathf.Max(0.05f, w.RangedWindup);
        _attackTimer = _atkTime;
        _attackCd = _atkTime + AttackCooldown;
        // 冷却从起手算，所以实际射速 = RangedCooldown（不会变成 CD + 动作时长）
        _rangedCd = Mathf.Max(_atkTime, w.RangedCooldown);
        _rangedCdTotal = _rangedCd;   // 冷却弧按这个总时长算弧长

        _atkAim = _aimAngle;
        _facingRight = Mathf.Cos(_atkAim) >= 0f;

        // 挥臂幅度比近战大（近战 120°，这里 160°）：像"把粉笔从头顶甩下来"
        _atkSweepDeg = 160f;

        // 远程武器手上不画粉笔了（只留左手那个盒子），
        // 这里不设贴图也不设缩放，只确保它不显示
        _weaponSprite.Visible = HandSpriteVisible;
        _weapon.Monitoring = false;              // 远程不做近战判定
    }

    /// <summary>挥到 55% 时射出一发。由 UpdateAttack 每帧调用。</summary>
    private void UpdateRangedFire(float k)
    {
        if (_rangedFired || k < 0.55f) return;

        var w = Current;
        if (w == null) return;

        _rangedFired = true;
        FireChalk(w);
    }

    private void FireChalk(WeaponData w)
    {
        // CurrentScene 在正常游戏里就是关卡根节点；
        // 用 -s 脚本直接实例化场景调试时它是 null，退回玩家的父节点当容器。
        var scene = GetTree().CurrentScene ?? GetParent();
        if (scene == null) return;

        // 用出招瞬间锁定的方向，不跟鼠标飘（否则挥到一半动鼠标会歪）
        Vector2 dir = new Vector2(Mathf.Cos(_atkAim), Mathf.Sin(_atkAim));

        var p = new ChalkProjectile
        {
            // 远程伤害读的是 RangedDamage，不是 DamageMin / DamageMax
            // （那两个是近战蓄力区间，IsRanged 勾上后在检查器里是藏起来的）。
            // 这里留一层兜底：万一 RangedDamage 没配（<= 0），退回武器最高伤害，
            // 免得「改了 DamageMax 却发现粉笔伤害纹丝不动」这种坑再踩一次。
            Damage = w.RangedDamage > 0 ? w.RangedDamage : w.DamageMax,
            Speed = w.RangedSpeed,
            MaxDistance = w.RangedMaxDistance,
            Radius = w.ProjectileRadius,
            Knockback = w.ProjectileKnockback,
            ProjectileTexture = w.ProjectileIcon != null ? w.ProjectileIcon : w.Icon,
            SpriteScale = w.ProjectileScale,
            SpriteThickness = w.ProjectileThickness,
        };

        // 挂在场景根上而不是玩家下面：玩家跑动时不会把粉笔"拖着走"
        scene.AddChild(p);
        p.GlobalPosition = GlobalPosition + dir * w.ProjectileSpawnDist;
        p.Launch(dir);
    }

    /// <summary>左手粉笔盒：拿着远程武器时显示在朝向的反侧。</summary>
    private void UpdateChalkBox()
    {
        if (_chalkBox == null) return;

        var w = Current;
        bool show = _holding && w != null && w.IsRanged && ShowWeaponSprite;
        _chalkBox.Visible = show;
        if (!show) return;

        _chalkBox.Texture = w.Icon;
        _chalkBox.Scale = Vector2.One * w.SpriteScale;
        _chalkBox.FlipH = !_facingRight;

        // 右手位置是 _weaponHome，左手就取反
        float x = _facingRight ? -Mathf.Abs(_weaponHome.X) : Mathf.Abs(_weaponHome.X);
        _chalkBox.Position = new Vector2(x, _weaponHome.Y + ChalkBoxDropY);
    }

    /// <summary>
    /// 粉笔盒外面那圈内置冷却弧。冷却走完 / 换武器 / 空手时整条消失。
    /// </summary>
    private void UpdateRangedCdArc()
    {
        if (_cdArc == null) return;

        var w = Current;
        bool active = _holding && w != null && w.IsRanged && ShowWeaponSprite
                      && _rangedCd > 0.001f && _rangedCdTotal > 0.001f;

        if (!active)
        {
            _cdArc.Clear();
            return;
        }

        _cdArc.SetCooldown(_rangedCdTotal, _rangedCd);

        // 和左手盒子同一个圆心：朝右时在角色左边，朝左时镜像到右边。
        // Scale.X = -1 直接把整个弧镜像过去，省得在脚本里算反向角度。
        float x = _facingRight ? -Mathf.Abs(_weaponHome.X) : Mathf.Abs(_weaponHome.X);
        _cdArc.Position = new Vector2(x, _weaponHome.Y + ChalkBoxDropY);
        _cdArc.Scale = new Vector2(_facingRight ? 1f : -1f, 1f);
    }
}
