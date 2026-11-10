extends SceneTree

# 粉笔打召唤物：确认投射物有没有生成、有没有命中、召唤物掉不掉血
func _initialize() -> void:
	var scn: Node2D = load("res://eden.tscn").instantiate()
	root.add_child(scn)
	for i in range(10):
		await process_frame

	var box: Node2D = scn.get_node("box")
	var arena: Node = scn.get_node("SnakeBossArena")
	var boss: Node = arena.get_node("SnakeBoss")

	box.global_position = Vector2(boss.global_position.x - 60.0, 684.0)
	var t := 0.0
	while t < 30.0:
		await create_timer(0.2).timeout
		t += 0.2
		if boss.State == 2:
			break
	await create_timer(0.8).timeout
	box.global_position = Vector2(boss.global_position.x - 130.0, 684.0)
	await create_timer(0.6).timeout

	# 召唤物放远一点，让粉笔有飞行距离
	var m: Node2D = load("res://SnakeMinion.tscn").instantiate()
	arena.add_child(m)
	m.global_position = box.global_position + Vector2(-150, 0)
	m.HopForward = 0.0
	m.HopUp = 0.0
	m.HopInterval = 99.0
	await create_timer(0.7).timeout
	print("[Chalk] 召唤物 pos=%s hp=%d  与玩家距离=%.1f" % [
		str(m.global_position), m.Hp, box.global_position.distance_to(m.global_position)])

	# 给粉笔
	var chalk = load("res://assets/weapons/chalk.tres")
	var slots = box.Weapons
	slots[0] = chalk
	box.Weapons = slots
	Input.action_press("slot1")
	await create_timer(0.2).timeout
	Input.action_release("slot1")
	await create_timer(0.3).timeout

	# 瞄准召唤物的身体中部（画布坐标 = 玩家屏幕位置 + 世界偏移 × 缩放）
	var sp: Vector2 = box.get_global_transform_with_canvas().origin
	var mm := InputEventMouseMotion.new()
	mm.position = sp + Vector2(-120, -18)
	Input.parse_input_event(mm)
	await create_timer(0.2).timeout
	print("[Chalk] Holding=%s 活着=%s" % [str(box.Holding), str(box.IsAlive)])
	print("[Chalk] 玩家屏幕=%s 鼠标=%s Current=%s IsRanged=%s" % [
		str(sp), str(mm.position), str(box.Current),
		str(box.Current.IsRanged if box.Current != null else "无")])

	Input.action_press("attack")
	for i in range(24):
		await create_timer(0.05).timeout
		var n := 0
		for c in AllNodes(scn):
			var sc = c.get_script()
			if sc != null and str(sc.resource_path).ends_with("ChalkProjectile.cs"):
				n += 1
				print("[Chalk] t=%.2f 投射物 pos=%s dmg=%s" % [i * 0.05, str(c.global_position), str(c.Damage)])
		if n == 0 and i in [0, 6, 12]:
			print("[Chalk] t=%.2f 场上无投射物  hp=%d" % [i * 0.05, m.Hp])
	Input.action_release("attack")
	await create_timer(0.3).timeout
	print("[Chalk] 打完 hp=%d" % m.Hp)
	quit()

func AllNodes(n: Node) -> Array:
	var out := [n]
	for c in n.get_children():
		out += AllNodes(c)
	return out
