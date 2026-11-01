using Godot;

/// <summary>
/// 野兽苹果 · 技能 1：斜向飞扑（走位 → 蓄力 2 秒 → 正弦拱弧横扫）。
///
/// 这是 <see cref="BeastAppleBoss"/> 的一部分（partial class），
/// 状态机主循环在 BeastAppleBoss.cs，这里只放本技能的实现。
/// </summary>
public partial class BeastAppleBoss
{
    // ══════════════════════════ 走位：先跳到边界

    /// <summary>
    /// 放技能之前先「跳」到战场的一侧边界，而不是原地瞬移过去。
    /// 两个原因：
    ///   1. 瞬移看不见过程，玩家会觉得 Boss 凭空出现在脸上；
    ///   2. 原来蓄力时 Boss 站在战场中间，它的碰撞体比人宽，把玩家挡在绿圈外面，
    ///      「站进圈里蹲下」这件事根本做不到。先走位到边界，中间就空出来了。
    /// </summary>
    private void StartReposition(float toX, AfterRepo next)
    {
        _st = St.Reposition;
        _repoFromX = GlobalPosition.X;
        _repoToX = toX;
        _repoT = 0f;
        _repoDur = Mathf.Max(0.15f, RepositionTime);
        _repoNext = next;

        Velocity = Vector2.Zero;
        Rotation = 0f;
        if (_sprite != null) { _sprite.Position = Vector2.Zero; _sprite.Scale = _baseScale; _sprite.Rotation = 0f; }
        ApplyTexture(toX >= _repoFromX ? RightTexture : LeftTexture);
        AudioManager.Instance?.PlaySfx("beast_jump", 0.85f, 1f);
    }

    private void UpdateReposition(float dt)
    {
        _repoT += dt;
        float p = Mathf.Clamp(_repoT / _repoDur, 0f, 1f);

        float x = Mathf.Lerp(_repoFromX, _repoToX, p);
        float y = _groundCenterY - RepositionArcHeight * Mathf.Sin(Mathf.Pi * p);
        GlobalPosition = new Vector2(x, y);

        // 空中拉长一点，看得出是「跳」过去的
        if (_sprite != null)
        {
            float stretch = Mathf.Sin(Mathf.Pi * p);
            _sprite.Scale = _baseScale * new Vector2(1f - 0.10f * stretch, 1f + 0.14f * stretch);
        }

        if (p < 1f) return;

        GlobalPosition = new Vector2(_repoToX, _groundCenterY);
        if (_sprite != null) _sprite.Scale = _baseScale;
        AudioManager.Instance?.PlaySfx("beast_slam", 0.5f, 1.25f);
        EmitSignal(SignalName.ShakeRequested, 5f, 0.14f);

        if (_repoNext == AfterRepo.Lunge) BeginLungeCharge();
        else BeginCharge();
    }

    // ══════════════════════════ 技能 1：横贯飞扑

    /// <summary>技能 1 第一步：随机挑一个方向，跳到起手那一侧的边界。</summary>
    private void StartLunge()
    {
        _lungeDir = GD.Randf() < 0.5f ? 1f : -1f;      // +1 = 往右飞
        float startX = _lungeDir > 0f ? MinCenterX : MaxCenterX;
        StartReposition(startX, AfterRepo.Lunge);
    }

    /// <summary>到了边界才开始蓄力：Boss 在边上，战场中间空着，玩家进得去绿圈。</summary>
    private void BeginLungeCharge()
    {
        _st = St.LungeCharge;
        _timer = LungeChargeTime;
        _animT = 0f;
        _lungeHitDone = false;
        FacePlayer();

        // 地面上那个绿色安全圈（只有一个扁椭圆，没有轨道 / 圆柱 / 进度条）
        _lungeWarn = new BeastLungeWarning();
        SpawnRoot.AddChild(_lungeWarn);   // 挂进容器，玩家死了能一起清掉
        _lungeWarn.Setup(FloorY, ZoneCenterX, ZoneHalfWidth, LungeChargeTime);
        // 蓄力这段刻意不放音效（绿圈本身 + 身体后仰已经够提示了）
    }

    private void UpdateLungeCharge(float dt)
    {
        // 蓄力：整只压紧、往起手方向后仰
        float p = 1f - Mathf.Max(0f, _timer) / Mathf.Max(0.01f, LungeChargeTime);
        if (_sprite != null)
        {
            float squash = 0.06f + 0.10f * p;
            _sprite.Scale = _baseScale * new Vector2(1f + squash, 1f - squash * 0.6f);
            _sprite.Rotation = -_lungeDir * 0.10f * p;
        }

        // 蓄力期间身体轻微抖动，抖得越来越快
        if (_sprite != null && Mathf.PosMod(_animT, 0.09f) < dt)
            _sprite.Position = new Vector2((GD.Randf() - 0.5f) * 6f * p, (GD.Randf() - 0.5f) * 4f * p);

        if (_timer <= 0f) StartLungeDash();
    }

