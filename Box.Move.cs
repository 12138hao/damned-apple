using Godot;

/// <summary>Box 的移动与相机：左右移动、跳跃三件套、重力、单向平台、镜头升降与高度锁定。</summary>
/// <remarks>Box 的 partial 部分，字段仍声明在 Box.cs 里。</remarks>
public partial class Box
{
    /// <summary>
    /// 把「上下看」的基准高度归零并立刻对齐镜头。
    /// 传送 / 切场景后调用，避免镜头还停在旧地图的 offset 上导致视角偏移。
    /// </summary>
    public void ResetCameraLookY()
    {
        _camBaseY = 0f;
        if (_cam != null)
        {
            _cam.Offset = new Vector2(_cam.Offset.X, 0f);
            _cam.ResetSmoothing();
        }
    }

    /// <summary>碰撞体高度的一半，用来把"地面坐标"换算成"玩家中心坐标"。</summary>
    private float GetHalfHeight()
    {
        var shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (shape?.Shape is RectangleShape2D rect) return rect.Size.Y * 0.5f;
        if (shape?.Shape is CircleShape2D circle) return circle.Radius;
        return 16f;
    }

    /// <summary>判断脚底下踩的是不是单向平台（看它是否在 OneWayLayerBit 这一层）。</summary>
    private bool OnOneWayPlatform()
    {
        var col = GetLastSlideCollision();
        if (col == null) return false;

        var body = col.GetCollider() as CollisionObject2D;
        if (body == null) return false;

        return (body.CollisionLayer & OneWayLayerBit) != 0;
    }

    /// <summary>单向平台：站在上面按 S 时临时把平台层从碰撞掩码里摘掉。</summary>
    private void HandleDropThrough(float dt)
    {
        // 爬绳时按 S 是往下爬，不是穿平台
        if (_climbing) return;

        // 单向平台：站在上面按 S 就穿下去
        if (Input.IsActionJustPressed("look_down") && IsOnFloor() && OnOneWayPlatform())
        {
            _dropTimer = DropThroughTime;
        }

        if (_dropTimer > 0f)
        {
            _dropTimer -= dt;
            // 暂时不检测单向平台那一层，玩家就会掉下去
            CollisionMask = _baseMask & ~OneWayLayerBit;
        }
        else if (CollisionMask != _baseMask)
        {
            CollisionMask = _baseMask;   // 恢复
        }
    }

    /// <summary>不在地面时加重力。必须乘 dt，否则下落速度跟帧率挂钩。</summary>
    private void ApplyGravity(float dt)
    {
        if (_climbing) return;   // 挂在绳子上时不受重力，否则会往下滑

        if (!IsOnFloor())
        {
            // 蹲着往下掉时重力加倍，做出「快速下坠」
            float g = Gravity * (IsCrouching ? CrouchFallMultiplier : 1f);
            Velocity += new Vector2(0, g * dt);
        }
    }

    /// <summary>上下看：W 抬头、S 低头，镜头平滑升降。</summary>
    private void UpdateCameraLook(float dt)
    {
        // 上下看：镜头平滑升降，W 上移、S 下移
        float lookY = 0f;
        if (Input.IsActionPressed("look_up")) lookY -= 1f;
        if (Input.IsActionPressed("look_down")) lookY += 1f;
        _cam.Offset = new Vector2(
            _cam.Offset.X,
            Mathf.MoveToward(_cam.Offset.Y,
                             _camBaseY + lookY * LookDistance,
                             LookSpeed * dt));
    }

