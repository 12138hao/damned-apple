using Godot;

/// <summary>Box 的载具：上下车、状态切换、子弹发射。</summary>
/// <remarks>Box 的 partial 部分，字段仍声明在 Box.cs 里。</remarks>
public partial class Box
{
    /// <summary>捡到载具（由 VehiclePickup 调用）。之后按 R 乘坐。</summary>
    public void GiveVehicle(Texture2D sheet, int hframes, float rideScale = 0.7f)
    {
        _hasVehicle = true;
        _vehicleSheet = sheet;
        _vehicleHframes = Mathf.Max(1, hframes);
        _vehicleSpriteScale = Mathf.Max(0.05f, rideScale);

        // 记到 GameState，换场景回来还能用
        if (GameState.I != null)
        {
            GameState.I.HasVehicle = true;
            GameState.I.VehicleSheetPath = sheet?.ResourcePath;
            GameState.I.VehicleHframes = _vehicleHframes;
            GameState.I.VehicleScale = _vehicleSpriteScale;
        }

        GD.Print("[Vehicle] 拿到载具，按 R 乘坐");
    }

    /// <summary>R 键：上车 / 下车。</summary>
    private void ToggleVehicle()
    {
        if (!_hasVehicle) return;

        _inVehicle = !_inVehicle;
        ApplyVehicleState();
    }

    private void ApplyVehicleState()
    {
        if (_inVehicle)
        {
            // 上车：收起武器、清掉蓄力
            _holding = false;
            _charging = false;
            _chargeArc?.SetProgress(0f);
            if (_weaponSprite != null) _weaponSprite.Visible = false;
            _trail?.Clear();

            if (_vehicleSheet != null)
            {
                _sprite.Texture = _vehicleSheet;
                _sprite.Hframes = _vehicleHframes;
                _sprite.Frame = 0;
                _sprite.Scale = Vector2.One * _vehicleSpriteScale;

                // 让车轮底和人物脚底落在同一条水平线上（以节点位置为基准）：
                //   人物：贴图 64 高，中心 32，脚底 y=57 → 相对节点 +25
                //   载具：贴图 128 高，中心 64，车轮底 y=121 → 相对中心 +57
                //   载具缩放后要落在 +25 → Offset = 25 / scale - 57
                // 这样改 RideScale 也不用重新调偏移，自动对齐。
                const float PlayerFootOffset = 25f;
                const float VehicleFootFromCenter = 57f;
                float alignY = PlayerFootOffset / Mathf.Max(0.05f, _vehicleSpriteScale)
                               - VehicleFootFromCenter;
                _sprite.Offset = new Vector2(0f, alignY + VehicleFootTweakY);

                // 碰撞体放大到接近载具视觉尺寸，别让车"碾过去"却不掉血
                if (_bodyShape != null)
                {
                    _bodyShape.Shape = new RectangleShape2D
                    {
                        Size = new Vector2(VehicleBodyWidth, VehicleBodyHeight)
                    };
                }
            }
            GD.Print("[Vehicle] 上车");
        }
        else
        {
            // 下车：恢复原贴图
            if (_baseTexture != null)
            {
                _sprite.Texture = _baseTexture;
                _sprite.Hframes = _baseHframes;
                _sprite.Frame = 0;
                _sprite.Offset = _baseSpriteOffset;   // 恢复原始偏移，不是 0
                _sprite.Scale = _baseSpriteScale;
                if (_bodyShape != null && _baseBodyShape != null)
                    _bodyShape.Shape = _baseBodyShape;
            }
            // 下车瞬间让朝向跟鼠标，避免沿用载具状态下的残留值
            _facingRight = _aimDir.X >= 0f;
            GD.Print("[Vehicle] 下车");
        }
    }

    /// <summary>
    /// 载具状态下的左键：从炮管水平发射一发子弹。
    /// 鼠标在左边就从左炮口往左打，在右边就从右炮口往右打（不做 360 度瞄准）。
    /// </summary>
    private void UpdateVehicleAttack(float dt)
    {
        if (!Input.IsActionJustPressed("attack")) return;

        AudioManager.Instance?.PlaySfx("vehicle_shoot", 0.8f, 1f + GD.Randf() * 0.06f);

        bool toRight = _aimDir.X >= 0f;
        Vector2 dir = toRight ? Vector2.Right : Vector2.Left;

        // 炮口跟着贴图走：贴图中心在 Offset，炮口相对中心 (iconX, iconY)，整体再乘缩放
        float iconCenterY = _sprite.Offset.Y;
        float muzzleX = (toRight ? VehicleMuzzleIconX : -VehicleMuzzleIconX) * _vehicleSpriteScale;
        float muzzleY = (iconCenterY + VehicleMuzzleIconY) * _vehicleSpriteScale;

        // 子弹加到场景根，不然会跟着玩家一起动
        Node root = GetTree().CurrentScene ?? GetTree().Root;

        int count = Mathf.Max(1, VehicleBulletPerShot);
        for (int i = 0; i < count; i++)
        {
            // 多发时纵向均匀错开，方向保持水平
            float offsetY = (i - (count - 1) * 0.5f) * VehicleBulletSpreadY;

            var bullet = new Bullet
            {
                Damage = VehicleBulletDamage,
                Speed = VehicleBulletSpeed,
                LifeFrames = VehicleBulletLifeFrames,
                Knockback = VehicleBulletKnockback,
            };

            root.AddChild(bullet);
            bullet.GlobalPosition = GlobalPosition + new Vector2(muzzleX, muzzleY + offsetY);
            bullet.Launch(dir);
        }
    }
}