    /// <summary>起跳：从这一侧边界沿弧线飞到对面边界（不是瞬移）。</summary>
    private void StartLungeDash()
    {
        _st = St.LungeDash;
        _lungeHitDone = false;
        _lungeT = 0f;

        if (_sprite != null) { _sprite.Position = Vector2.Zero; _sprite.Scale = _baseScale; _sprite.Rotation = 0f; }
        if (_lungeWarn != null && IsInstanceValid(_lungeWarn)) _lungeWarn.StopWarning();

        _lungeFromX = GlobalPosition.X;
        _lungeToX = _lungeDir > 0f ? MaxCenterX : MinCenterX;
        _lungeDur = Mathf.Max(0.2f, Mathf.Abs(_lungeToX - _lungeFromX) / Mathf.Max(1f, LungeSpeed));

        ApplyTexture(_lungeDir > 0f ? RightTexture : LeftTexture);
        AudioManager.Instance?.PlaySfx("beast_lunge", 1f, 0.9f);
        EmitSignal(SignalName.ShakeRequested, 7f, 0.2f);
    }

    /// <summary>
    /// 弧线飞扑：X 匀速、Y 走一个正弦拱，跟恐怖苹果的跳跃同一个思路。
    /// 起跳 / 腾空 / 落下都有过程，玩家读得出「它现在在哪、下一秒到哪」。
    /// 拱高 120 时，拱顶 Boss 底边还在地面上方 25px —— 站着的人照样会被扫到，
    /// 只有「在中心圈里蹲下」这条规则能免伤。
    /// </summary>
    private void UpdateLungeDash(float dt)
    {
        _lungeT += dt;
        float p = Mathf.Clamp(_lungeT / _lungeDur, 0f, 1f);

        float x = Mathf.Lerp(_lungeFromX, _lungeToX, p);
        float y = _groundCenterY - LungeArcHeight * Mathf.Sin(Mathf.Pi * p);
        GlobalPosition = new Vector2(x, y);

        // 前冲姿态：横向拉长 + 顺着弧线前倾
        if (_sprite != null)
        {
            _sprite.Scale = _baseScale * new Vector2(1.16f, 0.9f);
            _sprite.Rotation = -_lungeDir * Mathf.Cos(Mathf.Pi * p) * 0.18f;
        }

        CheckLungeHit();

        if (p < 1f) return;

        GlobalPosition = new Vector2(_lungeToX, _groundCenterY);
        if (_sprite != null) { _sprite.Scale = _baseScale; _sprite.Rotation = 0f; }
        EmitSignal(SignalName.ShakeRequested, 12f, 0.3f);
        AudioManager.Instance?.PlaySfx("beast_slam", 0.9f, 1.1f);
        _st = St.Idle;
        _timer = LungeRecover;
        Velocity = Vector2.Zero;
    }

    /// <summary>
    /// 飞扑命中：沿这一帧走过的**横向**轨迹扫，只看 X 有没有重叠。
    ///
    /// 为什么不再按身体矩形求交：
    ///   飞扑改成弧线以后，拱顶时 Boss 的脚离地 120px，而玩家只有 64 高——
    ///   纯几何判定会让玩家「从它肚子底下穿过去」，站着也不掉血，那是漏判。
    ///   这招的手感本来就是「整条道被一只巨型苹果扫了一遍」：
    ///   站着必中，唯一免伤方式是需求里写的那条——站在中心圈里按 C 蹲下。
    /// </summary>
    private void CheckLungeHit()
    {
        if (_lungeHitDone) return;
        if (_player == null || !IsInstanceValid(_player)) return;

        Rect2 pr = _player.BodyRect();

        // 站在上面的平台 / 墙头上不算被扫到
        if (pr.Position.Y > FloorY + 8f) return;

        float bossHalfW = _halfW * 1.7f * 0.5f;
        float lo = Mathf.Min(_prevPos.X, GlobalPosition.X) - bossHalfW;
        float hi = Mathf.Max(_prevPos.X, GlobalPosition.X) + bossHalfW;
        if (pr.End.X < lo || pr.Position.X > hi) return;

        _lungeHitDone = true;

        // 躲避规则：站在战场中心 20% 范围内 + 按 C 蹲下 → 完全躲开
        bool inZone = Mathf.Abs(_player.GlobalPosition.X - ZoneCenterX) <= ZoneHalfWidth;
        if (inZone && _player.IsCrouching)
        {
            // 躲开就是躲开了，不放音效——玩家看得到 Boss 从头顶飞过去
            return;
        }

        _player.TakeDamage(Damage, GlobalPosition);
        EmitSignal(SignalName.ShakeRequested, 10f, 0.25f);
    }
}