    /// <summary>左右移动：方向键加速到 Speed，松手用 Friction 刹车。</summary>
    private void UpdateHorizontalMove(float dt, bool inputLocked)
    {
        // 爬绳时横向由 UpdateClimb 接管（吸附到绳子中线），这里不能插手
        if (_climbing) return;

        // 蹲下期间不能走：原地刹车
        if (IsCrouching)
        {
            Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0f, Friction * dt), Velocity.Y);
            return;
        }

        if (_knockTimer <= 0f)
        {
            float dir = inputLocked ? 0f : Input.GetAxis("move_left", "move_right");
            if (dir != 0)
            {
                _facingRight = dir > 0f;          // 按方向键才改朝向
                float speedMult = _inVehicle ? VehicleSpeedMult : 1f;
                Velocity = new Vector2(Mathf.MoveToward(Velocity.X, dir * Speed * speedMult, Acceleration * dt), Velocity.Y);
            }
            else
            {
                Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0, Friction * dt), Velocity.Y);
            }
        }
    }

    /// <summary>跳跃：跳跃缓冲 + 土狼时间 + 松手切跳跃（短按跳得矮）。</summary>
    private void UpdateJump(float dt, bool inputLocked)
    {
        // 爬绳时的跳跃由 UpdateClimb 处理（蹬绳跳开），这里跳过，
        // 否则同一个跳跃键会被消费两次，_jumpsLeft 也会被扣错。
        if (_climbing) return;

        if (_inVehicle)
        {
            _jumpBufferTimer = 0f;   // 载具不能跳
        }
        else if (IsCrouching)
        {
            _jumpBufferTimer = 0f;   // 蹲下也不能跳
        }
        else if (!inputLocked && Input.IsActionJustPressed("jump"))
        {
            _jumpBufferTimer = JumpBufferTime;
        }
        if (_jumpBufferTimer > 0 && (_coyoteTimer > 0 || _jumpsLeft > 0))
        {
            Velocity = new Vector2(Velocity.X, -jumpVelocity);
            _jumpBufferTimer = 0;
            _coyoteTimer = 0;
            _jumpsLeft--;
            AudioManager.Instance?.PlaySfx("jump", 0.7f, 1f + GD.Randf() * 0.1f);
        }
        if (Input.IsActionJustReleased("jump") && Velocity.Y < 0)
        {
            Velocity = new Vector2(Velocity.X, Velocity.Y * 0.45f);
        }
    }

    /// <summary>
    /// 绳索攀爬。W 抓住往上爬、S 往下爬、松手就挂在原地，跳跃键蹬绳跳开。
    ///
    /// 关键点：
    ///   · 必须先于 ApplyGravity / UpdateHorizontalMove / UpdateJump 调用，
    ///     因为这三个在攀爬时都会 return，Velocity 由这里独家决定。
    ///   · 横向用「按距离比例给速度」吸附到绳子中线，而不是直接改 GlobalPosition ——
    ///     直接改位置会绕过 MoveAndSlide 的碰撞检测，可能把玩家塞进墙里。
    /// </summary>
    private void UpdateClimb(float dt, bool inputLocked)
    {
        if (_grabCooldown > 0f) _grabCooldown = Mathf.Max(0f, _grabCooldown - dt);

        Rope rope = CurrentRope;
        bool ropeValid = rope != null && IsInstanceValid(rope) && rope.PlayerInRange;

        // 抓绳子的前提：手上有绳子、没锁输入、没坐车、没蹲着
        bool canGrab = ropeValid && !inputLocked && !_inVehicle && !IsCrouching
                       && _state == State.Alive;

        // 已经抓着但条件没了（走出判定区 / 开始对话 / 上了车）→ 松手
        if (_climbing && !canGrab) _climbing = false;

        // 起爬：在绳子范围内按住 W
        if (!_climbing && canGrab && _grabCooldown <= 0f
            && Input.IsActionPressed("look_up"))
        {
            _climbing = true;
            _jumpsLeft = MaxJumps;   // 下绳之后还能正常跳
            _coyoteTimer = 0f;
            _jumpBufferTimer = 0f;
        }

        if (!_climbing) return;

        // 蹬绳跳开：跳跃键直接脱离绳子
        if (Input.IsActionJustPressed("jump"))
        {
            _climbing = false;
            _grabCooldown = 0.35f;
            Velocity = new Vector2(Velocity.X, -jumpVelocity * 0.75f);
            AudioManager.Instance?.PlaySfx("jump", 0.6f, 1.1f);
            return;
        }

        // 竖直：W 上、S 下、都不按就挂在原地
        float vy = 0f;
        if (Input.IsActionPressed("look_up")) vy = -rope.ClimbSpeed;
        else if (Input.IsActionPressed("look_down")) vy = rope.ClimbSpeed;

        // 横向吸附到绳子中线
        float dx = rope.RopeX - GlobalPosition.X;
        float vx = Mathf.Abs(dx) < 1f ? 0f
                 : Mathf.Clamp(dx * rope.SnapStrength, -rope.SnapSpeed, rope.SnapSpeed);

        Velocity = new Vector2(vx, vy);

        // 到顶：松手 + 轻轻上抛，让玩家越过绳头落到传送点上。
        // 加冷却是因为松手后人还在判定区里，按住 W 会立刻又抓住，形成上下弹跳。
        if (GlobalPosition.Y <= rope.TopY)
        {
            _climbing = false;
            _grabCooldown = 0.5f;
            Velocity = new Vector2(0f, -jumpVelocity * 0.35f);
            return;
        }

        // 到底：已经踩在地面上了就松手，交还给正常移动
        if (IsOnFloor() && GlobalPosition.Y >= rope.BottomY - GetHalfHeight() - 4f)
        {
            _climbing = false;
            Velocity = new Vector2(0f, 0f);
        }
    }

    /// <summary>锁定镜头高度：把相机子节点的 local Y 反向补偿掉。</summary>
    private void LockCameraHeight()
    {
        // 锁定镜头 Y：Camera2D 是 box 的子节点，把 local Y 反向补偿掉，
        // 世界 Y 就固定了，X 仍然跟着玩家走。
        //
        // 攀爬要特殊处理：这根绳子 500 像素高，镜头死锁在 CameraFixedY 的话
        // 玩家爬到一半就出屏幕；但完全放开又会升到世界顶部以上，露出 y<0 的空白。
        // 所以攀爬时让镜头跟着人走，同时卡住「可视区域顶边不高于世界 y=0」。
        if (!LockCameraY) return;

        // 相机世界中心 = 玩家Y + 本地Position.Y + Offset.Y
        float baseCenter = CameraFixedY + _cam.Offset.Y;
        float center = baseCenter;
        if (_climbing)
        {
            // 半屏高 = 镜头中心能到的最小值（再往上就露出世界外）
            float halfH = GetViewport().GetVisibleRect().Size.Y * 0.5f;
            center = Mathf.Clamp(GlobalPosition.Y, halfH, baseCenter);
        }

        _cam.Position = new Vector2(_cam.Position.X,
                                    center - GlobalPosition.Y - _cam.Offset.Y);
    }
}
