using Godot;

/// <summary>
/// 循环播放精灵表里的一段帧（呼吸、待机、水面波纹之类）。
/// 挂到 Sprite2D 上，配合 Hframes 使用。
/// </summary>
public partial class SheetAnimator : Sprite2D
{
    [Export] public int StartFrame = 0;      // 从第几帧开始
    [Export] public int FrameCount = 4;      // 循环几帧
    [Export] public float Interval = 0.18f;  // 每帧停留多久（秒）
    [Export] public bool PingPong = false;   // true = 0→1→2→1→0 往复，false = 0→1→2→3→0
    [Export] public bool Paused = false;     // 暂停循环（对话换表情时用）

    private float _t;
    private int _i;
    private int _dir = 1;

    public override void _Ready()
    {
        Frame = StartFrame;
    }

    public override void _Process(double delta)
    {
        if (Paused) return;
        if (FrameCount <= 1) return;

        _t += (float)delta;
        if (_t < Interval) return;
        _t -= Interval;

        if (PingPong)
        {
            _i += _dir;
            if (_i >= FrameCount - 1) { _i = FrameCount - 1; _dir = -1; }
            else if (_i <= 0) { _i = 0; _dir = 1; }
        }
        else
        {
            _i = (_i + 1) % FrameCount;
        }

        Frame = StartFrame + _i;
    }
}
