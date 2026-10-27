using Godot;
using System.Collections.Generic;

/// <summary>
/// 跨场景保存玩家状态。必须在「项目设置 → 自动加载(Autoload)」里注册，
/// 节点名填 GameState。
/// </summary>
public partial class GameState : Node
{
    public static GameState I { get; private set; }

    /// <summary>要恢复的血量。-1 表示用满血。</summary>
    public int PendingHp = -1;

    /// <summary>到达新场景后玩家的出生位置。null 表示用场景里摆的位置。</summary>
    public Vector2? PendingSpawn = null;

    /// <summary>要恢复的武器槽内容。null 表示不恢复。</summary>
    public WeaponData[] PendingWeapons = null;

    /// <summary>要恢复的当前手持槽。和 PendingWeapons 配套使用。</summary>
    public int PendingSlot = -1;

    /// <summary>对话进行中。门和拾取物要检查这个，避免按 E 时一边对话一边被传走。</summary>
    public bool DialogueActive { get; set; }

    /// <summary>已经聊过的 NPC 名字。切场景后仍然记得，用来区分首次/再次对话。</summary>
    private readonly HashSet<string> _talked = new();

    /// <summary>已经捡过的物品（场景路径）。切场景回来不会再刷出来。</summary>
    private readonly HashSet<string> _pickedUp = new();

    // ── 载具（跨场景保持）──
    public bool HasVehicle { get; set; }
    public string VehicleSheetPath { get; set; }
    public int VehicleHframes { get; set; } = 1;
    public float VehicleScale { get; set; } = 0.7f;

    /// <summary>最后一次激活的存档点（节点路径），用来让存档点显示"已激活"。</summary>
    public string RevivePointKey { get; set; }

    /// <summary>存档点所在的场景路径。死亡时如果不在这个场景，就先切过去再复活。</summary>
    public string ReviveScenePath { get; set; }

    /// <summary>存档点的地面位置。</summary>
    public Vector2 RevivePosition { get; set; }

    public override void _Ready()
    {
        I = this;
    }

    /// <summary>这个 NPC 是否已经聊过。</summary>
    public bool HasTalked(string npcName)
    {
        return !string.IsNullOrEmpty(npcName) && _talked.Contains(npcName);
    }

    /// <summary>标记这个 NPC 已经聊过。</summary>
    public void MarkTalked(string npcName)
    {
        if (!string.IsNullOrEmpty(npcName)) _talked.Add(npcName);
    }

    /// <summary>这个物品是否已经捡过。</summary>
    public bool HasPickedUp(string id)
    {
        return !string.IsNullOrEmpty(id) && _pickedUp.Contains(id);
    }

    /// <summary>标记物品已被捡走。</summary>
    public void MarkPickedUp(string id)
    {
        if (!string.IsNullOrEmpty(id)) _pickedUp.Add(id);
    }

    /// <summary>切场景前调用：记下血量 + 出生点 + 武器。</summary>
    /// <summary>切场景后要给玩家的受击无敌时长（跨场景复活用）。</summary>
    public float PendingInvuln = 0f;

    public void PrepareTravel(int hp, Vector2 spawn,
                              WeaponData[] weapons = null, int slot = -1,
                              float invuln = 0f)
    {
        PendingHp = hp;
        PendingSpawn = spawn;
        PendingInvuln = invuln;
        DialogueActive = false;   // 切场景时兜底：旧场景的对话气泡不会跟过来

        // 复制一份，避免旧场景销毁时数组被连带回收
        if (weapons != null)
        {
            PendingWeapons = (WeaponData[])weapons.Clone();
            PendingSlot = slot;
        }
        else
        {
            PendingWeapons = null;
            PendingSlot = -1;
        }
    }

    /// <summary>取血量（没记录就用 fallback）。</summary>
    public int TakeHp(int fallback)
    {
        return PendingHp >= 0 ? PendingHp : fallback;
    }

    /// <summary>取出生点，取完即清空，保证只生效一次。</summary>
    public Vector2? TakeSpawn()
    {
        var p = PendingSpawn;
        PendingSpawn = null;
        return p;
    }

    /// <summary>取跨场景复活的无敌时长，取完即清空。</summary>
    public float TakeInvuln()
    {
        float v = PendingInvuln;
        PendingInvuln = 0f;
        return v;
    }

    /// <summary>取武器配置。返回 null 表示没有要恢复的。</summary>
    public WeaponData[] TakeWeapons(out int slot)
    {
        slot = PendingSlot;
        var w = PendingWeapons;
        PendingWeapons = null;
        PendingSlot = -1;
        return w;
    }

    /// <summary>全部清掉，回到初始状态。</summary>
    public void Reset()
    {
        PendingHp = -1;
        PendingSpawn = null;
        PendingWeapons = null;
        PendingSlot = -1;
        PendingInvuln = 0f;

        // 新游戏：物品重新刷出来，载具也没了
        _pickedUp.Clear();
        HasVehicle = false;
        VehicleSheetPath = null;
        VehicleHframes = 1;
        VehicleScale = 0.7f;
        RevivePointKey = null;
        ReviveScenePath = null;
        RevivePosition = Vector2.Zero;
    }
}
