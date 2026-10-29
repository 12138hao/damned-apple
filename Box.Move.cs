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

    /// <summary>锁定镜头高度：把相机子节点的 local Y 反向补偿掉。</summary>
    private void LockCameraHeight()
    {
        // 锁定镜头 Y：Camera2D 是 box 的子节点，把 local Y 反向补偿掉，
        // 世界 Y 就固定了，X 仍然跟着玩家走。
        if (LockCameraY)
        {
            _cam.Position = new Vector2(_cam.Position.X, CameraFixedY - GlobalPosition.Y);
        }
    }
}
