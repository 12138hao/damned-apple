extends SceneTree

# 列出 eden 里所有可配的音效名，并检查对应文件在不在
func _initialize() -> void:
	var arena: Node = load("res://SnakeBossArena.tscn").instantiate()
	root.add_child(arena)
	for i in range(4):
		await process_frame

	var boss: Node = arena.get_node("SnakeBoss")
	print("[Sfx] ---- SnakeBoss ----")
	for p in boss.get_property_list():
		var nm: String = p.name
		if not (nm.begins_with("Sfx") or nm == "HurtSfx" or nm == "DeathSfx"):
			continue
		var v: String = str(boss.get(nm))
		print("[Sfx] %-14s = %-14s  %s" % [nm, "\"" + v + "\"", Exists(v)])

	print("[Sfx] ---- SnakeArena ----")
	print("[Sfx] 战斗曲文件 %s  ->  %s" % [str(arena.get("BattleBgm")), BgmExists(str(arena.get("BattleBgm")))])
	for p in arena.get_property_list():
		var nm: String = p.name
		if not (nm.begins_with("BattleBgm") or nm == "BattleBgmVolume"):
			continue
		print("[Sfx] %-16s = %s" % [nm, str(arena.get(nm))])

	var m: Node = load("res://SnakeMinion.tscn").instantiate()
	arena.add_child(m)
	print("[Sfx] ---- SnakeMinion ----")
	print("[Sfx] HurtSfx        = %s  %s" % [str(m.get("HurtSfx")), Exists(str(m.get("HurtSfx")))])
	print("[Sfx] DeathSfx       = %s  %s" % [str(m.get("DeathSfx")), Exists(str(m.get("DeathSfx")))])
	quit()

func Exists(n: String) -> String:
	if n == "":
		return "（留空=不播）"
	for ext in [".ogg", ".wav", ".mp3"]:
		if ResourceLoader.exists("res://assets/audio/sfx/" + n + ext):
			return "有文件 " + ext
	return "缺文件"

func BgmExists(n: String) -> String:
	for ext in [".ogg", ".wav", ".mp3"]:
		if ResourceLoader.exists("res://assets/audio/bgm/" + n + ext):
			return "有文件 " + ext
	return "缺文件"
