/// <summary>
/// 纯 C# 的跨场景记录，不依赖 Godot，可在单元测试里直接 new。
/// 存放「已经聊过的 NPC」「已经捡过的物品」这类需要跨场景记住的集合。
/// GameState 将来可以组合本类，把状态逻辑从 Godot 节点里剥离出来，便于测试。
/// </summary>
using System;
using System.Collections.Generic;

public sealed class GameRecords
{
    private readonly HashSet<string> _talked = new(StringComparer.Ordinal);
    private readonly HashSet<string> _pickedUp = new(StringComparer.Ordinal);

    /// <summary>这个 NPC 是否已经聊过。</summary>
    public bool HasTalked(string npcName)
        => !string.IsNullOrEmpty(npcName) && _talked.Contains(npcName);

    /// <summary>标记这个 NPC 已经聊过。重复标记是幂等的（不会报错）。</summary>
    public void MarkTalked(string npcName)
    {
        if (!string.IsNullOrEmpty(npcName)) _talked.Add(npcName);
    }

    /// <summary>这个物品是否已经捡过。</summary>
    public bool HasPickedUp(string id)
        => !string.IsNullOrEmpty(id) && _pickedUp.Contains(id);

    /// <summary>标记物品已被捡走。重复标记幂等。</summary>
    public void MarkPickedUp(string id)
    {
        if (!string.IsNullOrEmpty(id)) _pickedUp.Add(id);
    }

    /// <summary>只清空「捡过的物品」，保留对话记录（回到存档点/新一关时用）。</summary>
    public void ClearPickedUp()
    {
        _pickedUp.Clear();
    }

    /// <summary>清空所有记录（新游戏用）。</summary>
    public void Reset()
    {
        _talked.Clear();
        _pickedUp.Clear();
    }
}