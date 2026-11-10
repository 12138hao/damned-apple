extends SceneTree

# 黑蛇 Boss 实机测试：把玩家挪到战区，等对话结束开打，按时间点抓帧
const MARKS := [
	{"t": 3.0,  "n": "snake_01_talk"},
	{"t": 9.0,  "n": "snake_02_skill"},
	{"t": 12.0, "n": "snake_03_skill"},
	{"t": 15.0, "n": "snake_04_skill"},
	{"t": 18.0, "n": "snake_05_skill"},
	{"t": 24.0, "n": "snake_06_enrage"},
	{"t": 28.0, "n": "snake_07_enrage"},
]

func _initialize() -> void:
	var scn: Node2D = load("res://eden.tscn").instantiate()
	root.add_child(scn)
	for i in range(8):
		await process_frame

	var box: Node2D = scn.get_node("box")
	box.global_position = Vector2(6900.0, 684.0)
	box.set_physics_process(true)

	var boss: Node = scn.get_node_or_null("SnakeBossArena/SnakeBoss")
	print("boss=", boss)

	var prev := 0.0
	var enraged := false
	for m in MARKS:
		await create_timer(m["t"] - prev).timeout
		prev = m["t"]

		# 24 秒那一档把血压到半血以下，测狂暴
		if m["n"] == "snake_06_enrage" and boss != null and not enraged:
			boss.DrainHp(2600)
			enraged = true
			print("-> 强制扣血触发狂暴")

		if boss != null:
			print("t=%.1f  hp=%d  state=%s" % [m["t"], boss.Hp, boss.State])

		var img := root.get_viewport().get_texture().get_image()
		if img == null:
			print("!! 拿不到视口")
			break
		var path := "res://docs/screenshots/%s.png" % m["n"]
		img.save_png(path)
		print("[Shot] ", path)

	quit()
