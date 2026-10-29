using Godot;

/// <summary>Box 的生命：受击、死亡、复活、存档点、对话回应。</summary>
/// <remarks>Box 的 partial 部分，字段仍声明在 Box.cs 里。</remarks>
public partial class Box
{
    /// <summary>持续伤害（毒圈、岩浆）：只扣血，不击退、不触发受击无敌。</summary>
    public void TakeDotDamage(int amount)
    {
        if (_state != State.Alive) return;
        if (amount <= 0) return;

        Hp = Mathf.Max(0, Hp - amount);
        EmitSignal(SignalName.HpChanged, Hp, MaxHp);

        if (Hp <= 0) Die(GlobalPosition + Vector2.Down);
    }

    public void TakeDamage(int amount, Vector2 from)
    {
        if (_state != State.Alive) return;      // 死亡 / 复活期间完全免疫
        if (_hurtTimer > 0f) return;

        AudioManager.Instance?.PlaySfx("hurt", 0.9f);
        Hp = Mathf.Max(0, Hp - amount);
        EmitSignal(SignalName.HpChanged, Hp, MaxHp);

        if (Hp <= 0)
        {
            Die(from);
            return;
        }

        float dirX = GlobalPosition.X < from.X ? -1f : 1f;
        // 蹲着底盘低，击退和击飞都打折
        float kbScale = IsCrouching ? CrouchKnockbackScale : 1f;
        Velocity = new Vector2(dirX * KnockBack * kbScale, -KnockUp * kbScale);
        _hurtTimer = HurtCooldown;
        _knockTimer = KnockTime;
    }

    private void Respawn()
    {
        GlobalPosition = _spawnPoint;
        Velocity = Vector2.Zero;
        Hp = MaxHp;
        // 复活后给一段无敌，挡住「上一帧还在怪物身边」的过期重叠检测
        _hurtTimer = HurtCooldown;
        _knockTimer = 0f;
        EmitSignal(SignalName.HpChanged, Hp, MaxHp);
    }

    private void Die()
    {
        Die(GlobalPosition);
    }

    /// <summary>
    /// 敌人"真正重新接触"玩家时调用：清掉受击免疫，让这次接触立刻生效。
    /// 只在敌人确认离开过（超过 TouchGraceTime）之后才会被调用，
    /// 所以贴着敌人时免疫仍然正常，不会变成"每帧掉血"。
    /// </summary>
    public void ResetHurtCooldown()
    {
        _hurtTimer = 0f;
    }

    /// <summary>把血回满。已经满了就什么也不做，免得刷一堆 HpChanged 信号。</summary>
    public void HealFull()
    {
        if (Hp >= MaxHp) return;
        Hp = MaxHp;
        EmitSignal(SignalName.HpChanged, Hp, MaxHp);
    }

    /// 存档点调用：把死亡后的复活位置改到这里。只在当前场景有效。
    /// 传进来的是存档点所在地面，这里往上抬半个身高，免得复活时半截陷进地板。
    /// </summary>
    public void SetRevivePoint(Vector2 groundPos)
    {
        _spawnPoint = groundPos + new Vector2(0f, -GetHalfHeight());
        GD.Print($"[Revive] 复活点已设为 {_spawnPoint}（地面 {groundPos}）");
    }

    /// <summary>存档时调用：往上跳一下 + 冒个气泡说话。</summary>
    public void DoSaveCheer(string text = "存档成功！")
    {
        if (_state != State.Alive) return;

        // 载具状态不跳：载具本来就不能跳，存档时把车弹起来很怪
        if (!_inVehicle)
            Velocity = new Vector2(Velocity.X, -SaveJumpSpeed);

        var bubble = GetNodeOrNull<DialogueUI>("DialogueUI");
        if (bubble == null) return;
        bubble.ShowLine(text);
        _ = HideBubbleAfter(bubble, 1.8f);
    }

    private async System.Threading.Tasks.Task HideBubbleAfter(DialogueUI bubble, float sec)
    {
        await ToSignal(GetTree().CreateTimer(sec), SceneTreeTimer.SignalName.Timeout);
        if (IsInstanceValid(bubble)) bubble.HideBubble();
    }

    /// <summary>外部（如深渊/尖刺碰撞区）调用：立刻死亡并走完整死亡+复活流程。</summary>
    public void Kill()
    {
        if (_state != State.Alive) return;   // 已经在死/复活中就不重复触发
        Die(GlobalPosition);
    }

