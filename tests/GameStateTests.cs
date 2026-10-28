using Xunit;

namespace MyGameTests;

/// <summary>
/// 测试纯 C# 类 GameRecords（不依赖 Godot，所以能脱离引擎运行）。
/// 这类「记忆集合」逻辑是跨场景状态的核心，测试它 = 验证玩家进度不会被记丢。
/// </summary>
public class GameRecordsTests
{
    [Fact]
    public void MarkTalked_Then_HasTalked_IsTrue()
    {
        var r = new GameRecords();

        r.MarkTalked("王五");

        Assert.True(r.HasTalked("王五"));
    }

    [Fact]
    public void NotTalked_Then_HasTalked_IsFalse()
    {
        var r = new GameRecords();

        Assert.False(r.HasTalked("王五"));
    }

    [Fact]
    public void MarkTalked_Twice_IsIdempotent()
    {
        var r = new GameRecords();

        r.MarkTalked("小红");
        r.MarkTalked("小红");

        Assert.True(r.HasTalked("小红"));
    }

    [Fact]
    public void EmptyOrNull_DoesNotStick()
    {
        var r = new GameRecords();

        r.MarkTalked("");
        r.MarkTalked(null!);

        Assert.False(r.HasTalked(""));
    }

    [Fact]
    public void Reset_ClearsTalked()
    {
        var r = new GameRecords();
        r.MarkTalked("小明");

        r.Reset();

        Assert.False(r.HasTalked("小明"));
    }

    [Fact]
    public void PickedUp_And_Reset_MatchTalkedBehaviour()
    {
        var r = new GameRecords();

        r.MarkPickedUp("res://items/钥匙.tscn");
        Assert.True(r.HasPickedUp("res://items/钥匙.tscn"));

        r.Reset();
        Assert.False(r.HasPickedUp("res://items/钥匙.tscn"));
    }
}