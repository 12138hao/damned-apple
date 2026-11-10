extends SceneTree

# 逐个数确认能被 GD.Load 成 AudioStream（能 load 说明导入没问题）
func _initialize() -> void:
	var sfx := ["snake_spit", "snake_tail", "tree_shake", "snake_roar",
				"snake_summon", "snake_hurt", "snake_die"]
	for n in sfx:
		var s = load("res://assets/audio/sfx/%s.mp3" % n)
		if s == null:
			print("[Load] %-14s 加载失败" % n)
		else:
			print("[Load] %-14s %s  长 %.2fs" % [n, s.get_class(), s.get_length()])
	for n in ["eden", "snake_fight"]:
		var s = load("res://assets/audio/bgm/%s.mp3" % n)
		if s == null:
			print("[Load] %-14s 加载失败" % n)
		else:
			print("[Load] %-14s %s  长 %.2fs" % [n, s.get_class(), s.get_length()])
	quit()
