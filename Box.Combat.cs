using Godot;

/// <summary>Box 的战斗：鼠标瞄准、朝向判定、空手连击、蓄力挥击、攻击输入。</summary>
/// <remarks>Box 的 partial 部分，字段仍声明在 Box.cs 里。</remarks>
public partial class Box
{
    /// <summary>用鼠标位置更新攻击方向。鼠标离得太近时保持上一次的方向，避免抖动。</summary>
    private void UpdateAim()
    {
        Vector2 toMouse = GetGlobalMousePosition() - GlobalPosition;
        if (toMouse.LengthSquared() < 9f) return;   // 3 像素以内忽略

        _aimAngle = ClampAimAngle(toMouse.Angle());
        _aimDir = new Vector2(Mathf.Cos(_aimAngle), Mathf.Sin(_aimAngle));
    }

    /// <summary>把瞄准角度限制在可攻击范围内，脚下的扇形打不到。</summary>
    private float ClampAimAngle(float rad)
    {
        float deg = Mathf.RadToDeg(rad);
        // 归一化到 -180 ~ 180
        deg = Mathf.Wrap(deg, -180f, 180f);

        // 落在脚下禁区里就贴到最近的边界（按距离判断，正下方时不会突然跳到一边）
        if (deg > AimBlockFromDeg && deg < AimBlockToDeg)
        {
            float toLow = Mathf.Abs(deg - AimBlockFromDeg);
            float toHigh = Mathf.Abs(deg - AimBlockToDeg);
            deg = (toLow <= toHigh) ? AimBlockFromDeg : AimBlockToDeg;
        }

        return Mathf.DegToRad(deg);
    }

    /// <summary>打出一拳。连击窗口内连续点会累加连击，打满时伤害和击退翻倍。</summary>
    private void StartFist()
    {
        AudioManager.Instance?.PlaySfx("attack", 0.9f, 1f + GD.Randf() * 0.06f);
        // 连击累加：窗口内算连击，否则从 1 开始
        _combo = _comboTimer > 0f ? Mathf.Min(_combo + 1, MaxCombo) : 1;
        _comboTimer = ComboWindow;
        _fistCd = FistCooldown;

        bool finisher = _combo >= MaxCombo;

        // 最后一拳更疼、推得更远
        int dmg = finisher ? FistDamage * 2 : FistDamage;
        float knock = finisher ? 320f : 120f;
        float reach = finisher ? FistReach * 1.25f : FistReach;

        // 出拳瞬间把人物朝向也对齐过去。
        // 否则「人物朝右、手臂朝左」会看起来像攻击方向反了。
        _facingRight = _aimDir.X >= 0f;

        // 鼠标在左半边用帧 16，右半边用帧 18。
        // 原本是 16↔17 / 18↔19 交替，但这两帧的头部像素对不齐
        // （实测差异 199 像素里 143 在头部），交替播放时眼睛会乱晃，
        // 所以固定用一帧，宁可少点微动感也不晃眼。
        _punchFrame = _aimDir.X < 0f ? 16 : 18;
        _punchTimer = FistTime;

        // 命中判定：在拳头伸出的位置查一个圆
        Vector2 hitCenter = GlobalPosition + _aimDir * reach;
        _fistShape.Radius = FistRadius * (finisher ? 1.2f : 1f);

        var space = GetWorld2D().DirectSpaceState;
        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = _fistShape,
            Transform = new Transform2D(0f, hitCenter),
            CollisionMask = 4,
            CollideWithBodies = true,
            CollideWithAreas = false,
        };

