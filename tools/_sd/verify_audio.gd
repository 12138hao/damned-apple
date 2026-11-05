extends SceneTree

# 逐个真正加载一遍，打印时长，确认 Godot 已经导入成功
func _initialize() -> void:
	var list := {
		"sfx": ["mud_splash","mud_step","apple_swing","apple_hurt","apple_die","apple_fire","apple_grab","apple_throw"],
		"bgm": ["jungle","jungle_inside"],
	}
	for kind in list:
		print("=== ", kind, " ===")
		for n in list[kind]:
			var found := ""
			for e in [".ogg", ".wav", ".mp3"]:
				var p := "res://assets/audio/%s/%s%s" % [kind, n, e]
				if ResourceLoader.exists(p):
					found = p
					break
			if found == "":
				print("  ✗ %-14s 文件不存在" % n)
				continue
			var st: AudioStream = load(found)
			if st == null:
				print("  ✗ %-14s 加载失败" % n)
			else:
				print("  ✓ %-14s %6.2f 秒   %s" % [n, st.get_length(), found.get_file()])
	quit()
