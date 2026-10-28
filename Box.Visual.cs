using Godot;

/// <summary>Box 的视觉：贴图朝向与动画选帧。</summary>
/// <remarks>Box 的 partial 部分，字段仍声明在 Box.cs 里。</remarks>
public partial class Box
{
    /// <summary>贴图朝向与动画选帧（出拳 > 蓄力 > 挥击 > 上下看 > 空中 > 跑 > 站）。</summary>
    private void UpdateVisual(float dt)
    {
        // 贴图朝向：每帧无条件更新，不能只放在跑动分支里
        // （否则站着、蓄力、空中时转向不会翻面）
        // 载具的贴图分左右两套（0~3 朝右、4~7 朝左），不需要再镜像
        _sprite.FlipH = _inVehicle ? false : !_facingRight;

        // 载具：0~3 帧向右走，4~7 帧向左走（贴图不足 8 帧就只显示第 0 帧）
        if (_inVehicle)
        {
            if (_sprite.Hframes >= 8)
            {
                _animTimer += dt;
                if (_animTimer >= 0.1f)
                {
                    _animTimer = 0f;
                    int start = _facingRight ? 0 : 4;
                    int f = _sprite.Frame;
                    if (f < start || f >= start + 4) f = start;
                    else f = start + (f - start + 1) % 4;
                    _sprite.Frame = f;
                }
            }
            else
            {
                _sprite.Frame = 0;
                _animTimer = 0f;
            }
        }
        // 动画：出拳 > 蓄力 > 挥击 > 上下看 > 空中 > 跑步 > 站立
        else if (_punchTimer > 0f)
        {
            // 出拳帧 16~19 自带左右（16/17 朝左，18/19 朝右），
            // 所以这里必须关掉镜像，否则会「左拳被镜像成右拳」。
            _sprite.FlipH = false;
            _sprite.Frame = _punchFrame;   // 这一拳固定用一帧，下次点击换另一帧
            _animTimer = 0f;
        }
        else if (_charging)
        {
            _sprite.Frame = 12;                      // 帧 12：举棒蓄力
        }
        else if (_attackTimer > 0f)
        {
            _sprite.Frame = 13;                      // 帧 13：挥出
        }
        else if (Input.IsActionPressed("look_up"))
        {
            _sprite.Frame = 14;                      // 帧 14：向上看
        }
        else if (Input.IsActionPressed("look_down"))
        {
            _sprite.Frame = 15;                      // 帧 15：向下看
        }
        else if (_holding && IsOnFloor() && Mathf.Abs(Velocity.X) < 10f)
        {
            // 帧 11 里画的那根棒子太细，看着像没有。
            // 改成用空手呼吸帧 + 独立武器贴图，球棒才看得清。
            _animTimer = 0f;
            _breathTimer += dt;
            if (_breathTimer >= BreathInterval)
            {
                _breathTimer = 0f;
                _breathFrame = 1 - _breathFrame;
            }
            _sprite.Frame = _breathFrame;
        }
        else if (!IsOnFloor())
        {
            _sprite.Frame = Velocity.Y < 0f ? 6 : 7;      // 上升 6 / 下落 7
        }
        else if (Mathf.Abs(Velocity.X) > 10f)
        {
            _animTimer += dt;
            if (_animTimer >= 0.1f)
            {
                _animTimer = 0f;
                _runFrame = (_runFrame + 1) % 4;          // 0→1→2→3→0
                _sprite.Frame = 2 + _runFrame;
            }
        }
        else
        {
            // 站立呼吸：帧 0 ↔ 帧 1 缓慢交替
            _animTimer = 0f;
            _breathTimer += dt;
            if (_breathTimer >= BreathInterval)
            {
                _breathTimer = 0f;
                _breathFrame = 1 - _breathFrame;
            }
            _sprite.Frame = _breathFrame;
        }
    }
}
