using Godot;
using System.Collections.Generic;

/// <summary>
/// 存盘系统：把 GameState 里的进度写进 user://save.json。
///
/// 三条原则：
/// ① 只在「存档点触发」和「切换关卡」时写盘，**不在退出游戏时写** ——
///    崩溃或强退时那次写入就丢了，等于没存。
/// ② 武器存的是 .tres 的资源路径，不是 WeaponData 对象本身。
///    因为 Pickup 交出去的武器是 Duplicate 出来的副本，副本的 ResourcePath 是空字符串
///    （实测确认过），所以靠 WeaponData.SourcePath 兜底。
/// ③ 读档失败一律返回 false 当没档处理，绝不半途抛异常把主菜单搞崩。
/// </summary>
public static class SaveSystem
{
    private const string SavePath = "user://save.json";
    private const int SaveVersion = 1;

    /// <summary>读档成功后要进哪个场景。只有 LoadIntoState 返回 true 时才有值。</summary>
    public static string PendingScene { get; private set; } = "";

    /// <summary>盘上有没有存档。</summary>
    public static bool HasSave() => FileAccess.FileExists(SavePath);

    /// <summary>删掉存档（新游戏时调用）。</summary>
    public static void Delete()
    {
        if (!HasSave()) return;
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SavePath));
    }

    /// <summary>把当前进度写盘。任何异常情况返回 false，不抛。</summary>
    public static bool Save()
    {
        var gs = GameState.I;
        if (gs == null) return false;

        var tree = gs.GetTree();
        if (tree == null) return false;

        string scenePath = tree.CurrentScene?.SceneFilePath ?? "";
        if (string.IsNullOrEmpty(scenePath)) return false;

        var player = tree.Root.FindChild("box", true, false) as Box;

        var d = new Godot.Collections.Dictionary
        {
            ["version"] = SaveVersion,
            ["scene"] = scenePath,
            ["hp"] = player != null ? player.Hp : 100,
            ["slot"] = player != null ? player.CurrentSlot : -1,
        };

        // 出生点直接用玩家此刻的位置，不依赖存档点自己的坐标约定
        var spawn = new Godot.Collections.Array();
        if (player != null)
        {
            spawn.Add(player.GlobalPosition.X);
            spawn.Add(player.GlobalPosition.Y);
        }
        d["spawn"] = spawn;

        // 武器槽 → 资源路径（空槽写空字符串）
        var weapons = new Godot.Collections.Array();
        if (player != null)
        {
            foreach (var w in player.Weapons)
                weapons.Add(WeaponPathOf(w));
        }
        d["weapons"] = weapons;

        d["vehicle"] = new Godot.Collections.Dictionary
        {
            ["has"] = gs.HasVehicle,
            ["sheet"] = gs.VehicleSheetPath ?? "",
            ["hframes"] = gs.VehicleHframes,
            ["scale"] = gs.VehicleScale,
        };

        d["revive"] = new Godot.Collections.Dictionary
        {
            ["key"] = gs.RevivePointKey ?? "",
            ["scene"] = gs.ReviveScenePath ?? "",
            ["x"] = gs.RevivePosition.X,
            ["y"] = gs.RevivePosition.Y,
        };

        d["talked"] = ToArray(gs.TalkedKeys);
        d["picked"] = ToArray(gs.PickedKeys);

        // 已通关：主菜单读档后靠它弹「游戏已通关」提示
        d["cleared"] = gs.Cleared;

        using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        if (f == null)
        {
            GD.PushWarning($"[Save] 写盘失败：{FileAccess.GetOpenError()}");
            return false;
        }
        f.StoreString(Json.Stringify(d, "\t"));
        return true;
    }

    /// <summary>
    /// 读档并写进 GameState（不切场景，切场景由调用方决定）。
    /// 成功后 <see cref="PendingScene"/> 就是目标场景路径。
    /// </summary>
    public static bool LoadIntoState()
    {
        PendingScene = "";
        if (!HasSave()) return false;

        string text;
        using (var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read))
        {
            if (f == null) return false;
            text = f.GetAsText();
        }
        if (string.IsNullOrEmpty(text)) return false;

        var parsed = Json.ParseString(text);
        if (parsed.VariantType != Variant.Type.Dictionary) return false;

        var d = parsed.AsGodotDictionary();

        int ver = d.ContainsKey("version") ? d["version"].AsInt32() : 0;
        if (ver != SaveVersion) return false;      // 版本对不上就当没档

        string scene = d.ContainsKey("scene") ? d["scene"].AsString() : "";
        if (string.IsNullOrEmpty(scene)) return false;

        var gs = GameState.I;
        if (gs == null) return false;

        // 血量
        gs.PendingHp = d.ContainsKey("hp") ? d["hp"].AsInt32() : 100;

        // 出生点：没有就不设（null = 用场景里摆的位置）
        Vector2? spawn = null;
        if (d.ContainsKey("spawn"))
        {
            var arr = d["spawn"].AsGodotArray();
            if (arr.Count >= 2)
                spawn = new Vector2(arr[0].AsSingle(), arr[1].AsSingle());
        }
        gs.PendingSpawn = spawn;

        // 武器：按路径重新加载，再复制一份（避免和 .tres 共享实例被就地改坏）
        if (d.ContainsKey("weapons"))
        {
            var arr = d["weapons"].AsGodotArray();
            var weapons = new WeaponData[arr.Count];
            for (int i = 0; i < arr.Count; i++)
            {
                string p = arr[i].AsString();
                if (string.IsNullOrEmpty(p)) continue;
                var res = ResourceLoader.Load<WeaponData>(p);
                if (res != null)
                {
                    var copy = (WeaponData)res.Duplicate();
                    copy.SourcePath = p;
                    weapons[i] = copy;
                }
            }
            gs.PendingWeapons = weapons;
            gs.PendingSlot = d.ContainsKey("slot") ? d["slot"].AsInt32() : -1;
        }

        // 载具
        if (d.ContainsKey("vehicle"))
        {
            var v = d["vehicle"].AsGodotDictionary();
            gs.HasVehicle = v.ContainsKey("has") && v["has"].AsBool();
            gs.VehicleSheetPath = NullIfEmpty(v, "sheet");
            gs.VehicleHframes = v.ContainsKey("hframes") ? v["hframes"].AsInt32() : 1;
            gs.VehicleScale = v.ContainsKey("scale") ? v["scale"].AsSingle() : 0.7f;
        }

        // 复活点
        if (d.ContainsKey("revive"))
        {
            var r = d["revive"].AsGodotDictionary();
            gs.RevivePointKey = NullIfEmpty(r, "key");
            gs.ReviveScenePath = NullIfEmpty(r, "scene");
            gs.RevivePosition = new Vector2(
                r.ContainsKey("x") ? r["x"].AsSingle() : 0f,
                r.ContainsKey("y") ? r["y"].AsSingle() : 0f);
        }

        // 聊过 / 捡过
        gs.RestoreRecords(
            ReadStringList(d, "talked"),
            ReadStringList(d, "picked"));

        // 已通关（老存档没有这个键 → false）
        gs.Cleared = d.ContainsKey("cleared") && d["cleared"].AsBool();

        PendingScene = scene;
        return true;
    }

    // ── 小工具 ────────────────────────────────────────────────

    /// <summary>取武器能用来存档的路径：副本优先用 SourcePath，原资源用 ResourcePath。</summary>
    private static string WeaponPathOf(WeaponData w)
    {
        if (w == null) return "";
        if (!string.IsNullOrEmpty(w.SourcePath)) return w.SourcePath;
        return w.ResourcePath ?? "";
    }

    private static Godot.Collections.Array ToArray(IEnumerable<string> src)
    {
        var arr = new Godot.Collections.Array();
        if (src == null) return arr;
        foreach (var s in src)
            if (!string.IsNullOrEmpty(s)) arr.Add(s);
        return arr;
    }

    private static List<string> ReadStringList(Godot.Collections.Dictionary d, string key)
    {
        var list = new List<string>();
        if (!d.ContainsKey(key)) return list;
        foreach (var v in d[key].AsGodotArray())
        {
            string s = v.AsString();
            if (!string.IsNullOrEmpty(s)) list.Add(s);
        }
        return list;
    }

    private static string NullIfEmpty(Godot.Collections.Dictionary d, string key)
    {
        if (!d.ContainsKey(key)) return null;
        string s = d[key].AsString();
        return string.IsNullOrEmpty(s) ? null : s;
    }
}
