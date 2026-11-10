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

    /// <summary>给外部（演出脚本）用的半身高，把地面坐标换算成玩家中心坐标。</summary>
    public float HalfHeight => GetHalfHeight();

    /// <summary>
    /// 瞬移之后让镜头立刻到位。
    /// 注意不能直接写 _cam.GlobalPosition = GlobalPosition —— Camera2D 是 box 的子节点，
    /// 还带着 offset 和「锁定高度」的补偿，硬怼会把那两样都抹掉。
    /// 正确做法是先按正常逻辑算好位置，再清掉平滑状态让它立刻贴上去。
    /// </summary>
    public void SnapCamera()
    {
        if (_cam == null) return;
        LockCameraHeight();
        _cam.ResetSmoothing();
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

    /// <summary>上下看：W 抬头、S 低头，镜头平滑升降。爬藤/爬绳时禁用（那时 W/S 是上下爬）。</summary>
    private void UpdateCameraLook(float dt)
    {
        float lookY = 0f;

        // 挂在绳子/藤蔓上时，W/S 已经被攀爬吃掉了。
        // 如果这里还跟着抬镜头，就会出现「往上爬、画面同时往上飘」的叠加运动，
        // 玩家会觉得画面在乱晃 —— 而且爬到顶时镜头会先撞到上限。
        if (!_climbing)
        {
            if (Input.IsActionPressed("look_up")) lookY -= 1f;
            if (Input.IsActionPressed("look_down")) lookY += 1f;
        }

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
                float accel = Acceleration;

                // 泥潭：速度和加速度一起压。只压速度的话，起步那一下还是窜得很快，
                // 感觉像「滑过泥面」而不是「被泥拖着」。
                if (InMud)
                {
                    GetMudModifiers(out float mudSpeed, out _, out float mudAccel);
                    speedMult *= mudSpeed;
                    accel *= mudAccel;
                }

                Velocity = new Vector2(Mathf.MoveToward(Velocity.X, dir * Speed * speedMult, accel * dt), Velocity.Y);
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
            float power = jumpVelocity;

            // 泥潭：跳不高。注意高度跟速度的平方成正比，
            // 所以 0.55 倍跳跃力只跳得起原来的三成高。
            if (InMud)
            {
                GetMudModifiers(out _, out float mudJump, out _);
                power *= mudJump;
            }

            Velocity = new Vector2(Velocity.X, -power);
            _jumpBufferTimer = 0;
            _coyoteTimer = 0;
            _jumpsLeft--;

            // 跳跃音效固定，不再按「跳得多高 / 在不在泥里」调音量和音高
            AudioManager.PlayJump();
        }
        if (Input.IsActionJustReleased("jump") && Velocity.Y < 0)
        {
            Velocity = new Vector2(Velocity.X, Velocity.Y * 0.45f);
        }
    }

    /// <summary>
    /// 临时调试用：把攀爬的内部状态吐成一行字符串。
    /// GDScript 探针读不到 C# 的私有字段（_climbing 之类），只能靠这个方法转一手。
    /// </summary>
    public string DebugClimb()
    {
        Rope r = CurrentRope;
        bool valid = r != null && IsInstanceValid(r) && r.PlayerInRange;
        return $"rope={(r == null ? "null" : r.Name)} valid={valid} climbing={_climbing}"
             + $" cd={_grabCooldown:F2} vehicle={_inVehicle} crouch={IsCrouching}"
             + $" alive={_state == State.Alive} floor={IsOnFloor()}"
             + $" 对话锁={(GameState.I != null && GameState.I.DialogueActive)}";
    }

    /// <summary>
    /// 绳索 / 藤蔓攀爬。W 往上爬、S 往下滑、都不按就挂在原地，跳跃键蹬开。
    ///
    /// 关键点：
    ///   · 必须先于 ApplyGravity / UpdateHorizontalMove / UpdateJump 调用，
    ///     因为这三个在攀爬时都会 return，Velocity 由这里独家决定。
    ///   · 横向用「按距离比例给速度」吸附到绳子中线，而不是直接改 GlobalPosition ——
    ///     直接改位置会绕过 MoveAndSlide 的碰撞检测，可能把玩家塞进墙里。
    ///   · 上爬慢、下滑快且会加速（UpSpeed / DownSpeed / DownAccel 由具体物件给），
    ///     所以这里的 vy 不能像以前那样上下共用一个 ClimbSpeed。
    /// </summary>
    private void UpdateClimb(float dt, bool inputLocked)
    {
        if (_grabCooldown > 0f) _grabCooldown = Mathf.Max(0f, _grabCooldown - dt);
        if (_grabIgnoreTimer > 0f) _grabIgnoreTimer = Mathf.Max(0f, _grabIgnoreTimer - dt);

        Rope rope = CurrentRope;
        bool ropeValid = rope != null && IsInstanceValid(rope) && rope.PlayerInRange;

        // 刚蹬开的那一根短时间内不许再抓，否则一按跳就被它自己吸回去，永远跳不走
        bool justLeft = ropeValid && rope == _grabIgnore && _grabIgnoreTimer > 0f;

        // 抓绳子的前提：手上有绳子、没锁输入、没坐车、没蹲着、不是刚蹬开的那根
        bool canGrab = ropeValid && !justLeft && !inputLocked && !_inVehicle && !IsCrouching
                       && _state == State.Alive;

        // 已经抓着但条件没了（走出判定区 / 开始对话 / 上了车）→ 松手
        if (_climbing && !canGrab) _climbing = false;

        // 起爬：按 W；或者这根藤支持「空中碰到就抓」（换藤全靠这条）。
        // 注意 AutoGrabInAir 只在地面之外生效，否则走路经过藤蔓会被一直吸住。
        bool wantGrab = Input.IsActionPressed("look_up")
                        || (ropeValid && rope.AutoGrabInAir && !IsOnFloor());

        if (!_climbing && canGrab && _grabCooldown <= 0f && wantGrab)
        {
            _climbing = true;
            _jumpsLeft = MaxJumps;   // 下绳之后还能正常跳
            _coyoteTimer = 0f;
            _jumpBufferTimer = 0f;
            _climbSlideSpeed = 0f;
        }

        if (!_climbing) return;

        // 蹬开：往「按的方向」跳；没按方向就往面朝的方向跳。
        // 必须给横向初速度 —— 挂在藤上时横向速度是被吸附清零的，
        // 只往上跳的话两根藤之间永远换不过去。
        if (Input.IsActionJustPressed("jump"))
        {
            _climbing = false;
            _grabIgnore = rope;
            _grabIgnoreTimer = 0.5f;
            _grabCooldown = 0.1f;
            _climbSlideSpeed = 0f;

            float dir = Input.GetAxis("move_left", "move_right");
            if (Mathf.Abs(dir) < 0.1f) dir = _facingRight ? 1f : -1f;
            float vxOff = rope.JumpOffSpeed > 0f ? dir * rope.JumpOffSpeed : Velocity.X;

            Velocity = new Vector2(vxOff, -jumpVelocity * rope.JumpOffUp);
            AudioManager.PlayJump();
            return;
        }

        // 竖直：W 上（慢）、S 下滑（越滑越快）、都不按就挂着
        bool holdingUp = Input.IsActionPressed("look_up");
        bool holdingDown = Input.IsActionPressed("look_down");

        float vy;
        if (holdingUp)
        {
            vy = -rope.UpSpeed;
            _climbSlideSpeed = 0f;
        }
        else if (holdingDown)
        {
            // 下滑是加速的：从 0 加到 DownSpeed，手感是「溜下去」而不是电梯
            _climbSlideSpeed = Mathf.Min(_climbSlideSpeed + rope.DownAccel * dt, rope.DownSpeed);
            vy = _climbSlideSpeed;
        }
        else
        {
            vy = 0f;
            _climbSlideSpeed = 0f;
        }

        // 横向吸附到绳子中线
        float dx = rope.RopeX - GlobalPosition.X;
        float vx = Mathf.Abs(dx) < 1f ? 0f
                 : Mathf.Clamp(dx * rope.SnapStrength, -rope.SnapSpeed, rope.SnapSpeed);

        Velocity = new Vector2(vx, vy);

        // 到顶。必须带 vy < 0 这个条件：玩家正好停在顶端时 Y 就等于 ClimbTopY，
        // 不加方向判断的话按 S 也走这个分支，速度被清零 —— 人就卡死在顶上再也下不来。
        if (vy < 0f && GlobalPosition.Y <= rope.ClimbTopY)
        {
            if (rope.TossAtTop)
            {
                // 绳索：松手 + 轻轻上抛，让玩家越过绳头落到传送点上。
                // 加冷却是因为松手后人还在判定区里，按住 W 会立刻又抓住，形成上下弹跳。
                _climbing = false;
                _grabCooldown = 0.5f;
                Velocity = new Vector2(0f, -jumpVelocity * 0.35f);
                return;
            }

            // 藤蔓：停在顶端。Velocity 归零就行，别去改 GlobalPosition ——
            // 直接写位置会绕过 MoveAndSlide，可能把脑袋塞进树冠的碰撞里。
            Velocity = new Vector2(vx, 0f);
            _climbSlideSpeed = 0f;
            return;
        }

        // 到底：往下滑到藤蔓/绳索末端就松手，人自然掉下去。
        // 注意只判「正在往下」（vy > 0）—— 站在藤底刚抓住的那一帧 Y 也在底部，
        // 不分方向的话会「抓住→立刻放开」死循环，表现就是怎么按 W 都爬不上去。
        if (vy > 0f && GlobalPosition.Y >= rope.ClimbBottomY - GetHalfHeight())
        {
            _climbing = false;
            Velocity = new Vector2(0f, 0f);
            _climbSlideSpeed = 0f;
            return;
        }

        // 已经站在地上、又没有按着 W 往上 → 松手，交还给正常移动。
        // 不松的话，抓着藤底站着时左右键会被攀爬接管（UpdateHorizontalMove 会 return），人走不动。
        if (IsOnFloor() && !holdingUp)
        {
            _climbing = false;
            Velocity = new Vector2(0f, 0f);
            _climbSlideSpeed = 0f;
        }
    }

    /// <summary>锁定镜头高度：把相机子节点的 local Y 反向补偿掉。</summary>
    private void LockCameraHeight()
    {
        // 场景里 box 的 _Ready 可能还没跑（比如 WellExit 排在 box 前面），此时 _cam 是 null。
        // 以前这里没判空，井口入场动画第一帧就抛 NullReference，直接把人锁死在井里。
        if (_cam == null) return;

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
            // 上限不能比下限还低。有些场景（CameraFixedY 用默认 420、offset 又带 -200 的，
            // 比如 underground）算出来 baseCenter = 220，比半屏高 225 还小 5 像素 ——
            // 这时 Mathf.Clamp 的 min > max，会直接抛 ArgumentException
            // （调试器里刷 "'225' cannot be greater than 225/220"），
            // 而且它排在 _PhysicsProcess 中段，一抛就把后面的切武器/瞄准/朝向/攻击/换帧全跳过。
            // 上下限撞车时以"不露出世界外"为先，把上限抬到下限。
            center = Mathf.Clamp(GlobalPosition.Y, halfH, Mathf.Max(halfH, baseCenter));
        }

        _cam.Position = new Vector2(_cam.Position.X,
                                    center - GlobalPosition.Y - _cam.Offset.Y);
    }
}
