extends SceneTree

# 检查 C# 脚本有没有被识别成 tool 脚本 —— 只有 is_tool=true，编辑器里才会执行 _Draw

func _initialize() -> void:
	for p in ["res://Vine.cs", "res://Rope.cs", "res://SwingTrail.cs"]:
		var s = load(p)
		if s == null:
			print("%s  -> 加载失败" % p)
			continue
		print("%s  ->  is_tool=%s  class=%s" % [p, s.is_tool(), s.get_global_name()])
	quit()
