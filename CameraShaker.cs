using Godot;

/// <summary>
/// 相机震屏组件。纯逻辑类，不用挂节点，由 Arena 持有并每帧喂 dt。
///
/// 两个 Arena 原来各写了一份 UpdateShake，逻辑几乎一样，
/// 唯一的区别是「要不要抖 Y」——现在收成一个 ShakeY 开关。
///
/// 关键细节（踩过坑）：每帧必须「赋值」而不是在上一帧基础上累加。
/// 因为 Box 每帧只重设相机 Offset.Y，Offset.X 没人管，累加会留下残差 → 镜头永久偏一格。
/// </summary>
public class CameraShaker
{
    /// <summary>要震的相机。开战时由 Arena 设置。</summary>
    public Camera2D Cam;

    /// <summary>是否连纵向一起抖。横版游戏纵向抖一点更有重量感，默认开。</summary>
    public bool ShakeY = true;

    private float _time;
    private float _strength;

    /// <summary>请求震屏。同时有多个请求时取最大值，不会叠加成地震。</summary>
    public void Shake(float strength, float duration)
    {
        _strength = Mathf.Max(_strength, strength);
        _time = Mathf.Max(_time, duration);
    }

    public void Update(float dt)
    {
        if (Cam == null || !GodotObject.IsInstanceValid(Cam)) return;

        if (_time > 0f)
        {
            _time -= dt;

            float sx = (GD.Randf() - 0.5f) * _strength * 2f;
            float sy = ShakeY ? (GD.Randf() - 0.5f) * _strength : 0f;
            Cam.Offset = new Vector2(sx, Cam.Offset.Y + sy);

            if (_time <= 0f)
            {
                _strength = 0f;
                Cam.Offset = new Vector2(0f, Cam.Offset.Y);
            }
        }
        else if (Cam.Offset.X != 0f)
        {
            Cam.Offset = new Vector2(0f, Cam.Offset.Y);
        }
    }

    /// <summary>立刻停下并清掉横向偏移（换场景 / 收镜头时用）。</summary>
    public void Reset()
    {
        _time = 0f;
        _strength = 0f;
        if (Cam != null && GodotObject.IsInstanceValid(Cam))
            Cam.Offset = new Vector2(0f, Cam.Offset.Y);
    }
}
