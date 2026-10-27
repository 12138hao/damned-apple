using Godot;

/// <summary>
/// 单帧贴图的呼吸动画：靠纵向缩放 + 横向补偿做出「吸气鼓起、呼气回落」的效果。
/// 直接挂到 Sprite2D 上即可，不需要多帧贴图。
/// </summary>
public partial class Breath : Sprite2D
{
    [Export] public float Amplitude = 0.025f;   // 呼吸幅度（0.025 = 2.5%）
    [Export] public float Speed = 1.8f;         // 呼吸速度（越大越快）
    [Export] public bool KeepFeetOnGround = true; // 缩放时保持脚底不上下浮动

    private float _t;
    private float _baseOffsetY;
    private Vector2 _baseScale;

    public override void _Ready()
    {
        _baseOffsetY = Offset.Y;
        _baseScale = Scale;      // 记住场景里设的缩放，呼吸在它基础上叠加
    }

    public override void _Process(double delta)
    {
        _t += (float)delta * Speed;

        // sin 从 -1 到 1，映射成 1±Amplitude 的缩放
        float s = 1f + Mathf.Sin(_t) * Amplitude;

        // 纵向拉伸时横向略微压扁，看起来才有「体积守恒」的呼吸感
        // 在场景设定的基础缩放上叠加，而不是覆盖它
        Scale = new Vector2(_baseScale.X / Mathf.Sqrt(s), _baseScale.Y * s);

        if (KeepFeetOnGround)
        {
            // 精灵以中心为基准缩放，纵向变大时脚底会往下沉，
            // 这里用 Offset 把整体往上提回一点，让脚底保持贴地
            float grown = (s - 1f) * (Texture != null ? Texture.GetHeight() / Hframes : 0f) * 0.5f;
            Offset = new Vector2(Offset.X, _baseOffsetY - grown);
        }
    }
}
