using Godot;
using System.Collections.Generic;

/// <summary>
/// 全局音频管理器（Autoload 单例）。
///
/// 用法：
///   AudioManager.PlaySfx("jump");          // 播音效
///   AudioManager.PlayBgm("boss_fight");    // 切背景音乐
///   AudioManager.StopBgm();                // 停 BGM
///
/// 音频文件放在 res://assets/audio/ 下，命名对应：
///   assets/audio/sfx/jump.wav
///   assets/audio/bgm/boss_fight.ogg
///
/// **文件不存在时静默跳过**，不会报错——所以可以先把系统搭好，之后再丢文件。
/// </summary>
public partial class AudioManager : Node
{
    public static AudioManager Instance { get; private set; }

    [Export] public float BgmVolume = 0.55f;      // 0~1
    [Export] public float SfxVolume = 0.85f;      // 0~1
    [Export] public int SfxVoices = 8;            // 音效池大小（同时能播几个）

    /// <summary>
    /// 每个音效的独立音量倍率。key = 音效名（必须和 PlaySfx 里写的字符串完全一致），
    /// value = 相对倍率：1 = 原样，0.5 = 减半，2 = 加倍。
    /// 表里没列出的名字按 1 处理，所以只填你想改的那几个就行。
    ///
    /// ⚠ 这个字段只有在 AudioManager.tscn 里才改得动——脚本直接当 autoload 时，
    ///   导出字段不会出现在检查器里。
    /// </summary>
    [Export] public Godot.Collections.Dictionary<string, float> SfxVolumeScale { get; set; } = new()
    {
        { "jump",          1f },
        { "hurt",          1f },
        { "attack",        1f },
        { "swing",         1f },
        { "charge",        1f },
        { "vehicle_shoot", 1f },
        { "death",         1f },
        { "enemy_hurt",    1f },
        { "pickup",        1f },
        { "collapse",      1f },
        { "slam",          1f },
        { "boss_cry",      1f },
        { "blip",          1f },
    };

    private const string SfxDir = "res://assets/audio/sfx/";
    private const string BgmDir = "res://assets/audio/bgm/";

    private AudioStreamPlayer _bgm;
    private readonly List<AudioStreamPlayer> _sfxPool = new();
    private int _sfxCursor;
    private string _currentBgm = "";
    private Tween _bgmTween;

    public override void _Ready()
    {
        Instance = this;

        // BGM 播放器
        _bgm = new AudioStreamPlayer { Bus = "Master" };
        AddChild(_bgm);

        // 音效池：多个播放器轮换，避免连续播放互相打断
        for (int i = 0; i < SfxVoices; i++)
        {
            var p = new AudioStreamPlayer { Bus = "Master" };
            AddChild(p);
            _sfxPool.Add(p);
        }
    }

    // ---------------------------------------------------------------- 音效

    /// <summary>播放一次音效（自动挑一个空闲播放器）。</summary>
    public void PlaySfx(string name, float volumeScale = 1f, float pitch = 1f)
    {
        if (string.IsNullOrEmpty(name)) return;

        var stream = LoadStream(SfxDir, name);
        if (stream == null) return;   // 没有这个文件 → 静默

        var player = NextSfxPlayer();
        player.Stream = stream;
        player.VolumeDb = Mathf.LinearToDb(
            Mathf.Max(0.0001f, SfxVolume * volumeScale * GetSfxScale(name)));
        player.PitchScale = pitch;
        player.Play();
    }

    /// <summary>查表取这个音效的独立倍率，没配就返回 1（不改动）。</summary>
    private float GetSfxScale(string name)
    {
        if (SfxVolumeScale != null && SfxVolumeScale.TryGetValue(name, out float s))
            return s;
        return 1f;
    }

    private AudioStreamPlayer NextSfxPlayer()
    {
        // 先找没在播的
        foreach (var p in _sfxPool)
            if (!p.Playing) return p;

        // 全在播 → 轮换覆盖最早的那个
        var pick = _sfxPool[_sfxCursor % _sfxPool.Count];
        _sfxCursor++;
        return pick;
    }