    private void Die(Vector2 from)
    {
        AudioManager.Instance?.PlaySfx("death", 1f, 1f + GD.Randf() * 0.05f);
        // 载具状态下死亡：先下车恢复人物贴图。
        // 死亡动画用帧 8/9，而载具贴图只有 8 帧（0~7），不换回来会越界报错。
        if (_inVehicle)
        {
            _inVehicle = false;
            ApplyVehicleState();
        }

        _state = State.Dying;
        _stateTimer = DeathTime;
        _hurtTimer = 0f;
        _knockTimer = 0f;
        Hp = 0;                                  // 掉崖自杀时血条也要归零
        EmitSignal(SignalName.HpChanged, Hp, MaxHp);

        // 被击杀的瞬间同样吃一次击退：被打飞出去，再摔在地上
        float dirX = 0f;
        if (!from.IsEqualApprox(GlobalPosition))
        {
            dirX = GlobalPosition.X < from.X ? -1f : 1f;
        }
        Velocity = new Vector2(dirX * KnockBack, -KnockUp);

        _sprite.Modulate = Colors.White;
        _sprite.Frame = 8;                       // 帧 8：平躺
        EmitSignal(SignalName.Died);
    }

    private void StartRevive()
    {
        // 存档点如果不在当前场景，先切回那个场景，到那边再复活
        var gs = GameState.I;
        if (gs != null && !string.IsNullOrEmpty(gs.ReviveScenePath))
        {
            string cur = GetTree().CurrentScene?.SceneFilePath ?? "";
            if (gs.ReviveScenePath != cur)
            {
                // 用存档点的地面位置换算成玩家中心位置（不能传 _spawnPoint，
                // 那是"当前场景"的复活点，在别的场景里是错的）
                Vector2 spawnPos = gs.RevivePosition + new Vector2(0f, -GetHalfHeight());
                // 把复活无敌一起带过去，否则新场景一出生就可能被打死
                gs.PrepareTravel(MaxHp, spawnPos, Weapons, _currentSlot,
                                 ReviveTime + HurtCooldown);
                GetTree().ChangeSceneToFile(gs.ReviveScenePath);
                return;
            }
        }

        _state = State.Reviving;
        _stateTimer = ReviveTime;
        GlobalPosition = _spawnPoint;
        Velocity = Vector2.Zero;

        // 复活是瞬移，相机开了 position_smoothing 会慢慢飘过去，
        // 结果复活动画发生在镜头外面。这里直接把它怼到玩家身上。
        if (_cam != null)
        {
            _cam.GlobalPosition = GlobalPosition;   // 立刻对齐
            _cam.ResetSmoothing();                  // 顺便清掉平滑状态
        }
        Hp = MaxHp;
        _hurtTimer = ReviveTime + HurtCooldown;  // 复活全程无敌，结束后再留一段
        _knockTimer = 0f;
        _sprite.Visible = true;
        _sprite.Modulate = Colors.White;
        _sprite.Frame = 9;                       // 帧 9：天使形态
        EmitSignal(SignalName.HpChanged, Hp, MaxHp);
        EmitSignal(SignalName.Revived);
    }

    private void FinishRevive()
    {
        _state = State.Alive;
        _hurtTimer = 0f;             // 复活动画结束就清掉无敌，否则会接着闪红 1 秒
        _sprite.Visible = true;
        _sprite.Modulate = Colors.White;
        _sprite.Frame = 0;
    }

    /// <summary>死亡 / 复活期间每物理帧调用一次，不处理输入、重力和动画。</summary>
    private void UpdateSpecialState(float dt)
    {
        _stateTimer -= dt;

        if (_state == State.Dying)
        {
            // 死亡期间不再响应输入，但重力和击退照常生效：被打飞出去再摔在地上
            if (!IsOnFloor())
            {
                Velocity += new Vector2(0f, Gravity * dt);
            }
            // 水平速度慢慢衰减：空中阻力小飞得远，落地摩擦大很快停住
            float drag = IsOnFloor() ? 900f : 260f;
            Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0f, drag * dt), Velocity.Y);
            MoveAndSlide();

            if (_stateTimer <= 0f) StartRevive();
            return;
        }

        // Reviving：站定在出生点，不需要移动
        Velocity = Vector2.Zero;

        // 复活：前 ReviveWingTime 秒稳定显示天使形态，之后在天使和普通之间闪烁
        if (_stateTimer > ReviveTime - ReviveWingTime)
        {
            _sprite.Frame = 9;
        }
        else if (_stateTimer > 0f)
        {
            _sprite.Frame = ((int)(_stateTimer * 12f) % 2 == 0) ? 9 : 0;
        }
        else
        {
            FinishRevive();
        }
    }

    /// <summary>对话中说话时的回应动作：轻轻跳一下。jumpSpeed <= 0 或不在空中时不做事。</summary>
    public void SpeakReaction(float jumpSpeed)
    {
        if (jumpSpeed <= 0f) return;
        if (!IsOnFloor()) return;              // 空中就不跳了，避免打乱玩家操作

        Velocity = new Vector2(Velocity.X, -jumpSpeed);
    }
}