        foreach (var hit in space.IntersectShape(query))
        {
            if (hit["collider"].As<Node>() is Enemy enemy)
            {
                // 空手是单帧判定，无视受击无敌，这样连击才打得出来
                enemy.TryTakeHit(dmg, GlobalPosition, knock, 0.25f, true);
            }
        }
    }

    private void StartAttack()
    {
        AudioManager.Instance?.PlaySfx("swing", 0.85f, 1f + GD.Randf() * 0.08f);
        // 快照本招参数（武器可能在挥击过程中被卸下）
        var w = Current;
        _atkTime = w != null ? w.AttackTime : 0.22f;
        _atkStartDeg = w != null ? w.StartDeg : -75f;
        _atkSweepDeg = w != null ? w.AttackSweepDeg : 120f;
        _atkUpRatio = w != null ? w.KnockbackUpRatio : 0.32f;

        // 蓄满 + 这把武器支持突刺 → 这一招是突刺
        _atkThrust = w != null && w.ChargeThrust && _lastChargeRatio >= w.ThrustThreshold;
        if (_atkThrust)
        {
            _atkSweepDeg = 0f;      // 突刺不扫，方向锁死在瞄准线上
        }

        ApplyWeaponShape(w, _atkThrust);

        // 贴图不够长就补一道剑气
        if (_aura != null && w != null && w.AuraLength > 0f)
        {
            _aura.StartDist = w.AuraStartDist;
            _aura.Length = w.AuraLength;
            _aura.Fire();
        }

        _hitThisSwing.Clear();    // 新的一刀，清空命中记录
        _atkAim = _aimAngle;      // 出招瞬间锁定方向，挥击过程中不跟着鼠标飘
        _facingRight = Mathf.Cos(_atkAim) >= 0f;   // 人物朝向同步，避免"人朝右、刀朝左"

        _attackTimer = _atkTime;
        _attackCd = _atkTime + AttackCooldown;
        _weaponSprite.Visible = ShowWeaponSprite;
        _weapon.Monitoring = true;
    }

    private void UpdateAttack(float dt)
    {
        _attackCd = Mathf.Max(0f, _attackCd - dt);

        // 武器已经用 360° 旋转表示方向了，不要再 FlipH，
        // 否则「旋转 + 镜像」叠加会让贴图朝向错乱。
        _weaponSprite.FlipH = false;

        // 贴图大小按武器自己的 SpriteScale，突刺不额外拉伸
        // （横向贴图和斜向贴图的"长度方向"不一样，统一拉伸会变形）
        float iconScale = Current != null ? Current.SpriteScale : 1f;
        _weaponSprite.Scale = Vector2.One * iconScale;

        // 突刺：武器沿朝向往前送出去再收回（sin 曲线，出去一半再回来）
        float thrustExtend = 0f;
        if (_attackTimer > 0f && _atkThrust && Current != null)
        {
            float kk = 1f - Mathf.Max(0f, _attackTimer) / _atkTime;
            thrustExtend = Mathf.Sin(kk * Mathf.Pi) * Current.ThrustReach;
        }

        // 贴图偏移 = 握把对齐 + 突刺前伸
        Vector2 iconOffset = Current != null
            ? Current.SpriteOffset * Current.SpriteScale
            : Vector2.Zero;
        _weaponSprite.Position = iconOffset + new Vector2(thrustExtend, 0f);

        // 武器节点跟着朝向挪到左手/右手
        _weapon.Position = new Vector2(
            _facingRight ? Mathf.Abs(_weaponHome.X) : -Mathf.Abs(_weaponHome.X),
            _weaponHome.Y);

        if (_attackTimer <= 0f)
        {
            // 非攻击（含蓄力）：武器始终指向鼠标
            _weaponSprite.Visible = _holding && ShowWeaponSprite;
            _weapon.Rotation = _aimAngle + Mathf.DegToRad(Current != null ? Current.AngleOffset : WeaponAngleOffset);
            _weapon.Monitoring = false;
            _trail?.Clear();

            // 突刺结束，判定框恢复成普通形状（只恢复一次）
            if (_atkThrust)
            {
                _atkThrust = false;
                ApplyWeaponShape(Current, false);
            }
            return;
        }

        _attackTimer -= dt;

        // 360 度扫击：以出招瞬间的瞄准方向为中心，左右各扫一半
        float k = 1f - Mathf.Max(0f, _attackTimer) / _atkTime;
        float halfRad = Mathf.DegToRad(_atkSweepDeg) * 0.5f;

        // 扫击方向统一成「从上往下」：
        // 朝右时角度 -60° → +60° 就是从右上扫到右下；
        // 朝左时如果照搬会变成从下往上，所以要反过来扫（+60° → -60°）。
        bool sweepDownward = Mathf.Cos(_atkAim) >= 0f;
        float fromAngle = sweepDownward ? _atkAim - halfRad : _atkAim + halfRad;
        float toAngle = sweepDownward ? _atkAim + halfRad : _atkAim - halfRad;

        float angle = Mathf.Lerp(fromAngle, toAngle, k);
        float aimOffset = Current != null ? Current.AngleOffset : WeaponAngleOffset;
        _weapon.Rotation = angle + Mathf.DegToRad(aimOffset);   // 旋转整个武器节点，碰撞体跟着转

        // 气浪：从起始角画到当前角，越接近收招越淡（SwingTrail 用度数）
        // 突刺是一条直线，没有扇形气浪
        if (_trail != null && !_atkThrust)
        {
            float alpha = 1f - 0.55f * k;
            _trail.Show(
                Mathf.RadToDeg(fromAngle),
                Mathf.RadToDeg(angle),
                alpha);
        }

        // 命中判定：直接用物理空间查形状重叠（不依赖 Area2D 的 monitoring 时序）
        var space = GetWorld2D().DirectSpaceState;
        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = _weaponShape.Shape,
            Transform = _weaponShape.GlobalTransform,
            CollisionMask = 4,
            CollideWithBodies = true,
            CollideWithAreas = false,
        };
        foreach (var hit in space.IntersectShape(query))
        {
            if (hit["collider"].As<Node>() is Enemy enemy)
            {
                // 这一刀已经打过它了，跳过（防止一次挥击在多帧里反复结算）
                if (!_hitThisSwing.Add(enemy)) continue;
                // ignoreInvuln = true：让连续攻击的每一刀都能打中，
                // 不会被敌人 0.25 秒的受击无敌吃掉
                enemy.TryTakeHit(_pendingDamage, GlobalPosition, _pendingKnockback, _atkUpRatio, true);
            }
        }

        if (_attackTimer <= 0f)
        {
            _weaponSprite.Visible = _holding && ShowWeaponSprite;
            _weapon.Monitoring = false;
            _trail?.Clear();
        }
    }

    /// <summary>朝向：挥击时锁定出招方向，持武器跟鼠标，空手跟方向键。</summary>
    private void UpdateFacing()
    {
        // 朝向：挥击过程中锁定在出招方向，避免鼠标乱动导致角色翻面；
        // 其他持武器状态下跟鼠标；空手走动还是跟移动方向。
        if (_attackTimer > 0f)
        {
            _facingRight = Mathf.Cos(_atkAim) >= 0f;
        }
        else if (_inVehicle)
        {
            // 载具：车头跟着鼠标那半边，鼠标在左就朝左开，不管往哪边走
            _facingRight = _aimDir.X >= 0f;
        }
        else if (_holding || _charging)
        {
            _facingRight = _aimDir.X >= 0f;
        }
    }

    /// <summary>攻击输入：载具射击 / 蓄力挥击 / 空手连击，含连击计时与蓄力条。</summary>
    private void HandleAttackInput(float dt, bool inputLocked)
    {
        // 对话中：把蓄力清掉，避免对话结束后"继续蓄着"
        if (inputLocked && _charging)
        {
            _charging = false;
            _chargeTime = 0f;
            _chargeArc?.SetProgress(0f);
        }

        // 载具：左键发射子弹，不处理武器攻击（对话中不响应）
        if (_inVehicle)
        {
            if (!inputLocked) UpdateVehicleAttack(dt);
        }
        // 蓄力挥击：按住蓄力、松开挥出，伤害随按住时间从 Min 涨到 Max
        else if (!inputLocked && _holding && Current != null)
        {
            if (Input.IsActionJustPressed("attack") && _attackCd <= 0f)
            {
                _charging = true;
                _chargeTime = 0f;
                _chargeSfxPlayed = false;
            }

            if (_charging)
            {
                _chargeTime = Mathf.Min(_chargeTime + dt, ChargeMaxTime);

                // 蓄力音跟着蓄力条一起出现：短按没到 ChargeShowThreshold 就不发声，
                // 否则「点一下就砍」也会先响一声蓄力，听起来很吵。
                if (!_chargeSfxPlayed && _chargeTime >= ChargeShowThreshold)
                {
                    AudioManager.Instance?.PlaySfx("charge", 0.7f);
                    _chargeSfxPlayed = true;
                }

                if (_chargeArc != null)
                {
                    // 短按不显示蓄力条，按够时间才亮出来
                    if (_chargeTime >= ChargeShowThreshold)
                    {
                        _chargeArc.Position = new Vector2(_facingRight ? -38f : 38f, -6f);
                        _chargeArc.Scale = new Vector2(_facingRight ? 1f : -1f, 1f);
                        _chargeArc.SetProgress(
                            ChargeMaxTime <= ChargeShowThreshold
                                ? 1f
                                : (_chargeTime - ChargeShowThreshold) / (ChargeMaxTime - ChargeShowThreshold));
                    }
                    else
                    {
                        _chargeArc.SetProgress(0f);
                    }
                }

                if (Input.IsActionJustReleased("attack"))
                {
                    float ratio = ChargeMaxTime <= 0f ? 1f : _chargeTime / ChargeMaxTime;
                    _lastChargeRatio = ratio;

                    // 伤害和击退都按当前武器的区间、随蓄力比例取值
                    var w = Current;
                    if (w != null)
                    {
                        _pendingDamage = Mathf.RoundToInt(
                            Mathf.Lerp(w.DamageMin, w.DamageMax, ratio));
                        _pendingKnockback = Mathf.Lerp(w.KnockbackMin, w.KnockbackMax, ratio);
                    }
                    else
                    {
                        _pendingDamage = 0;
                        _pendingKnockback = 0f;
                    }

                    StartAttack();
                    _charging = false;
                    _chargeArc?.SetProgress(0f);
                }
            }
        }
        else
        {
            // 空手：按下即出拳，可以连点打连击
            if (Input.IsActionJustPressed("attack") && _fistCd <= 0f)
            {
                StartFist();
            }
        }

        // 连击窗口与出拳冷却倒计时
        _comboTimer = Mathf.Max(0f, _comboTimer - dt);
        _fistCd = Mathf.Max(0f, _fistCd - dt);
        _punchTimer = Mathf.Max(0f, _punchTimer - dt);
        if (_comboTimer <= 0f) _combo = 0;

        UpdateAttack(dt);
    }
}