    // ---------------------------------------------------------------- BGM

    /// <summary>切换背景音乐（同名不重复播）。</summary>
    public void PlayBgm(string name, float fadeTime = 0.6f)
    {
        if (string.IsNullOrEmpty(name)) return;
        if (_currentBgm == name && _bgm.Playing) return;

        var stream = LoadStream(BgmDir, name);
        if (stream == null) return;

        // OGG 是唯一能真正做到无缝循环的格式，BGM 用它就顺手开循环。
        // （mp3 循环在接缝处有一小段静音，所以只对 ogg 生效，不改变现有 mp3 曲子的行为）
        if (stream is AudioStreamOggVorbis ogg) ogg.Loop = true;

        _currentBgm = name;
        _bgm.Stream = stream;

        if (fadeTime <= 0f)
        {
            _bgm.VolumeDb = Mathf.LinearToDb(Mathf.Max(0.0001f, BgmVolume));
            _bgm.Play();
            return;
        }

        // 淡出旧曲 → 换曲 → 淡入
        KillBgmTween();
        _bgmTween = CreateTween();
        _bgmTween.TweenProperty(_bgm, "volume_db", -60f, fadeTime * 0.5f);
        _bgmTween.TweenCallback(Callable.From(() =>
        {
            _bgm.Play();
            var tw = CreateTween();
            tw.TweenProperty(_bgm, "volume_db",
                Mathf.LinearToDb(Mathf.Max(0.0001f, BgmVolume)), fadeTime * 0.5f);
        }));
    }

    public void StopBgm(float fadeTime = 0.5f)
    {
        _currentBgm = "";
        if (!_bgm.Playing) return;

        if (fadeTime <= 0f) { _bgm.Stop(); return; }

        KillBgmTween();
        _bgmTween = CreateTween();
        _bgmTween.TweenProperty(_bgm, "volume_db", -60f, fadeTime);
        _bgmTween.TweenCallback(Callable.From(_bgm.Stop));
    }

    private void KillBgmTween()
    {
        if (_bgmTween != null && _bgmTween.IsValid()) _bgmTween.Kill();
        _bgmTween = null;
    }

    // ---------------------------------------------------------------- 场景 BGM

    /// <summary>场景文件名（小写、不带扩展名）→ BGM key。要加场景音乐就在这里加一行。</summary>
    private static readonly Dictionary<string, string> SceneBgm = new()
    {
        { "firstry",      "town" },
        { "house_inside", "house" },
        { "bossarena",    "boss_fight" },
        { "underground",  "horror" },
    };

    /// <summary>
    /// 按当前场景自动切 BGM。传 <c>GetTree().CurrentScene.SceneFilePath</c> 即可。
    /// 场景不在表里就保持当前曲子不动。
    /// </summary>
    public void PlayBgmForScene(string sceneFilePath)
    {
        if (string.IsNullOrEmpty(sceneFilePath)) return;

        string key = System.IO.Path.GetFileNameWithoutExtension(sceneFilePath).ToLowerInvariant();
        if (SceneBgm.TryGetValue(key, out string bgm))
            PlayBgm(bgm);
    }

    // ---------------------------------------------------------------- 工具

    /// <summary>按「目录 + 名字」找文件，依次尝试 .ogg / .wav / .mp3。找不到返回 null。</summary>
    private static AudioStream LoadStream(string dir, string name)
    {
        string[] exts = { ".ogg", ".wav", ".mp3" };
        foreach (var ext in exts)
        {
            string path = dir + name + ext;
            if (ResourceLoader.Exists(path))
                return GD.Load<AudioStream>(path);
        }
        return null;
    }

    /// <summary>某个音效文件是否存在（方便排查）。</summary>
    public static bool HasSfx(string name)
        => LoadStream(SfxDir, name) != null;
}
