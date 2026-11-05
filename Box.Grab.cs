using Godot;

/// <summary>Box 的「被投技抓住」状态。</summary>
/// <remarks>
/// 被不死海盗苹果的骷髅手抓住期间：
///   - 位置由怪物控制（不跑物理、不受重力）
///   - 所有输入（移动/跳跃/攻击/切换武器）失效
///   - 除投技本身外，不吃任何伤害（接触伤害、子弹、持续伤害全部免疫）
/// 投技伤害走 <see cref="TakeGrabDamage"/>，**不触发受击无敌**，
/// 否则投技结束后会有 1 秒无敌，落地手感很怪。
/// </remarks>
public partial class Box
{
    /// <summary>正在被抓住。为 true 时 _PhysicsProcess 会提前返回。</summary>
    public bool Grabbed { get; private set; }

    /// <summary>
    /// 抓人的那个怪在世界里的位置（怪每帧写进来）。
    /// 挣脱时要往「背对它」的方向弹开 —— 只往上弹的话人会落在它脚边，刚挣开就吃一发接触伤害。
    /// </summary>
    public Vector2 GrabSource { get; set; }

    // ── 狂按跳跃挣脱 ────────────────────────────────────────
    /// <summary>已经按了几下。</summary>
    public int StruggleCount { get; private set; }

    /// <summary>总共要按几下。</summary>
    public int StruggleNeeded => Mathf.Max(1, StrugglePresses);

    /// <summary>挣脱进度 0~1，给 SpacePrompt 画进度条用。</summary>
    public float StruggleProgress => Mathf.Clamp((float)StruggleCount / StruggleNeeded, 0f, 1f);

    /// <summary>
    /// 按一次跳跃。攒够 StrugglePresses 次就挣脱。
    /// 挣脱 = 立刻解除被抓、往上弹一下、给一小段无敌。
    /// 已经扣掉的血不还（投技是一滴一滴扣的，按得快就吃得少，这就是奖励）。
    /// </summary>
    private void TryStruggle()
    {
        if (!Grabbed) return;
        if (StrugglePresses <= 0) return;        // 设成 0 = 这个怪挣不脱

        StruggleCount++;
        if (StruggleCount < StruggleNeeded) return;

        Grabbed = false;
        SetGrabHurt(false);

        // 往背离怪的方向弹开，别原地起跳
        float dirX = GlobalPosition.X < GrabSource.X ? -1f : 1f;
        Velocity = new Vector2(dirX * StruggleKnockback, -StruggleJumpUp);
        _knockTimer = 0.08f;
        _hurtTimer = Mathf.Max(_hurtTimer, StruggleHurtGrace);
        AudioManager.PlayJump();
    }

    // 投技「持续受击」的红闪：只在被抓期间用，不碰 _hurtTimer（那会给无敌）。
    private bool _grabHurting;
    private bool _grabHurtSounded;                 // 这次被抓已经叫过了（叫声只放一次）
    private float _grabFlashTime;
    private const float GrabFlashPeriod = 0.09f;   // 红/白各 0.09 秒 → 约 5.5Hz

    /// <summary>开始 / 停止投技持续受击的红闪。</summary>
    public void SetGrabHurt(bool on)
    {
        _grabHurting = on;
        _grabFlashTime = 0f;
        if (!on) _sprite.Modulate = Colors.White;
    }

    /// <summary>被抓期间每帧调用：红白交替闪，表现「一直在挨打」。</summary>
    private void UpdateGrabFlash(float dt)
    {
        if (!_grabHurting) return;
        _grabFlashTime += dt;
        bool red = (int)(_grabFlashTime / GrabFlashPeriod) % 2 == 0;
        _sprite.Modulate = red ? new Color(1f, 0.42f, 0.42f) : Colors.White;
    }

    /// <summary>
    /// 身体中心（脚底往上 30 像素）。
    /// 瞄准、命中判定都要用它 —— 用 GlobalPosition 等于瞄脚底，
    /// 骷髅手会一路贴着地面飞，看起来像条地蛇。
    /// </summary>
    public Vector2 BodyCenter => GlobalPosition + new Vector2(0f, -30f);

    /// <summary>开始被抓：清掉所有进行中的动作状态。</summary>
    public void BeginGrab()
    {
        Grabbed = true;
        Velocity = Vector2.Zero;

        _grabHurting = false;
        _grabFlashTime = 0f;
        _grabHurtSounded = false;
        StruggleCount = 0;

        _knockTimer = 0f;
        _climbing = false;
        _charging = false;
        _attackTimer = 0f;          // 收掉正在挥的武器
        _attackCd = 0f;

        // 挥击判定框可能还开着，强制关掉，否则被抓期间还会打到怪
        if (_weapon != null) _weapon.Monitoring = false;
        if (_trail != null) _trail.Clear();
    }

    /// <summary>解除被抓（投技结束或怪物死亡）。</summary>
    public void EndGrab()
    {
        Grabbed = false;
        Velocity = Vector2.Zero;
        SetGrabHurt(false);
    }

    /// <summary>被抓期间由怪物每帧调用，把玩家钉在指定位置。</summary>
    public void SetGrabbedPosition(Vector2 pos)
    {
        GlobalPosition = pos;
        Velocity = Vector2.Zero;
    }

    /// <summary>
    /// 投技伤害：直接扣血，不走受击流程。
    /// 不设 _hurtTimer，所以不会产生受击无敌；不自己给击退，击退由调用方在投技结束时给。
    /// 投技是「一滴一滴」扣的，所以叫声只放**一次**，不然 10 滴就是 10 声连珠炮。
    /// </summary>
    public void TakeGrabDamage(int amount)
    {
        if (_state != State.Alive) return;
        if (amount <= 0) return;

        if (!_grabHurtSounded)
        {
            _grabHurtSounded = true;
            AudioManager.Instance?.PlaySfx("hurt", 0.9f);
        }

        Hp = Mathf.Max(0, Hp - amount);
        EmitSignal(SignalName.HpChanged, Hp, MaxHp);

        if (Hp <= 0) Die(GlobalPosition + Vector2.Down);
    }

    /// <summary>投技结束的击退：解除被抓并甩出去。</summary>
    public void ReleaseFromGrab(Vector2 from, float knockback, float up)
    {
        Grabbed = false;
        SetGrabHurt(false);          // 松手立刻恢复原色，别带着红色被甩出去

        float dirX = GlobalPosition.X < from.X ? -1f : 1f;
        Velocity = new Vector2(dirX * knockback, -up);
        _knockTimer = KnockTime;

        // 给一小段受击无敌，避免刚落地就被旁边的怪连打。
        // 这是投技「结束之后」的保护，不影响投技本身的判定。
        _hurtTimer = HurtCooldown * 0.6f;
    }
}
