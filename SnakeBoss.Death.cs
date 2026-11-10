using Godot;

/// <summary>
/// SnakeBoss 的死亡演出：活体贴图交叉淡出、尸体贴图（脖子瘫软、嘴大张）淡入。
/// 换图那一瞬配合闪白 + 震屏，遮住"头从上面掉到下面"的位移。
/// </summary>
public partial class SnakeBoss
{
    private float _deathT;

    protected override void OnDeath()
    {
        State = SState.Dead;
        _deathT = 0f;
        if (_tailArt != null) _tailArt.Visible = false;
        if (_deadArt != null)
        {
            _deadArt.Visible = true;
            _deadArt.Modulate = new Color(1f, 1f, 1f, 0f);
        }
        // 死亡瞬间闪白
        if (_art != null) _art.Modulate = new Color(1.9f, 1.9f, 1.9f, 1f);

        // 死亡音由 Enemy.Die() 统一播一次（DeathSfx）。这里不再叠一层，
        // 否则 11 秒的死亡音会被拉成 25 秒，还和本体那份互相打架。
        _arena?.Shake(16f, 0.55f);
        _arena?.OnBossDefeated();
        GD.Print("[SnakeBoss] 被击败");
        QueueRedraw();
    }

    /// <summary>0.3 秒交叉淡化到尸体，之后轻微下沉稳住。</summary>
    private void TickDeath(float dt)
    {
        if (_deathT > 2.0f) return;
        _deathT += dt;

        float k = Mathf.Clamp(_deathT / 0.30f, 0f, 1f);
        float e = k * k * (3f - 2f * k);              // smoothstep
        float flash = Mathf.Clamp(1f - _deathT / 0.10f, 0f, 1f);

        if (_art != null)
        {
            _art.Modulate = new Color(
                1f + 0.9f * flash,
                1f + 0.9f * flash,
                1f + 0.9f * flash,
                1f - e);
        }
        if (_deadArt != null)
        {
            _deadArt.Modulate = new Color(1f, 1f, 1f, e);
            _deadArt.Position = new Vector2(0f, 5f * e);
        }
        QueueRedraw();
    }
}
